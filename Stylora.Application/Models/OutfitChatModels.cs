namespace Stylora.Application.Models;

public sealed class OutfitChatModelSettings
{
    public string PythonExecutablePath { get; set; } = "../../armochromia_classifier/.venv/Scripts/python.exe";
    public string WorkerScriptPath { get; set; } = "../../ml/outfit-chat/gemma_intent_worker.py";
    public string ModelId { get; set; } = "google/gemma-4-26B-A4B-it";
    public int WorkerStartupTimeoutSeconds { get; set; } = 900;
    public bool WarmupWorkerOnStartup { get; set; } = false;
    public int MaxNewTokens { get; set; } = 220;
    public double Temperature { get; set; } = 0.1;
}

public sealed class WeatherApiSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.openweathermap.org/";
    public string Units { get; set; } = "metric";
    public string DefaultCountryCode { get; set; } = string.Empty;
}

public sealed class OutfitIntentResult
{
    public string Intent { get; set; } = "generate_outfit";
    public bool IsInScope { get; set; } = true;
    public string? OccasionText { get; set; }
    public string? StyleBucket { get; set; }
    public string? Location { get; set; }
    public string? DateContext { get; set; }
    public string? WeatherSummary { get; set; }
    public string? WeatherStatus { get; set; }
    public double? TemperatureC { get; set; }
    public List<string> Constraints { get; set; } = [];
    public int ShuffleCount { get; set; }
    public string ParserSource { get; set; } = "heuristic";
}

public sealed class ResolvedWeatherContext
{
    public string Source { get; set; } = "derived";
    public string LocationLabel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double TemperatureC { get; set; }
    public string ThermalBand { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public DateTimeOffset? ForecastedFor { get; set; }
}
