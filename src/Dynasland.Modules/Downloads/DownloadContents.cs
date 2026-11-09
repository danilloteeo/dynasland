using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.Core.Activities;
using Dynasland.Core.Layout;
using Dynasland.Core.Models;
using Dynasland.Modules.Common;

namespace Dynasland.Modules.Downloads;

public sealed partial class DownloadItemViewModel(string key) : ObservableObject
{
    public string Key { get; } = key;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _detail = string.Empty;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isIndeterminate = true;

    public void Update(DownloadInfo info)
    {
        FileName = info.FileName;
        Detail = DescribeProgress(info);
        IsIndeterminate = info.Progress is null;
        Progress = info.Progress ?? 0;
    }

    /// <summary>Mostra só o que o provedor realmente sabe (sem total inventado).</summary>
    public static string DescribeProgress(DownloadInfo info)
    {
        var parts = new List<string>
        {
            info.TotalBytes is { } total
                ? $"{TextFormat.Bytes(info.ReceivedBytes)} de {TextFormat.Bytes(total)}"
                : TextFormat.Bytes(info.ReceivedBytes),
        };

        if (info.BytesPerSecond is { } speed)
        {
            parts.Add($"{TextFormat.Bytes(speed)}/s");
        }

        if (info.EstimatedRemaining is { } remaining)
        {
            parts.Add($"{TextFormat.Duration(remaining)} restantes");
        }

        return string.Join(" · ", parts);
    }
}

/// <summary>Downloads em andamento (atividade contínua).</summary>
public sealed partial class DownloadsContent : ObservableObject, IActivityContent
{
    public ObservableCollection<DownloadItemViewModel> Items { get; } = [];

    [ObservableProperty]
    private string _headline = string.Empty;

    [ObservableProperty]
    private string _summary = string.Empty;

    public string Glyph => Glyphs.Download;

    public void Upsert(string key, DownloadInfo info)
    {
        var item = Items.FirstOrDefault(i => i.Key == key);
        if (item is null)
        {
            item = new DownloadItemViewModel(key);
            Items.Insert(0, item);
        }

        item.Update(info);
        RefreshSummary(info);
    }

    public void Remove(string key)
    {
        if (Items.FirstOrDefault(i => i.Key == key) is { } item)
        {
            Items.Remove(item);
        }

        RefreshSummary(null);
    }

    private void RefreshSummary(DownloadInfo? latest)
    {
        Headline = Items.Count switch
        {
            0 => string.Empty,
            1 => Items[0].FileName,
            _ => $"{Items.Count} downloads em andamento",
        };

        // No compacto: velocidade (quando conhecida) do download mais recente, ou a quantidade.
        Summary = latest?.BytesPerSecond is { } speed && Items.Count == 1
            ? $"{TextFormat.Bytes(speed)}/s"
            : Items.Count == 1 ? TextFormat.Bytes(latest?.ReceivedBytes ?? 0) : $"{Items.Count}";
    }

    public IslandDimensions? GetPreferredDimensions(IslandState state) => state switch
    {
        IslandState.Compact => new IslandDimensions(236, 34, 17),
        IslandState.Expanded => new IslandDimensions(360, 64, 30),
        IslandState.Interactive => new IslandDimensions(410, 52 + 46 * Math.Clamp(Items.Count, 1, 3), 34),
        _ => null,
    };
}

/// <summary>Download concluído, com ações sobre o arquivo.</summary>
public sealed partial class DownloadCompletedContent(DownloadInfo info, Action open, Action reveal) : IActivityContent
{
    public string FileName => info.FileName;

    public string Detail => $"Concluído · {TextFormat.Bytes(info.ReceivedBytes)}";

    public string Glyph => Glyphs.Accept;

    public IslandDimensions? GetPreferredDimensions(IslandState state) => state switch
    {
        IslandState.Expanded => new IslandDimensions(360, 64, 30),
        IslandState.Interactive => new IslandDimensions(380, 112, 32),
        _ => null,
    };

    [RelayCommand]
    private void Open() => open();

    [RelayCommand]
    private void Reveal() => reveal();
}
