using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Stylora.Application.Interfaces;
using Stylora.Application.Models;

namespace Stylora.Infrastructure.Services;

public sealed class OpenWeatherService : IWeatherService
{
    private readonly WeatherApiSettings _settings;
    private readonly HttpClient _httpClient;

    public OpenWeatherService(WeatherApiSettings settings)
    {
        _settings = settings;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_settings.BaseUrl)
        };
    }

    public async Task<ResolvedWeatherContext?> ResolveAsync(
        OutfitIntentResult intent,
        CancellationToken cancellationToken = default)
    {
        if (intent.TemperatureC is not null || !string.IsNullOrWhiteSpace(intent.WeatherStatus))
        {
            var temperature = intent.TemperatureC ?? EstimateTemperature(intent.WeatherStatus);
            var status = NormalizeWeatherStatus(intent.WeatherStatus ?? "mild");
            return BuildResolvedWeatherContext(
                "message",
                intent.Location ?? string.Empty,
                status,
                temperature);
        }

        if (string.IsNullOrWhiteSpace(intent.Location) || string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            return null;
        }

        var geo = await ResolveCoordinatesAsync(intent.Location!, cancellationToken);
        if (geo is null)
        {
            return null;
        }

        var targetMoment = ResolveTargetMoment(intent.DateContext);
        if (targetMoment.Date > DateTimeOffset.Now.Date)
        {
            var forecast = await ResolveForecastAsync(geo.Latitude, geo.Longitude, targetMoment, cancellationToken);
            if (forecast is not null)
            {
                return BuildResolvedWeatherContext(
                    "openweather_forecast",
                    geo.Label,
                    NormalizeWeatherStatus(forecast.Status),
                    forecast.TemperatureC,
                    forecast.At);
            }
        }

        var current = await ResolveCurrentAsync(geo.Latitude, geo.Longitude, cancellationToken);
        return current is null
            ? null
            : BuildResolvedWeatherContext(
                "openweather_current",
                geo.Label,
                NormalizeWeatherStatus(current.Status),
                current.TemperatureC);
    }

    private async Task<LocationMatch?> ResolveCoordinatesAsync(string location, CancellationToken cancellationToken)
    {
        var query = Uri.EscapeDataString(
            string.IsNullOrWhiteSpace(_settings.DefaultCountryCode)
                ? location
                : $"{location},{_settings.DefaultCountryCode}");

        var response = await _httpClient.GetAsync(
            $"geo/1.0/direct?q={query}&limit=1&appid={_settings.ApiKey}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var matches = await response.Content.ReadFromJsonAsync<List<DirectGeocodeResponse>>(cancellationToken);
        var match = matches?.FirstOrDefault();
        if (match is null)
        {
            return null;
        }

        var label = string.Join(", ", new[] { match.Name, match.State, match.Country }.Where(part => !string.IsNullOrWhiteSpace(part)));
        return new LocationMatch(match.Lat, match.Lon, label);
    }

    private async Task<WeatherPayload?> ResolveCurrentAsync(double lat, double lon, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync(
            $"data/2.5/weather?lat={lat.ToString(CultureInfo.InvariantCulture)}&lon={lon.ToString(CultureInfo.InvariantCulture)}&appid={_settings.ApiKey}&units={_settings.Units}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync<CurrentWeatherResponse>(cancellationToken);
        var status = payload?.Weather?.FirstOrDefault()?.Main ?? payload?.Weather?.FirstOrDefault()?.Description;
        var temperature = payload?.Main?.FeelsLike ?? payload?.Main?.Temperature;
        return status is null || temperature is null
            ? null
            : new WeatherPayload(status, temperature.Value, DateTimeOffset.Now);
    }

    private async Task<WeatherPayload?> ResolveForecastAsync(double lat, double lon, DateTimeOffset targetMoment, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync(
            $"data/2.5/forecast?lat={lat.ToString(CultureInfo.InvariantCulture)}&lon={lon.ToString(CultureInfo.InvariantCulture)}&appid={_settings.ApiKey}&units={_settings.Units}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync<ForecastResponse>(cancellationToken);
        var bestMatch = payload?.List?
            .OrderBy(item => Math.Abs((item.At - targetMoment).TotalMinutes))
            .FirstOrDefault();

        var status = bestMatch?.Weather?.FirstOrDefault()?.Main ?? bestMatch?.Weather?.FirstOrDefault()?.Description;
        var temperature = bestMatch?.Main?.FeelsLike ?? bestMatch?.Main?.Temperature;
        return status is null || temperature is null || bestMatch is null
            ? null
            : new WeatherPayload(status, temperature.Value, bestMatch.At);
    }

    private static DateTimeOffset ResolveTargetMoment(string? dateContext)
    {
        var now = DateTimeOffset.Now;
        if (string.IsNullOrWhiteSpace(dateContext))
        {
            return now;
        }

        var lowered = dateContext.Trim().ToLowerInvariant();
        if (lowered.Contains("tomorrow", StringComparison.OrdinalIgnoreCase))
        {
            return new DateTimeOffset(now.Year, now.Month, now.Day, 12, 0, 0, now.Offset).AddDays(1);
        }

        if (lowered.Contains("tonight", StringComparison.OrdinalIgnoreCase))
        {
            return new DateTimeOffset(now.Year, now.Month, now.Day, 20, 0, 0, now.Offset);
        }

        if (lowered.Contains("weekend", StringComparison.OrdinalIgnoreCase))
        {
            var daysUntilSaturday = ((int)DayOfWeek.Saturday - (int)now.DayOfWeek + 7) % 7;
            daysUntilSaturday = daysUntilSaturday == 0 ? 7 : daysUntilSaturday;
            return new DateTimeOffset(now.Year, now.Month, now.Day, 12, 0, 0, now.Offset).AddDays(daysUntilSaturday);
        }

        if (Enum.TryParse<DayOfWeek>(CultureInfo.InvariantCulture.TextInfo.ToTitleCase(lowered), ignoreCase: true, out var dayOfWeek))
        {
            var daysUntil = ((int)dayOfWeek - (int)now.DayOfWeek + 7) % 7;
            daysUntil = daysUntil == 0 ? 7 : daysUntil;
            return new DateTimeOffset(now.Year, now.Month, now.Day, 12, 0, 0, now.Offset).AddDays(daysUntil);
        }

        return now;
    }

    private static ResolvedWeatherContext BuildResolvedWeatherContext(
        string source,
        string locationLabel,
        string status,
        double temperatureC,
        DateTimeOffset? forecastedFor = null)
    {
        var thermalBand = GetThermalBand(temperatureC);
        var summary = string.Join(", ", new[] { status, thermalBand, $"{temperatureC:0.#}C" }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Distinct(StringComparer.OrdinalIgnoreCase));

        return new ResolvedWeatherContext
        {
            Source = source,
            LocationLabel = locationLabel,
            Status = status,
            TemperatureC = temperatureC,
            ThermalBand = thermalBand,
            Summary = summary,
            ForecastedFor = forecastedFor
        };
    }

    private static string NormalizeWeatherStatus(string value)
    {
        var lowered = value.Trim().ToLowerInvariant();
        return lowered switch
        {
            var status when status.Contains("rain", StringComparison.OrdinalIgnoreCase) => "rainy",
            var status when status.Contains("cloud", StringComparison.OrdinalIgnoreCase) => "cloudy",
            var status when status.Contains("clear", StringComparison.OrdinalIgnoreCase) || status.Contains("sun", StringComparison.OrdinalIgnoreCase) => "sunny",
            var status when status.Contains("snow", StringComparison.OrdinalIgnoreCase) => "snowy",
            var status when status.Contains("storm", StringComparison.OrdinalIgnoreCase) || status.Contains("thunder", StringComparison.OrdinalIgnoreCase) => "stormy",
            var status when status.Contains("wind", StringComparison.OrdinalIgnoreCase) => "windy",
            _ => lowered
        };
    }

    private static double EstimateTemperature(string? status)
    {
        var lowered = status?.Trim().ToLowerInvariant() ?? string.Empty;
        return lowered switch
        {
            "freezing" => 2,
            "cold" => 8,
            "cool" => 15,
            "warm" => 23,
            "hot" => 30,
            _ => 18
        };
    }

    private static string GetThermalBand(double temperatureC)
    {
        return temperatureC switch
        {
            <= 4 => "freezing",
            <= 11 => "cold",
            <= 18 => "cool",
            <= 26 => "warm",
            _ => "hot"
        };
    }

    private sealed record LocationMatch(double Latitude, double Longitude, string Label);
    private sealed record WeatherPayload(string Status, double TemperatureC, DateTimeOffset At);

    private sealed class DirectGeocodeResponse
    {
        public string Name { get; set; } = string.Empty;
        public string? State { get; set; }
        public string? Country { get; set; }
        public double Lat { get; set; }
        public double Lon { get; set; }
    }

    private sealed class CurrentWeatherResponse
    {
        public CurrentWeatherMain? Main { get; set; }
        public List<WeatherDescription>? Weather { get; set; }
    }

    private sealed class ForecastResponse
    {
        public List<ForecastItem>? List { get; set; }
    }

    private sealed class ForecastItem
    {
        public CurrentWeatherMain? Main { get; set; }
        public List<WeatherDescription>? Weather { get; set; }
        public DateTimeOffset At { get; set; }

        public long Dt
        {
            set => At = DateTimeOffset.FromUnixTimeSeconds(value);
        }
    }

    private sealed class CurrentWeatherMain
    {
        [JsonPropertyName("temp")]
        public double Temperature { get; set; }

        [JsonPropertyName("feels_like")]
        public double? FeelsLike { get; set; }
    }

    private sealed class WeatherDescription
    {
        public string? Main { get; set; }
        public string? Description { get; set; }
    }
}
