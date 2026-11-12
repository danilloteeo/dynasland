using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.Core.Activities;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Layout;
using Dynasland.Core.Models;

namespace Dynasland.Modules.Life;

/// <summary>Ícone vetorial do clima (desenhado em <c>LifeTemplates.xaml</c>, chave <c>WeatherIcon</c>).</summary>
public enum WeatherIconKind
{
    Sun,
    Moon,
    PartlyCloudyDay,
    PartlyCloudyNight,
    Cloud,
    Fog,
    Drizzle,
    Rain,
    Snow,
    Storm,
}

/// <param name="RainText">Chance de chuva ("60%"), quando houver.</param>
public sealed record ForecastDayViewModel(string DayName, WeatherIconKind Icon, string MaxText, string MinText, string? RainText)
{
    public bool HasRain => RainText is not null;
}

/// <summary>Painel do clima: condição atual, máxima/mínima, sensação, umidade, vento e os próximos dias.</summary>
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
    private WeatherIconKind _icon = WeatherIconKind.Cloud;

    [ObservableProperty]
    private string _todayRangeText = string.Empty;

    [ObservableProperty]
    private string _feelsLikeText = "--";

    [ObservableProperty]
    private string _humidityText = "--";

    [ObservableProperty]
    private string _windText = "--";

    [ObservableProperty]
    private string _rainText = "--";

    [ObservableProperty]
    private string _updatedText = string.Empty;

    [ObservableProperty]
    private bool _isStale;

    [ObservableProperty]
    private bool _hasReport;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private StatusContent? _status;

    public bool HasStatus => Status is not null;

    public void Apply(WeatherReport report, DateTimeOffset now, TimeSpan staleAfter)
    {
        var current = report.Current;
        LocationText = report.Location.Name;
        TemperatureText = WeatherText.Temperature(current.TemperatureC);
        ConditionText = WeatherText.ConditionName(current.Condition);
        Icon = WeatherText.IconOf(current.Condition, current.IsDay);
        FeelsLikeText = WeatherText.Temperature(current.ApparentTemperatureC);
        HumidityText = $"{current.HumidityPercent}%";
        WindText = $"{current.WindKmh:0} km/h";

        var today = report.Days.FirstOrDefault();
        TodayRangeText = today is null ? string.Empty : $"Máx {Math.Round(today.MaxC)}°  Mín {Math.Round(today.MinC)}°";
        RainText = today?.PrecipitationChancePercent is { } chance ? $"{chance}%" : "--";

        // Nunca apresentar dado velho como atual: mostra a hora da consulta e marca como desatualizado.
        IsStale = now - report.FetchedAt > staleAfter;
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
                WeatherText.IconOf(day.Condition, true),
                $"{Math.Round(day.MaxC)}°",
                $"{Math.Round(day.MinC)}°",
                day.PrecipitationChancePercent is >= 20 and var dayChance ? $"{dayChance}%" : null));
        }

        HasReport = true;
        Status = null;
    }

    public void ShowStatus(StatusContent status) => Status = status;

    public IslandDimensions? GetPreferredDimensions(IslandState state) => state switch
    {
        IslandState.Interactive => new IslandDimensions(440, HasReport ? 252 : 120, 36),
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

    public static WeatherIconKind IconOf(WeatherCondition condition, bool isDay) => condition switch
    {
        WeatherCondition.Clear => isDay ? WeatherIconKind.Sun : WeatherIconKind.Moon,
        WeatherCondition.PartlyCloudy => isDay ? WeatherIconKind.PartlyCloudyDay : WeatherIconKind.PartlyCloudyNight,
        WeatherCondition.Fog => WeatherIconKind.Fog,
        WeatherCondition.Drizzle => WeatherIconKind.Drizzle,
        WeatherCondition.Rain => WeatherIconKind.Rain,
        WeatherCondition.Snow => WeatherIconKind.Snow,
        WeatherCondition.Thunderstorm => WeatherIconKind.Storm,
        _ => WeatherIconKind.Cloud,
    };
}
