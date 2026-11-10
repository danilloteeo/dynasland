using System.Globalization;

namespace Dynasland.Core.Interfaces;

// Life Center: clima (provedor externo, com cache) e informações rápidas da tela inicial.

/// <summary>Local da previsão do tempo, escolhido pelo usuário.</summary>
public sealed record WeatherLocation(string Name, string Region, string Country, double Latitude, double Longitude)
{
    public string DisplayName => string.Join(", ", new[] { Name, Region, Country }.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct());

    /// <summary>Chave do cache (coordenadas arredondadas).</summary>
    public string CacheKey => FormattableString.Invariant($"{Latitude:0.00};{Longitude:0.00}");

    /// <summary>Formato guardado nas configurações: nome|região|país|latitude|longitude.</summary>
    public string Serialize() => string.Join('|', Name, Region, Country,
        Latitude.ToString(CultureInfo.InvariantCulture), Longitude.ToString(CultureInfo.InvariantCulture));

    public static WeatherLocation? Parse(string? value)
    {
        var parts = value?.Split('|');
        return parts is { Length: 5 }
            && double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude)
            && double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude)
            ? new WeatherLocation(parts[0], parts[1], parts[2], latitude, longitude)
            : null;
    }
}

/// <summary>Condição do tempo, independente do provedor.</summary>
public enum WeatherCondition
{
    Clear,
    PartlyCloudy,
    Cloudy,
    Fog,
    Drizzle,
    Rain,
    Snow,
    Thunderstorm,
}

public sealed record CurrentWeather(
    double TemperatureC,
    double ApparentTemperatureC,
    int HumidityPercent,
    double WindKmh,
    WeatherCondition Condition,
    bool IsDay,
    DateTimeOffset ObservedAt);

public sealed record DailyForecast(DateOnly Date, double MinC, double MaxC, WeatherCondition Condition, int? PrecipitationChancePercent);

/// <param name="FetchedAt">Quando a previsão foi obtida do provedor (para nunca apresentar dado velho como atual).</param>
public sealed record WeatherReport(WeatherLocation Location, CurrentWeather Current, IReadOnlyList<DailyForecast> Days, DateTimeOffset FetchedAt);

/// <summary>Fonte de previsão do tempo (ex.: Open-Meteo). Pode lançar <see cref="HttpRequestException"/> sem conexão.</summary>
public interface IWeatherProvider
{
    string Name { get; }

    Task<IReadOnlyList<WeatherLocation>> SearchAsync(string query, CancellationToken cancellationToken = default);

    Task<WeatherReport> GetForecastAsync(WeatherLocation location, CancellationToken cancellationToken = default);
}
