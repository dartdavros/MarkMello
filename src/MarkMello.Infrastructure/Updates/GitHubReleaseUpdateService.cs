using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using MarkMello.Application.Abstractions;
using MarkMello.Application.Updates;

namespace MarkMello.Infrastructure.Updates;

public sealed class GitHubReleaseUpdateService : IUpdateService
{
    private const string ReleaseOwnerMetadataKey = "MarkMelloReleaseOwner";
    private const string ReleaseRepoMetadataKey = "MarkMelloReleaseRepo";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly string _releaseOwner;
    private readonly string _releaseRepo;
    private readonly string _currentVersion;
    private readonly ReleaseTargetDescriptor? _target;

    public GitHubReleaseUpdateService(HttpClient httpClient, Assembly? assembly = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        _httpClient = httpClient;

        assembly ??= Assembly.GetEntryAssembly() ?? typeof(GitHubReleaseUpdateService).Assembly;
        (_releaseOwner, _releaseRepo) = ResolveReleaseSource(assembly);
        _currentVersion = ResolveCurrentVersion(assembly);
        _target = ResolveReleaseTarget();
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_releaseOwner) || string.IsNullOrWhiteSpace(_releaseRepo))
        {
            return new UpdateCheckResult.SourceNotConfigured(
                "This build has no GitHub Releases source configured yet.");
        }

        if (_target is null)
        {
            return new UpdateCheckResult.UnsupportedPlatform(
                GetPlatformDisplayName(),
                GetArchitectureDisplayName(RuntimeInformation.OSArchitecture));
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://api.github.com/repos/{_releaseOwner}/{_releaseRepo}/releases/latest");

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheckResult.Failed(
                    $"GitHub Releases returned {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            await using var responseStream = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);

            var release = await JsonSerializer.DeserializeAsync<GitHubReleaseResponse>(
                    responseStream,
                    JsonOptions,
                    cancellationToken)
                .ConfigureAwait(false);

            if (release is null)
            {
                return new UpdateCheckResult.Failed("GitHub Releases returned an empty response.");
            }

            var asset = release.Assets.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, _target.AssetName, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(candidate.BrowserDownloadUrl)
                && !string.IsNullOrWhiteSpace(candidate.State)
                && string.Equals(candidate.State, "uploaded", StringComparison.OrdinalIgnoreCase));

            if (asset is null)
            {
                return new UpdateCheckResult.Failed(
                    $"Latest release does not include {_target.AssetName} for {_target.PlatformName} {_target.ArchitectureName}.");
            }

            var latestVersion = NormalizeDisplayVersion(release.TagName, release.Name);
            var publishedAt = release.PublishedAt ?? DateTimeOffset.MinValue;

            if (!IsRemoteVersionNewer(_currentVersion, latestVersion))
            {
                return new UpdateCheckResult.UpToDate(
                    _currentVersion,
                    latestVersion,
                    publishedAt,
                    release.HtmlUrl ?? BuildLatestReleasePageUrl());
            }

            return new UpdateCheckResult.UpdateAvailable(
                new AppUpdatePackage(
                    CurrentVersion: _currentVersion,
                    ReleaseVersion: latestVersion,
                    ReleaseTag: release.TagName ?? latestVersion,
                    PublishedAt: publishedAt,
                    ReleasePageUrl: release.HtmlUrl ?? BuildLatestReleasePageUrl(),
                    AssetName: asset.Name!,
                    DownloadUrl: asset.BrowserDownloadUrl!,
                    PlatformName: _target.PlatformName,
                    ArchitectureName: _target.ArchitectureName,
                    InstallAction: _target.InstallAction));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult.Failed(
                $"Couldn't check GitHub Releases: {ex.Message}");
        }
    }

    public async Task<UpdateDownloadResult> DownloadUpdateAsync(
        AppUpdatePackage package,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);

        try
        {
            var downloadDirectory = ResolveDownloadDirectory();
            Directory.CreateDirectory(downloadDirectory);

            var destinationPath = Path.Combine(downloadDirectory, package.AssetName);
            var temporaryPath = destinationPath + ".download";

            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            using var response = await _httpClient.GetAsync(
                    package.DownloadUrl,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return new UpdateDownloadResult.Failed(
                    $"GitHub download returned {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            await using (var sourceStream = await response.Content
                               .ReadAsStreamAsync(cancellationToken)
                               .ConfigureAwait(false))
            await using (var destinationStream = File.Create(temporaryPath))
            {
                await sourceStream.CopyToAsync(destinationStream, cancellationToken).ConfigureAwait(false);
            }

            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            File.Move(temporaryPath, destinationPath);
            TryApplyLinuxExecutableBit(destinationPath, package.InstallAction);

            return new UpdateDownloadResult.Success(package, destinationPath);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new UpdateDownloadResult.Failed(
                $"Couldn't download the update: {ex.Message}");
        }
    }

    public Task<UpdatePrepareResult> PrepareDownloadedUpdateAsync(
        AppUpdatePackage package,
        string downloadedFilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            if (string.IsNullOrWhiteSpace(downloadedFilePath) || !File.Exists(downloadedFilePath))
            {
                return Task.FromResult<UpdatePrepareResult>(
                    new UpdatePrepareResult.Failed("The downloaded update file could not be found."));
            }

            switch (package.InstallAction)
            {
                case AppUpdateInstallAction.LaunchInstaller:
                    LaunchWindowsInstaller(downloadedFilePath);
                    return Task.FromResult<UpdatePrepareResult>(
                        new UpdatePrepareResult.Success("Installer launched. Follow the native upgrade flow."));

                case AppUpdateInstallAction.OpenDiskImage:
                    StartCommand("open", downloadedFilePath);
                    return Task.FromResult<UpdatePrepareResult>(
                        new UpdatePrepareResult.Success("DMG opened. Continue with the native macOS install flow."));

                case AppUpdateInstallAction.RevealFile:
                    StartCommand("xdg-open", Path.GetDirectoryName(downloadedFilePath)!);
                    return Task.FromResult<UpdatePrepareResult>(
                        new UpdatePrepareResult.Success("The AppImage was revealed in your file manager."));

                default:
                    return Task.FromResult<UpdatePrepareResult>(
                        new UpdatePrepareResult.Failed("This platform does not define a post-download update action."));
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult<UpdatePrepareResult>(
                new UpdatePrepareResult.Failed($"Couldn't hand off the downloaded update: {ex.Message}"));
        }
    }

    private static (string Owner, string Repo) ResolveReleaseSource(Assembly assembly)
    {
        var metadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>();
        var owner = metadata.FirstOrDefault(static attribute =>
                string.Equals(attribute.Key, ReleaseOwnerMetadataKey, StringComparison.Ordinal))
            ?.Value;
        var repo = metadata.FirstOrDefault(static attribute =>
                string.Equals(attribute.Key, ReleaseRepoMetadataKey, StringComparison.Ordinal))
            ?.Value;

        return (owner ?? string.Empty, repo ?? string.Empty);
    }

    private static string ResolveCurrentVersion(Assembly assembly)
    {
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return NormalizeDisplayVersion(informationalVersion, null);
        }

        var version = assembly.GetName().Version;
        return version is null
            ? "1.0.0"
            : $"{version.Major}.{Math.Max(version.Minor, 0)}.{Math.Max(version.Build, 0)}";
    }

    private static ReleaseTargetDescriptor? ResolveReleaseTarget()
    {
        var architecture = RuntimeInformation.OSArchitecture;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return architecture switch
            {
                Architecture.X64 => new ReleaseTargetDescriptor(
                    "Windows",
                    "x64",
                    "MarkMello-setup-win-x64.exe",
                    AppUpdateInstallAction.LaunchInstaller),
                Architecture.Arm64 => new ReleaseTargetDescriptor(
                    "Windows",
                    "arm64",
                    "MarkMello-setup-win-arm64.exe",
                    AppUpdateInstallAction.LaunchInstaller),
                _ => null
            };
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return architecture switch
            {
                Architecture.Arm64 => new ReleaseTargetDescriptor(
                    "macOS",
                    "arm64",
                    "MarkMello-macos-arm64.dmg",
                    AppUpdateInstallAction.OpenDiskImage),
                Architecture.X64 => new ReleaseTargetDescriptor(
                    "macOS",
                    "x64",
                    "MarkMello-macos-x64.dmg",
                    AppUpdateInstallAction.OpenDiskImage),
                _ => null
            };
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return architecture switch
            {
                Architecture.X64 => new ReleaseTargetDescriptor(
                    "Linux",
                    "x86_64",
                    "MarkMello-linux-x86_64.AppImage",
                    AppUpdateInstallAction.RevealFile),
                Architecture.Arm64 => new ReleaseTargetDescriptor(
                    "Linux",
                    "aarch64",
                    "MarkMello-linux-aarch64.AppImage",
                    AppUpdateInstallAction.RevealFile),
                _ => null
            };
        }

        return null;
    }

    private string BuildLatestReleasePageUrl()
        => $"https://github.com/{_releaseOwner}/{_releaseRepo}/releases/latest";

    private static string NormalizeDisplayVersion(string? tagName, string? releaseName)
    {
        var candidate = !string.IsNullOrWhiteSpace(tagName)
            ? tagName
            : releaseName;

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return "unknown";
        }

        var trimmed = candidate.Trim();
        if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[1..];
        }

        var buildMetadataIndex = trimmed.IndexOf('+');
        return buildMetadataIndex >= 0
            ? trimmed[..buildMetadataIndex]
            : trimmed;
    }

    private static bool IsRemoteVersionNewer(string currentVersion, string latestVersion)
    {
        var current = TryParseVersionCore(currentVersion);
        var latest = TryParseVersionCore(latestVersion);

        if (current is not null && latest is not null)
        {
            return latest > current;
        }

        return !string.Equals(
            NormalizeDisplayVersion(currentVersion, null),
            NormalizeDisplayVersion(latestVersion, null),
            StringComparison.OrdinalIgnoreCase);
    }

    private static Version? TryParseVersionCore(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        var normalized = NormalizeDisplayVersion(version, null);
        var separatorIndex = normalized.IndexOfAny(['-', '+']);
        if (separatorIndex >= 0)
        {
            normalized = normalized[..separatorIndex];
        }

        return Version.TryParse(normalized, out var parsed)
            ? parsed
            : null;
    }

    private static string ResolveDownloadDirectory()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            return Path.Combine(userProfile, "Downloads", "MarkMello");
        }

        return Path.Combine(AppContext.BaseDirectory, "Updates");
    }

    private static void TryApplyLinuxExecutableBit(string downloadedFilePath, AppUpdateInstallAction action)
    {
        if (!OperatingSystem.IsLinux() || action != AppUpdateInstallAction.RevealFile)
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(
                downloadedFilePath,
                UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead
                | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead
                | UnixFileMode.OtherExecute);
        }
        catch
        {
            // Best-effort only: failed chmod must not hide a successfully
            // downloaded AppImage from the user.
        }
    }

    private static void LaunchWindowsInstaller(string installerPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = installerPath,
            UseShellExecute = true
        };

        Process.Start(startInfo);
    }

    private static void StartCommand(string fileName, string argument)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(argument);

        Process.Start(startInfo);
    }

    private static string GetPlatformDisplayName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "Windows";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "macOS";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "Linux";
        }

        return "Unknown";
    }

    private static string GetArchitectureDisplayName(Architecture architecture)
        => architecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            Architecture.Arm => "arm",
            _ => architecture.ToString()
        };

    private sealed record ReleaseTargetDescriptor(
        string PlatformName,
        string ArchitectureName,
        string AssetName,
        AppUpdateInstallAction InstallAction);

    private sealed class GitHubReleaseResponse
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; init; }

        [JsonPropertyName("published_at")]
        public DateTimeOffset? PublishedAt { get; init; }

        [JsonPropertyName("assets")]
        public GitHubReleaseAssetResponse[] Assets { get; init; } = [];
    }

    private sealed class GitHubReleaseAssetResponse
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; init; }

        [JsonPropertyName("state")]
        public string? State { get; init; }
    }
}
