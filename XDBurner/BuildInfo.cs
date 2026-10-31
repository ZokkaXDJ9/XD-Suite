using System.IO;

namespace XDBurner;
internal static class BuildInfo
{
#if BURNER_BETA
    public const bool IsBeta = true;
    public const string DataFolder = "XDBurnerBeta";
    public const string MutexName = @"Local\XDBurner.Beta.App";
#else
    public const bool IsBeta = false;
    public const string DataFolder = "XDBurner";
    public const string MutexName = @"Local\XDBurner.App";
#endif
    public static string Version => typeof(App).Assembly.GetName().Version!.ToString(3) + (IsBeta ? "-Beta" : "");
    public static string Name => (L10n.Current == UiLanguage.German ? "XD Brenner" : "XD Burner") + (IsBeta ? " Beta" : "");
    static string LanguageFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), DataFolder, "language.txt");
    public static void InitializeLanguage()
    {
        L10n.Initialize();
        if (IsBeta) { try { if (Enum.TryParse<UiLanguage>(File.ReadAllText(LanguageFile), out var language) && Enum.IsDefined(language)) L10n.Set(language, false); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }
    public static void SetLanguage(UiLanguage language)
    {
        L10n.Set(language, !IsBeta);
        if (IsBeta) { Directory.CreateDirectory(Path.GetDirectoryName(LanguageFile)!); File.WriteAllText(LanguageFile, language.ToString()); }
    }
}
