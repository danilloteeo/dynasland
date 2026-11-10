using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.Core.Activities;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Layout;
using Dynasland.Core.Models;

namespace Dynasland.Modules.Life;

public sealed record ForecastDayViewModel(string DayName, string Glyph, string MaxText, string MinText, string? RainText);

/// <summary>Painel do clima: condição atual, sensação, umidade, vento e os próximos dias.</summary>
public sealed partial class WeatherContent(Func<Task> refresh) : ObservableObject, IActivityContent
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public ObservableCollection<ForecastDayViewModel> Days { get; } = [];

    [ObservableProperty]
    private string _locationText = string.Empty;

    [ObservableProperty]
    private string _temperatureText = "--°";

    [ObservableProperty]
    private string _conditionText = string.Empty;

    [ObservableProperty]
    private string _glyph = WeatherText.GlyphOf(WeatherCondition.Cloudy, true);

    [ObservableProperty]
    private string _detailsText = string.Empty;

    [ObservableProperty]
    private string _updatedText = string.Empty;

    [ObservableProperty]
    private bool _isStale;

    [ObservableProperty]
    private bool _hasReport;

    [ObservableProperty]
    private StatusContent? _status;

    public bool HasStatus => Status is not null;

    partial void OnStatusChanged(StatusContent? value) => OnPropertyChanged(nameof(HasStatus));

    public void Apply(WeatherReport report, DateTimeOffset now, TimeSpan staleAfter)
    {
        var current = report.Current;
        LocationText = report.Location.Name;
        TemperatureText = WeatherText.Temperature(current.TemperatureC);
        ConditionText = WeatherText.ConditionName(current.Condition);
        Glyph = WeatherText.GlyphOf(current.Condition, current.IsDay);
        DetailsText = $"Sensação {WeatherText.Temperature(current.ApparentTemperatureC)} · Umidade {current.HumidityPercent}% · Vento {current.WindKmh:0} km/h";

        // Nunca apresentar dado velho como atual: mostra a hora da consulta e marca como desatualizado.
        var age = now - report.FetchedAt;
        IsStale = age > staleAfter;
        var at = report.FetchedAt.ToLocalTime();
        UpdatedText = IsStale
            ? $"Desatualizado · consultado {(at.Date == now.ToLocalTime().Date ? at.ToString("'às' HH:mm", PtBr) : at.ToString("'em' dd/MM 'às' HH:mm", PtBr))}"
            : $"Atualizado às {at:HH:mm}";

        Days.Clear();
        foreach (var day in report.Days.Skip(1).Take(5))
        {
            var name = day.Date.ToDateTime(TimeOnly.MinValue).ToString("ddd", PtBr).TrimEnd('.');
            Days.Add(new ForecastDayViewModel(
                char.ToUpper(name[0], PtBr) + name[1..],
                WeatherText.GlyphOf(day.Condition, true),
                $"{Math.Round(day.MaxC)}°",
                $"{Math.Round(day.MinC)}°",
                day.PrecipitationChancePercent is > 0 and var chance ? $"{chance}%" : null));
        }

        HasReport = true;
        Status = null;
    }

    public void ShowStatus(StatusContent status) => Status = status;

    public IslandDimensions? GetPreferredDimensions(IslandState state) => state switch
    {
        IslandState.Interactive => new IslandDimensions(410, HasReport ? 196 : 120, 36),
        _ => null,
    };

    public void OnPresentationChanged(IslandState? state)
    {
        if (state == IslandState.Interactive)
        {
            _ = refresh();
        }
    }

    [RelayCommand]
    private Task RefreshAsync() => refresh();
}

/// <summary>Textos e ícones do clima em português.</summary>
public static class WeatherText
{
    public static string Temperature(double celsius) => $"{Math.Round(celsius):0}°";

    public static string ConditionName(WeatherCondition condition) => condition switch
    {
        WeatherCondition.Clear => "Céu limpo",
        WeatherCondition.PartlyCloudy => "Parcialmente nublado",
        WeatherCondition.Cloudy => "Nublado",
        WeatherCondition.Fog => "Neblina",
        WeatherCondition.Drizzle => "Garoa",
        WeatherCondition.Rain => "Chuva",
        WeatherCondition.Snow => "Neve",
        WeatherCondition.Thunderstorm => "Tempestade",
        _ => string.Empty,
    };

    /// <summary>
    /// Símbolos de clima (Segoe UI Emoji/Symbol, monocromáticos no WPF). As fontes de ícones do Windows 10
    /// não têm ícones de clima; os templates usam <c>WeatherGlyphFont</c>.
    /// </summary>
    public static string GlyphOf(WeatherCondition condition, bool isDay) => condition switch
    {
        WeatherCondition.Clear => isDay ? "☀" : "☾",
        WeatherCondition.PartlyCloudy => isDay ? "⛅" : "☁",
        WeatherCondition.Cloudy => "☁",
        WeatherCondition.Fog => "🌫",
        WeatherCondition.Drizzle or WeatherCondition.Rain => "🌧",
        WeatherCondition.Snow => "❄",
        WeatherCondition.Thunderstorm => "⛈",
        _ => "☁",
    };
}
