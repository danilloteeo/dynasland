using Dynasland.Infrastructure.Windows.Gaming;

namespace Dynasland.Tests.Infrastructure;

public class GameDetectorTests
{
    [Theory]
    [InlineData("eldenring", @"D:\SteamLibrary\steamapps\common\ELDEN RING\Game\eldenring.exe", false, true)]
    [InlineData("FortniteClient-Win64-Shipping", @"C:\Program Files\Epic Games\Fortnite\FortniteClient-Win64-Shipping.exe", false, true)]
    [InlineData("jogo", @"C:\Jogos\jogo.exe", true, true)]
    [InlineData("jogo", @"C:\Jogos\jogo.exe", false, false)]
    [InlineData("chrome", @"C:\Program Files\Google\Chrome\Application\chrome.exe", true, false)]
    [InlineData("vlc", @"C:\Program Files\VideoLAN\VLC\vlc.exe", true, false)]
    [InlineData("steamwebhelper", @"C:\Program Files (x86)\Steam\steamapps\common\x\steamwebhelper.exe", true, false)]
    public void Decides_if_foreground_process_is_a_game(string name, string path, bool fullscreen, bool expected) =>
        Assert.Equal(expected, GameDetector.IsGame(name, path, fullscreen));
}
