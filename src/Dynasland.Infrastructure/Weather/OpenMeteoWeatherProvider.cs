using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Dynasland.Core.Interfaces;

namespace Dynasland.Infrastructure.Weather;

/// <summary>
/// Previsão do tempo pelo Open-Meteo (https://open-meteo.com): gratuito, sem chave e sem cadastro.
/// Usa a API de geocodificação para buscar cidades e a de previsão para o tempo atual e os próximos dias.
/// </summary>
public sealed class OpenMeteoWeatherProvider(HttpClient http, TimeProvider time) : IWeatherProvider
{
    private const string GeocodingUrl = "https://geocoding-api.open-meteo.com/v1/search";
    private const string ForecastUrl = "https://api.open-meteo.com/v1/forecast";

    public string Name => "Open-Meteo";

    public async Task<IReadOnlyList<WeatherLocation>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
        {
            return [];
        }

        var url = $"{GeocodingUrl}?name={Uri.EscapeDataString(query.Trim())}&count=8&language=pt&format=json";
        var response = await http.GetFromJsonAsync<GeocodingResponse>(url, cancellationToken);
        return [.. (response?.Results ?? [])
            .Select(r => new WeatherLocation(r.Name ?? string.Empty, r.Admin1 ?? string.Empty, r.Country ?? string.Empty, r.Latitude, r.Longitude))];
    }

    public async Task<WeatherReport> GetForecastAsync(WeatherLocation location, CancellationToken cancellationToken = default)
    {
        var url = FormattableString.Invariant($"{ForecastUrl}?latitude={location.Latitude}&longitude={location.Longitude}")
            + "&current=temperature_2m,apparent_temperature,relative_humidity_2m,weather_code,wind_speed_10m,is_day"
            + "&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max"
            + "&timezone=auto&forecast_days=6&wind_speed_unit=kmh";
        var response = await http.GetFromJsonAsync<ForecastResponse>(url, cancellationToken)
            ?? throw new HttpRequestException("Resposta vazia do Open-Meteo.");
        return Map(location, response, time.GetUtcNow());
    }

    /// <summary>Converte a resposta da API (separado para testes com JSON gravado).</summary>
    internal static WeatherReport Map(WeatherLocation location, ForecastResponse response, DateTimeOffset fetchedAt)
    {
        var current = response.Current ?? throw new HttpRequestException("Previsão sem dados atuais.");
        var offset = TimeSpan.FromSeconds(response.UtcOffsetSeconds);
        var observed = DateTime.TryParse(current.Time, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)
            ? new DateTimeOffset(local, offset)
            : fetchedAt;

        var days = new List<DailyForecast>();
        if (response.Daily is { Time: { } dates } daily)
        {
            for (var i = 0; i < dates.Count; i++)
            {
                if (!DateOnly.TryParse(dates[i], CultureInfo.InvariantCulture, out var date))
                {
                    continue;
                }

                days.Add(new DailyForecast(
                    date,
                    At(daily.TemperatureMin, i),
                    At(daily.TemperatureMax, i),
                    ConditionOf((int)At(daily.WeatherCode, i)),
                    daily.PrecipitationProbabilityMax is { } chances && i < chances.Count ? chances[i] : null));
            }
        }

        return new WeatherReport(
            location,
            new CurrentWeather(
                current.Temperature,
                current.ApparentTemperature,
                (int)Math.Round(current.Humidity),
                current.WindSpeed,
                ConditionOf(current.WeatherCode),
                current.IsDay != 0,
                observed),
            days,
            fetchedAt);
    }

    /// <summary>Códigos WMO usados pelo Open-Meteo.</summary>
    internal static WeatherCondition ConditionOf(int code) => code switch
    {
        0 => WeatherCondition.Clear,
        1 or 2 => WeatherCondition.PartlyCloudy,
        3 => WeatherCondition.Cloudy,
        45 or 48 => WeatherCondition.Fog,
        >= 51 and <= 57 => WeatherCondition.Drizzle,
        (>= 61 and <= 67) or (>= 80 and <= 82) => WeatherCondition.Rain,
        (>= 71 and <= 77) or 85 or 86 => WeatherCondition.Snow,
        >= 95 => WeatherCondition.Thunderstorm,
        _ => WeatherCondition.Cloudy,
    };

    private static double At(IReadOnlyList<double>? values, int index) => values is not null && index < values.Count ? values[index] : 0;

    internal sealed record GeocodingResponse([property: JsonPropertyName("results")] List<GeocodingResult>? Results);

    internal sealed record GeocodingResult(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("admin1")] string? Admin1,
        [property: JsonPropertyName("country")] string? Country,
        [property: JsonPropertyName("latitude")] double Latitude,
        [property: JsonPropertyName("longitude")] double Longitude);

    internal sealed record ForecastResponse(
        [property: JsonPropertyName("utc_offset_seconds")] int UtcOffsetSeconds,
        [property: JsonPropertyName("current")] CurrentBlock? Current,
        [property: JsonPropertyName("daily")] DailyBlock? Daily);

    internal sealed record CurrentBlock(
        [property: JsonPropertyName("time")] string? Time,
        [property: JsonPropertyName("temperature_2m")] double Temperature,
        [property: JsonPropertyName("apparent_temperature")] double ApparentTemperature,
        [property: JsonPropertyName("relative_humidity_2m")] double Humidity,
        [property: JsonPropertyName("weather_code")] int WeatherCode,
        [property: JsonPropertyName("wind_speed_10m")] double WindSpeed,
        [property: JsonPropertyName("is_day")] int IsDay);

    internal sealed record DailyBlock(
        [property: JsonPropertyName("time")] List<string>? Time,
        [property: JsonPropertyName("weather_code")] List<double>? WeatherCode,
        [property: JsonPropertyName("temperature_2m_max")] List<double>? TemperatureMax,
        [property: JsonPropertyName("temperature_2m_min")] List<double>? TemperatureMin,
        [property: JsonPropertyName("precipitation_probability_max")] List<int?>? PrecipitationProbabilityMax);
}
