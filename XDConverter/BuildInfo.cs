namespace XDConverter;

internal static class BuildInfo
{
#if CONVERTER_PREVIEW
    public const bool IsPreview = true;
    public const string DataFolder = "XDConverterPreview";
    public const string OutputFolder = "XD Konverter Vorschau";
    public const string MutexName = @"Local\XDConverter.Preview.App";
#else
    public const bool IsPreview = false;
    public const string DataFolder = "XDConverter";
    public const string OutputFolder = "XD Konverter";
    public const string MutexName = @"Local\XDConverter.App";
#endif
}
