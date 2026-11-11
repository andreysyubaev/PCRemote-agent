
using System;
using System.Globalization;
using System.IO;
using System.Resources;

namespace PCRemote.Desktop.Localization;

public static class LocalizationManager
{
    private static readonly ResourceManager ResourceManager = new(
        "PCRemote.Desktop.Localization.Strings",
        typeof(LocalizationManager).Assembly);

    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PCRemote");

    private static readonly string LanguageSettingsPath = Path.Combine(
        SettingsDirectory,
        "language.txt");

    public static void Initialize()
    {
        var languagePreference = LoadLanguagePreference();

        var language = languagePreference switch
        {
            "ru" => "ru",
            "en" => "en",
            _ => GetSystemLanguage()
        };

        ApplyCulture(language);
    }

    public static string GetLanguagePreference()
    {
        return LoadLanguagePreference();
    }

    public static void SaveLanguagePreference(string language)
    {
        if (language is not ("System" or "ru" or "en"))
        {
            throw new ArgumentException(
                "Unsupported language preference.",
                nameof(language));
        }

        Directory.CreateDirectory(SettingsDirectory);

        File.WriteAllText(LanguageSettingsPath, language);
    }

    public static string GetString(string key)
    {
        return ResourceManager.GetString(
            key,
            CultureInfo.CurrentUICulture) ?? $"!{key}!";
    }

    private static string LoadLanguagePreference()
    {
        try
        {
            if (!File.Exists(LanguageSettingsPath))
                return "System";

            var language = File.ReadAllText(LanguageSettingsPath).Trim();

            return language is "System" or "ru" or "en"
                ? language
                : "System";
        }
        catch
        {
            return "System";
        }
    }

    private static string GetSystemLanguage()
    {
        return CultureInfo.InstalledUICulture.TwoLetterISOLanguageName == "ru"
            ? "ru"
            : "en";
    }

    private static void ApplyCulture(string language)
    {
        var culture = CultureInfo.GetCultureInfo(language);

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
