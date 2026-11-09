using Dynasland.Core.Models;
using Dynasland.Infrastructure.Windows.Downloads;
using Dynasland.Infrastructure.Windows.Metrics;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dynasland.Tests.Infrastructure;

public class Phase4IntegrationTests
{
    [Fact]
    public void System_metrics_read_real_cpu_memory_and_disks()
    {
        using var service = new WindowsSystemMetricsService();

        var first = service.Sample();
        Thread.Sleep(300);
        var second = service.Sample();

        Assert.Null(first.CpuPercent); // precisa de duas leituras
        Assert.InRange(second.CpuPercent!.Value, 0, 100);
        Assert.True(second.Memory.TotalBytes > 0 && second.Memory.UsedBytes <= second.Memory.TotalBytes);
        Assert.Contains(second.Disks, d => d.IsSystemDrive && d.TotalBytes > 0);
        if (second.GpuPercent is { } gpu)
        {
            Assert.InRange(gpu, 0, 100);
        }

        if (second.CpuTemperatureCelsius is { } temperature)
        {
            Assert.InRange(temperature, 1, 124);
        }
    }

    /// <summary>Reproduz no sistema de arquivos real o que Chrome/Edge/Opera fazem ao baixar um arquivo.</summary>
    [Fact]
    public async Task Folder_provider_follows_chromium_download_until_completion()
    {
        var folder = Directory.CreateTempSubdirectory("dynasland-downloads-").FullName;
        var events = new List<DownloadInfo>();
        using var provider = new BrowserDownloadFolderProvider(folder, TimeProvider.System, NullLogger<BrowserDownloadFolderProvider>.Instance);
        provider.Changed += (_, e) => { lock (events) { events.Add(e); } };
        try
        {
            Assert.True(await provider.StartAsync());

            var unconfirmed = Path.Combine(folder, "Unconfirmed 481516.crdownload");
            await File.WriteAllBytesAsync(unconfirmed, new byte[64 * 1024]);
            await WaitForAsync(events, e => e.State == DownloadState.InProgress);

            var named = Path.Combine(folder, "relatorio.pdf.crdownload");
            File.Move(unconfirmed, named);
            await using (var stream = new FileStream(named, FileMode.Append))
            {
                await stream.WriteAsync(new byte[256 * 1024]);
            }

            await WaitForAsync(events, e => e.FileName == "relatorio.pdf" && e.ReceivedBytes == 320 * 1024);

            File.Move(named, Path.Combine(folder, "relatorio.pdf"));
            var completed = await WaitForAsync(events, e => e.State == DownloadState.Completed);

            Assert.Equal("relatorio.pdf", completed.FileName);
            Assert.Equal(320 * 1024, completed.ReceivedBytes);
            lock (events)
            {
                Assert.Single(events.Select(e => e.Id).Distinct());
                Assert.Equal("Novo download", events[0].FileName);
            }
        }
        finally
        {
            await provider.StopAsync();
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task Folder_provider_reports_cancellation_when_temporary_file_disappears()
    {
        var folder = Directory.CreateTempSubdirectory("dynasland-downloads-").FullName;
        var events = new List<DownloadInfo>();
        using var provider = new BrowserDownloadFolderProvider(folder, TimeProvider.System, NullLogger<BrowserDownloadFolderProvider>.Instance);
        provider.Changed += (_, e) => { lock (events) { events.Add(e); } };
        try
        {
            await provider.StartAsync();
            var temporary = Path.Combine(folder, "video.mp4.part");
            await File.WriteAllBytesAsync(temporary, new byte[1024]);
            await WaitForAsync(events, e => e.State == DownloadState.InProgress);

            File.Delete(temporary);

            var cancelled = await WaitForAsync(events, e => e.State == DownloadState.Cancelled);
            Assert.Equal("video.mp4", cancelled.FileName);
        }
        finally
        {
            await provider.StopAsync();
            Directory.Delete(folder, recursive: true);
        }
    }

    private static async Task<DownloadInfo> WaitForAsync(List<DownloadInfo> events, Func<DownloadInfo, bool> predicate)
    {
        for (var i = 0; i < 100; i++)
        {
            lock (events)
            {
                if (events.FirstOrDefault(predicate) is { } match)
                {
                    return match;
                }
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("Evento de download esperado não chegou.");
    }
}
