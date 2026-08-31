using Avalonia;
using Avalonia.Headless;
using AvaloniaApplication = Avalonia.Application;

namespace MarkMello.Presentation.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AvaloniaHeadlessTestGroup : ICollectionFixture<AvaloniaHeadlessFixture>
{
    public const string Name = nameof(AvaloniaHeadlessTestGroup);
}

public sealed class AvaloniaHeadlessFixture : IDisposable
{
    public AvaloniaHeadlessFixture()
    {
        Session = HeadlessUnitTestSession.StartNew(typeof(AvaloniaApplication));
    }

    public HeadlessUnitTestSession Session { get; }

    /// <summary>
    /// Единственный правильный способ выполнить асинхронный UI-тест.
    /// У сессии нет перегрузки <c>Dispatch(Func&lt;Task&gt;)</c>: async-лямбда связывается
    /// с <c>Dispatch&lt;T&gt;(Func&lt;T&gt;)</c>, где <c>T = Task</c>, — внешняя задача завершается,
    /// как только лямбда стартовала, и упавший тест проходит. Возврат значения
    /// уводит вызов в перегрузку <c>Func&lt;Task&lt;T&gt;&gt;</c>, которая тело действительно ждёт.
    /// </summary>
    public Task RunAsync(Func<Task> body, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(body);

        return Session.Dispatch(
            async () =>
            {
                await body().ConfigureAwait(true);
                return true;
            },
            cancellationToken);
    }

    public void Dispose()
    {
        Session.Dispose();
    }
}
