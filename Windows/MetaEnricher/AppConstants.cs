namespace MetaEnricher;

public static class AppConstants
{
    // Ollama
    public const string DefaultOllamaUrl   = "http://localhost:11434";
    public const string CloudOllamaUrl     = "https://ollama.com";
    public const string DefaultOllamaModel = "qwen2.5vl";

    // Library structure
    public const string DefaultPicksFolder = "Edited export";
    public const string OriginalsSubFolder = "JPEG";
    public const string RawSubFolder       = "RAW";

    // Storage filenames
    public const string SettingsFileName     = "settings.json";
    public const string SessionNotesFileName = "session_notes.json";
    public const string AppDataDirName       = "MetaEnricher";

    // Timeouts
    public const int OllamaRequestTimeoutSec = 300;
    public const int OllamaInferenceTimeoutSec = 120;
    public const int GeocodingTimeoutSec = 15;
}
