using MarkMello.Domain;
using MarkMello.Presentation.Localization;
using System.Reflection;

namespace MarkMello.Presentation.Tests;

public sealed class LocalizationServiceTests
{
    [Fact]
    public void EnglishAndRussianDictionariesExposeSameKeys()
    {
        var english = GetDictionary("English");
        var russian = GetDictionary("Russian");

        Assert.Empty(english.Keys.Except(russian.Keys));
        Assert.Empty(russian.Keys.Except(english.Keys));
    }


    [Theory]
    [InlineData(AppLanguage.English)]
    [InlineData(AppLanguage.Russian)]
    public void OutlineStringsResolveInBothLanguages(AppLanguage language)
    {
        var localization = new LocalizationService(language);
        string[] keys =
        [
            "OutlineHeader", "OutlineEmpty", "OutlineToggleTooltip", "OutlineSettingsHeader",
            "OutlineSideLabel", "OutlineSideHint", "OutlineSideLeft", "OutlineSideRight",
            "OutlineMaxLevelLabel", "OutlineMaxLevelHint", "OutlineLineHeightLabel",
            "OutlineLineHeightHint", "OutlineLevelSizesLabel", "OutlineLevelSizesHint"
        ];

        foreach (var key in keys)
        {
            var value = localization[key];
            Assert.NotEqual($"[[{key}]]", value);
            Assert.False(string.IsNullOrWhiteSpace(value), key);
        }
    }

    [Fact]
    public void SetLanguageRaisesIndexerNotificationsForActiveBindings()
    {
        var localization = new LocalizationService(AppLanguage.English);
        var names = new List<string?>();
        localization.PropertyChanged += (_, e) => names.Add(e.PropertyName);

        localization.SetLanguage(AppLanguage.Russian);

        Assert.Contains(nameof(ILocalizationService.SelectedLanguage), names);
        Assert.Contains(nameof(ILocalizationService.EffectiveLanguage), names);
        Assert.Contains(nameof(ILocalizationService.Culture), names);
        Assert.Contains("Item", names);
        Assert.Contains("Item[]", names);
        Assert.Contains(string.Empty, names);
        Assert.Equal("Тихое место для чтения Markdown.", localization["WelcomeTagline"]);
    }

    private static IReadOnlyDictionary<string, string> GetDictionary(string fieldName)
    {
        var field = typeof(LocalizationService).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
        if (field is null)
        {
            throw new InvalidOperationException($"Localization dictionary '{fieldName}' was not found.");
        }

        var value = field.GetValue(null);
        return Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(value);
    }
}
