using MarkMello.Application.Abstractions;
using MarkMello.Infrastructure.Platform.SingleInstance;

namespace MarkMello.Presentation.Tests;

public sealed class SingleInstanceChannelTests : IDisposable
{
    private readonly string _tempFile;

    public SingleInstanceChannelTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"markmello-single-{Guid.NewGuid():N}.md");
        File.WriteAllText(_tempFile, "# Single");
    }

    public void Dispose() => File.Delete(_tempFile);

    [Fact]
    public void MessageRoundTripsPaths()
    {
        using var stream = new MemoryStream();
        string[] paths = [_tempFile, "C:\\Документы\\заметки.md"];

        SingleInstanceChannel.Write(stream, paths);
        stream.Position = 0;

        Assert.Equal(paths, SingleInstanceChannel.TryRead(stream));
    }

    [Fact]
    public void ForeignOrTruncatedMessageIsIgnored()
    {
        using var foreign = new MemoryStream([42, 0, 0, 0, 1, 0, 0, 0]);
        Assert.Null(SingleInstanceChannel.TryRead(foreign));

        using var truncated = new MemoryStream([1, 0, 0, 0, 2, 0, 0, 0]);
        Assert.Null(SingleInstanceChannel.TryRead(truncated));
    }

    [Fact]
    public void ResolvePathsKeepsExistingPathsAndSkipsSwitches()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.md");

        var paths = SingleInstanceChannel.ResolvePaths(["--smoke-exit-after-open", missing, _tempFile]);

        Assert.Equal(new[] { Path.GetFullPath(_tempFile) }, paths);
    }

    [Fact]
    public async Task ForwardedPathsReachListeningInstance()
    {
        // Сквозная проверка канала: повторный запуск пишет аргументы,
        // первый экземпляр поднимает событие с теми же путями.
        var name = $"MarkMello-test-{Guid.NewGuid():N}"[..32];
        using var activation = new PipeInstanceActivation(name);
        var received = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        activation.Activated += (_, e) => received.TrySetResult(e.Paths);
        activation.StartListening();

        Assert.True(SingleInstanceGuard.TrySend(name, [_tempFile]));

        var paths = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(new[] { _tempFile }, paths);
    }

    [Fact]
    public async Task PathsForwardedBeforeWindowIsShownAreDeliveredOnSubscribe()
    {
        // Несколько файлов, открытых разом из проводника: вторичные процессы
        // успевают переслать пути раньше, чем первый покажет окно и подпишется.
        var name = $"MarkMello-test-{Guid.NewGuid():N}"[..32];
        using var activation = new PipeInstanceActivation(name);
        activation.StartListening();

        Assert.True(SingleInstanceGuard.TrySend(name, [_tempFile]));
        Assert.True(SingleInstanceGuard.TrySend(name, []));

        var received = new List<IReadOnlyList<string>>();
        var bothArrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await WaitUntilAsync(() => activation.PendingCount == 2);

        activation.Activated += (_, e) =>
        {
            lock (received)
            {
                received.Add(e.Paths);
                if (received.Count == 2)
                {
                    bothArrived.TrySetResult();
                }
            }
        };

        await bothArrived.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(new[] { _tempFile }, received[0]);
        Assert.Empty(received[1]);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Сообщения не дошли до канала.");
            await Task.Delay(20);
        }
    }

    [Fact]
    public void EmptyLaunchIsForwardedAsEmptyList()
    {
        // Запуск без файла тоже доходит до первого экземпляра — чтобы вывести окно вперёд.
        using var stream = new MemoryStream();

        SingleInstanceChannel.Write(stream, []);
        stream.Position = 0;

        var paths = SingleInstanceChannel.TryRead(stream);
        Assert.NotNull(paths);
        Assert.Empty(paths);
    }
}
