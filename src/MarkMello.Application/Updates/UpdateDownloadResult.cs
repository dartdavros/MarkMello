namespace MarkMello.Application.Updates;

public abstract record UpdateDownloadResult
{
    private UpdateDownloadResult()
    {
    }

    public sealed record Success(AppUpdatePackage Package, string DownloadedFilePath) : UpdateDownloadResult;

    public sealed record Failed(string Message) : UpdateDownloadResult;
}
