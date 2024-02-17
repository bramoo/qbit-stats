using System.Diagnostics.Metrics;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prometheus;

public class QbitStats(ILogger<QbitStats> logger, IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var qbitConfig = configuration.GetRequiredSection("qbittorrent").Get<QbittorrentConfig>() ?? throw new ArgumentException("Invalid qBittorrent config");
        var metricConfig = configuration.GetRequiredSection("metrics").Get<MetricConfig>() ?? throw new ArgumentException("Invalid metrics configuration");

        HttpClient client = new()
        {
            BaseAddress = new Uri(qbitConfig.WebUI, "/api/v2/")
        };

        Metrics.SuppressDefaultMetrics(new SuppressDefaultMetricOptions
        {
            SuppressDebugMetrics = true,
            SuppressEventCounters = true,
            SuppressProcessMetrics = true
        });

        Meter qbitMeter = new(metricConfig.MeterName);
        IList<TorrentInfo> torrents = [];
        DateTime lastFetch = DateTime.MinValue;
        TimeSpan minRefreshTime = TimeSpan.FromSeconds(metricConfig.MinRefreshSeconds);

        qbitMeter.CreateObservableCounter("uploaded", FetchAndReportUploadCounts);

        IEnumerable<Measurement<long>> FetchAndReportUploadCounts()
        {
            var now = DateTime.Now;
            if (now - lastFetch >= minRefreshTime)
            {
                var authResponse = client.PostAsync("auth/login", new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    {"username", qbitConfig.Username},
                    {"password", qbitConfig.Password}
                })).Result;

                var authMessage = authResponse.Content.ReadAsStringAsync().Result;
                if (authMessage != "Ok.")
                {
                    logger.LogError("Failed to authenticate with qbittorrent");
                    yield break;
                }

                torrents = client.GetFromJsonAsync<List<TorrentInfo>>("torrents/info").Result ?? [];
                logger.LogInformation($"Got {torrents.Count} torrent infos");
                lastFetch = now;
            }
            else
            {
                logger.LogInformation("reusing previous fetch");
            }

            foreach (var info in torrents)
            {
                yield return new(info.Uploaded, new("hash", info.Hash), new("name", info.Name), new("tracker", info.Tracker));
            }
        }

        using var server = new KestrelMetricServer(metricConfig.Port, metricConfig.Path);
        server.Start();

        logger.LogInformation($"Serving metrics at http://localhost:{metricConfig.Port}{metricConfig.Path}");
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}