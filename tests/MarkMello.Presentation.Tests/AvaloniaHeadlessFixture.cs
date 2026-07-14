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

    public void Dispose()
    {
        Session.Dispose();
    }
}
