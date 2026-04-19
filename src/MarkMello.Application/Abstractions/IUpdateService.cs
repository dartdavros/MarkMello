using MarkMello.Application.Updates;

namespace MarkMello.Application.Abstractions;

public interface IUpdateService
{
    Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default);

    Task<UpdateDownloadResult> DownloadUpdateAsync(
        AppUpdatePackage package,
        CancellationToken cancellationToken = default);

    Task<UpdatePrepareResult> PrepareDownloadedUpdateAsync(
        AppUpdatePackage package,
        string downloadedFilePath,
        CancellationToken cancellationToken = default);
}
