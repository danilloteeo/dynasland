using System.Text.Json;
using Dynasland.Infrastructure.Spotify;

namespace Dynasland.Tests.Infrastructure;

public class SpotifyServiceTests
{
    [Fact]
    public void Callback_query_is_parsed()
    {
        var query = SpotifyService.ParseQuery("/callback?code=AQB%2Fx-1&state=abc_123");

        Assert.Equal("AQB/x-1", query["code"]);
        Assert.Equal("abc_123", query["state"]);
        Assert.Empty(SpotifyService.ParseQuery("/callback"));
        Assert.Equal("access_denied", SpotifyService.ParseQuery("/callback?error=access_denied")["error"]);
    }

    [Fact]
    public void Queue_response_maps_tracks_and_podcast_episodes()
    {
        const string json = """
            {"currently_playing":{"name":"Atual","duration_ms":200000,"artists":[{"name":"A"},{"name":"B"}],
                                  "album":{"images":[{"url":"grande","width":640},{"url":"pequena","width":64}]}},
             "queue":[{"name":"Próxima","duration_ms":180000,"artists":[{"name":"C"}],"album":{"images":[{"url":"x","width":300}]}},
                      {"name":"Episódio","duration_ms":3600000,"images":[{"url":"ep","width":64}],"show":{"name":"Podcast","images":[]}}]}
            """;
        var response = JsonSerializer.Deserialize<SpotifyService.QueueResponse>(json)!;

        var current = SpotifyService.Map(response.CurrentlyPlaying)!;
        Assert.Equal("A, B", current.Artists);
        Assert.Equal("pequena", current.ImageUrl);
        Assert.Equal(TimeSpan.FromSeconds(200), current.Duration);

        var episode = SpotifyService.Map(response.Queue![1])!;
        Assert.Equal("Podcast", episode.Artists);
        Assert.Equal("ep", episode.ImageUrl);
        Assert.Null(SpotifyService.Map(null));
    }
}
