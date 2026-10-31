namespace XDConverter;

internal static class BuildInfo
{
#if CONVERTER_BETA
    public const bool IsPreview = false;
    public const bool IsBeta = true;
    public const string DataFolder = "XDConverterBeta";
    public const string OutputFolder = "XD Konverter Beta";
    public const string MutexName = @"Local\XDConverter.Beta.App";
#elif CONVERTER_PREVIEW
    public const bool IsPreview = true;
    public const bool IsBeta = false;
    public const string DataFolder = "XDConverterPreview";
    public const string OutputFolder = "XD Konverter Vorschau";
    public const string MutexName = @"Local\XDConverter.Preview.App";
#else
    public const bool IsPreview = false;
    public const bool IsBeta = false;
    public const string DataFolder = "XDConverter";
    public const string OutputFolder = "XD Konverter";
    public const string MutexName = @"Local\XDConverter.App";
#endif
}
