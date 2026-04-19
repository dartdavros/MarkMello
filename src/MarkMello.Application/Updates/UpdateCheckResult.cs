namespace MarkMello.Application.Updates;

public abstract record UpdateCheckResult
{
    private UpdateCheckResult()
    {
    }

    public sealed record SourceNotConfigured(string Message) : UpdateCheckResult;

    public sealed record UnsupportedPlatform(string PlatformName, string ArchitectureName) : UpdateCheckResult;

    public sealed record UpToDate(
        string CurrentVersion,
        string LatestVersion,
        DateTimeOffset PublishedAt,
        string ReleasePageUrl) : UpdateCheckResult;

    public sealed record UpdateAvailable(AppUpdatePackage Package) : UpdateCheckResult;

    public sealed record Failed(string Message) : UpdateCheckResult;
}
