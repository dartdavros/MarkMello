using System.Text.Json;
using System.Text.Json.Serialization;
using MarkMello.Domain;
using MarkMello.Domain.Workspace;
using MarkMello.Infrastructure.Settings;
using MarkMello.Infrastructure.Updates;

namespace MarkMello.Infrastructure.Serialization;

[JsonSourceGenerationOptions(
    AllowTrailingCommas = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    UseStringEnumConverter = true,
    WriteIndented = true)]
[JsonSerializable(typeof(SettingsFileModel))]
[JsonSerializable(typeof(ReadingPreferences))]
[JsonSerializable(typeof(WindowPlacement))]
[JsonSerializable(typeof(WorkspaceSessionState))]
[JsonSerializable(typeof(ThemeMode))]
[JsonSerializable(typeof(AppLanguage))]
[JsonSerializable(typeof(FontFamilyMode))]
[JsonSerializable(typeof(DocumentMinimapMode))]
[JsonSerializable(typeof(WindowBorderMode))]
[JsonSerializable(typeof(GitHubReleaseResponse))]
[JsonSerializable(typeof(GitHubReleaseAssetResponse))]
internal sealed partial class MarkMelloJsonSerializerContext : JsonSerializerContext;
