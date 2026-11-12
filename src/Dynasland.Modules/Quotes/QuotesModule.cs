using Dynasland.Core.Activities;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Modules;
using Dynasland.Modules.Common;
using Microsoft.Extensions.Logging;

namespace Dynasland.Modules.Quotes;

/// <summary>Dólar, euro e bitcoin em reais. Os valores só são buscados com o painel aberto.</summary>
public sealed class QuotesModule(
    IQuotesProvider provider,
    IEventBus bus,
    IUiDispatcher ui,
    TimeProvider time,
    IShortcutRegistry shortcuts,
    ILogger<QuotesModule> logger) : IIslandModule
{
    public const string ModuleId = "quotes";
    public const string PanelActivityId = "quotes.panel";

    private IDisposable? _registration;
    private QuotesContent? _content;

    public string Id => ModuleId;

    public string DisplayName => "Cotações";

    public string Description => "Dólar, euro e bitcoin em reais. Os valores vêm da AwesomeAPI e só são buscados com o painel aberto.";

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _content = new QuotesContent(RefreshAsync);
        _registration = shortcuts.Register(new IslandShortcut("quotes.panel", ModuleId, VectorIcons.Trend, "Cotações", OpenPanel));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _registration?.Dispose();
        _registration = null;
        _content = null;
        return Task.CompletedTask;
    }

    public void OpenPanel()
    {
        if (_content is not { } content)
        {
            return;
        }

        bus.PostActivity(new IslandActivity(PanelActivityId, ModuleId, ActivityKind.Transient, content) { OpenInteractive = true });
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_content is not { } content)
        {
            return;
        }

        try
        {
            var quotes = await provider.GetQuotesAsync();
            var now = time.GetLocalNow();
            ui.Post(() => content.SetQuotes(quotes, now));
        }
        catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            logger.LogWarning(ex, "Cotações indisponíveis.");
            ui.Post(() => content.SetError("Sem conexão"));
        }
    }
}
