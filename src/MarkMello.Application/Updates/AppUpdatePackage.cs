namespace MarkMello.Application.Updates;

public sealed record AppUpdatePackage(
    string CurrentVersion,
    string ReleaseVersion,
    string ReleaseTag,
    DateTimeOffset PublishedAt,
    string ReleasePageUrl,
    string AssetName,
    string DownloadUrl,
    string PlatformName,
    string ArchitectureName,
    AppUpdateInstallAction InstallAction);
