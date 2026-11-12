using Dynasland.Core.Models;
using Dynasland.Infrastructure.Windows.Privacy;
using Dynasland.Tests.Modules;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Dynasland.Tests.Infrastructure;

public class PrivacyIndicatorServiceTests(ITestOutputHelper output)
{
    [Fact]
    public void Reading_the_consent_store_never_throws()
    {
        // Só leitura do HKCU: neste computador pode haver qualquer coisa (ou nada) registrada.
        var entries = ConsentStoreReader.Read();

        foreach (var entry in entries.Where(e => e.Start > 0))
        {
            output.WriteLine($"{entry.Capability} {entry.Key} start={entry.Start} stop={entry.Stop}");
        }

        Assert.All(entries, e => Assert.False(string.IsNullOrEmpty(e.Key)));
    }

    [Fact]
    public async Task Disabled_setting_keeps_indicators_off()
    {
        var settings = new FakeIslandSettings();
        await settings.UpdateAsync(s => s with { ShowPrivacyIndicators = false });

        using var service = new RegistryPrivacyIndicatorService(settings, NullLogger<RegistryPrivacyIndicatorService>.Instance);

        Assert.Equal(PrivacyUsage.None, service.Current);
    }

    [Fact]
    public async Task Turning_the_setting_off_clears_the_indicators()
    {
        var settings = new FakeIslandSettings();
        using var service = new RegistryPrivacyIndicatorService(settings, NullLogger<RegistryPrivacyIndicatorService>.Instance);

        await settings.UpdateAsync(s => s with { ShowPrivacyIndicators = false });

        Assert.Equal(PrivacyUsage.None, service.Current);
    }
}
