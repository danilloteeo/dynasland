#if DEBUG
using System.Windows.Media;
using Dynasland.Core.Activities;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Modules.Media;
using Dynasland.Modules.Notifications;
using Dynasland.Modules.Timer;
using Dynasland.Modules.Volume;
using Dynasland.Modules.Water;
using Microsoft.Extensions.DependencyInjection;

namespace Dynasland.App.Services;

/// <summary>
/// Só na versão de depuração: <c>--demo=agua|volume|notificacao|musica|atividade</c> posta um estado da ilha
/// com dados de exemplo, para conferir o visual sem depender de eventos reais.
/// </summary>
public static class DemoMode
{
    public static void Run(IServiceProvider services, string name)
    {
        try
        {
            var bus = services.GetRequiredService<IEventBus>();
            var activity = name.ToLowerInvariant() switch
            {
                "agua" or "água" => Water(),
                "volume" => Volume(services),
                "notificacao" or "notificação" => Notification(),
                "musica" or "música" => Music(services),
                "atividade" => Activity(services),
                "cotacoes" or "cotações" => OpenModulePanel<Dynasland.Modules.Quotes.QuotesModule>(services),
                "placar" => OpenModulePanel<Dynasland.Modules.Scores.ScoresModule>(services),
                _ => null,
            };

            if (activity is not null)
            {
                bus.Publish(new ActivityPostedEvent(activity));
            }
        }
        catch (Exception ex)
        {
            System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dynasland-demo.log"), $"{DateTime.Now:T} {name}: {ex}\n");
        }
    }

    /// <summary>Abre o painel de um módulo real (com dados da internet) e não posta nada além disso.</summary>
    private static IslandActivity? OpenModulePanel<TModule>(IServiceProvider services)
        where TModule : class
    {
        var module = services.GetServices<Dynasland.Core.Modules.IIslandModule>().OfType<TModule>().FirstOrDefault();
        switch (module)
        {
            case Dynasland.Modules.Quotes.QuotesModule quotes:
                quotes.OpenPanel();
                break;
            case Dynasland.Modules.Scores.ScoresModule scores:
                scores.OpenPanel();
                break;
        }

        return null;
    }

    private static IslandActivity Water()
    {
        var content = new WaterBodyPanelContent(() => { }, () => { }, _ => { });
        content.Apply(5, 8);
        return new IslandActivity("demo-water", "water", ActivityKind.Transient, content) { OpenInteractive = true, Duration = TimeSpan.FromMinutes(10) };
    }

    private static IslandActivity Volume(IServiceProvider services)
    {
        var content = new VolumeContent(
            services.GetRequiredService<IAudioVolumeService>(),
            services.GetService<IAudioDeviceService>(),
            services.GetService<IAppVolumeService>());
        return new IslandActivity("demo-volume", "volume", ActivityKind.Transient, content) { OpenInteractive = true, Duration = TimeSpan.FromMinutes(10) };
    }

    private static IslandActivity Notification()
    {
        var info = new NotificationInfo(1, "demo", "Mensagens", "Mensagem nova", "Chegamos em 10 minutos, pode descer?", DateTimeOffset.Now, null);
        var content = new NotificationContent(
            info, null, false, Color.FromRgb(0x1F, 0x9D, 0x55), TimeSpan.FromMinutes(10), true, _ => { }, _ => { }, _ => { });
        return new IslandActivity("demo-notification", "notifications", ActivityKind.Transient, content) { Duration = TimeSpan.FromMinutes(10) };
    }

    private static MediaContent MusicContent(IServiceProvider services)
    {
        var media = new DemoMedia();
        var content = new MediaContent("demo", media, services.GetRequiredService<IUiDispatcher>(), TimeProvider.System, () => true);
        content.Update(media.Sessions[0], null);
        return content;
    }

    private static IslandActivity Music(IServiceProvider services) =>
        new("demo-media", "media", ActivityKind.Transient, MusicContent(services)) { OpenInteractive = true, Duration = TimeSpan.FromMinutes(10) };

    /// <summary>Atividade compacta: a pílula sem a dock (o módulo Timer pode estar desligado, então uso a música).</summary>
    private static IslandActivity Activity(IServiceProvider services) =>
        new("demo-media-live", "media", ActivityKind.Live, MusicContent(services));

    private sealed class DemoMedia : IMediaSessionService
    {
        public IReadOnlyList<MediaSnapshot> Sessions { get; } =
        [
            new MediaSnapshot("demo", "Música", "Nome da faixa", "Artista", "Álbum", null, MediaPlaybackState.Playing,
                TimeSpan.FromSeconds(72), TimeSpan.FromSeconds(208), DateTimeOffset.Now, true, true, true, true) { SessionId = "demo" },
        ];

        public string? CurrentSessionId => "demo";

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync() => Task.CompletedTask;

        public Task<bool> PlayAsync(string sessionId) => Task.FromResult(true);

        public Task<bool> PauseAsync(string sessionId) => Task.FromResult(true);

        public Task<bool> SkipNextAsync(string sessionId) => Task.FromResult(true);

        public Task<bool> SkipPreviousAsync(string sessionId) => Task.FromResult(true);
    }
}
#endif
