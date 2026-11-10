using CommunityToolkit.Mvvm.ComponentModel;
using Dynasland.Core.Interfaces;

namespace Dynasland.App.ViewModels.Hub;

/// <summary>Página da Central: carregada ao ser aberta e liberada ao fechar a janela.</summary>
public interface IHubPage : IDisposable
{
    HubPage Page { get; }

    Task LoadAsync();
}

/// <summary>Central: históricos (área de transferência, notificações, foco, capturas) e agenda.</summary>
public sealed partial class HubViewModel : ObservableObject, IDisposable
{
    private readonly Dictionary<HubPage, IHubPage> _pages;

    [ObservableProperty]
    private HubPage _section;

    [ObservableProperty]
    private IHubPage? _current;

    public HubViewModel(IEnumerable<IHubPage> pages)
    {
        _pages = pages.ToDictionary(p => p.Page);
    }

    public bool Has(HubPage page) => _pages.ContainsKey(page);

    public void Navigate(HubPage page)
    {
        var target = _pages.ContainsKey(page) ? page : _pages.Keys.First();
        if (Section == target)
        {
            // Mesma seção (ex.: a primeira, valor padrão): a mudança de propriedade não dispara.
            Activate(target);
        }
        else
        {
            Section = target;
        }
    }

    partial void OnSectionChanged(HubPage value) => Activate(value);

    private void Activate(HubPage value)
    {
        if (_pages.TryGetValue(value, out var page))
        {
            Current = page;
            _ = page.LoadAsync();
        }
    }

    public void Dispose()
    {
        foreach (var page in _pages.Values)
        {
            page.Dispose();
        }
    }
}
