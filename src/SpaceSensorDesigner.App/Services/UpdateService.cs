using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace SpaceSensorDesigner.App.Services;

public enum UpdateStatus { NotConfigured, UpToDate, UpdateAvailable, Error }

public sealed record UpdateCheckResult(UpdateStatus Status, string CurrentVersion, string? LatestVersion, string? DownloadUrl, string? Message)
{
    public static UpdateCheckResult NotConfigured(string current) =>
        new(UpdateStatus.NotConfigured, current, null, null,
            "Auto-update is not configured. Set an update feed URL in Settings to enable it.");
}

/// <summary>
/// Checks a JSON version manifest for a newer build. The manifest is a small document of the form
/// <c>{ "version": "1.2.0", "url": "https://.../SpaceSensorDesigner-Setup.exe" }</c>.
/// There is no bundled server — the feed URL is user-supplied in Settings, so an empty URL yields
/// <see cref="UpdateStatus.NotConfigured"/> rather than a fake result.
/// </summary>
public sealed class UpdateService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public string CurrentVersion => AppInfo.Version;

    public async Task<UpdateCheckResult> CheckAsync(string? feedUrl)
    {
        if (string.IsNullOrWhiteSpace(feedUrl))
            return UpdateCheckResult.NotConfigured(CurrentVersion);

        try
        {
            var json = await Http.GetStringAsync(feedUrl).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var latest = root.TryGetProperty("version", out var v) ? v.GetString() : null;
            var url = root.TryGetProperty("url", out var u) ? u.GetString() : null;

            if (string.IsNullOrWhiteSpace(latest))
                return new UpdateCheckResult(UpdateStatus.Error, CurrentVersion, null, null,
                    "The update feed did not contain a 'version' field.");

            bool newer = CompareVersions(latest!, CurrentVersion) > 0;
            return newer
                ? new UpdateCheckResult(UpdateStatus.UpdateAvailable, CurrentVersion, latest, url,
                    $"Version {latest} is available (you have {CurrentVersion}).")
                : new UpdateCheckResult(UpdateStatus.UpToDate, CurrentVersion, latest, null,
                    $"You are on the latest version ({CurrentVersion}).");
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(UpdateStatus.Error, CurrentVersion, null, null,
                $"Update check failed: {ex.Message}");
        }
    }

    /// <summary>Compares dotted numeric versions. Returns &gt;0 if <paramref name="a"/> is newer.</summary>
    internal static int CompareVersions(string a, string b)
    {
        var pa = a.Split('.');
        var pb = b.Split('.');
        int n = Math.Max(pa.Length, pb.Length);
        for (int i = 0; i < n; i++)
        {
            int ia = i < pa.Length && int.TryParse(pa[i], out var x) ? x : 0;
            int ib = i < pb.Length && int.TryParse(pb[i], out var y) ? y : 0;
            if (ia != ib) return ia.CompareTo(ib);
        }
        return 0;
    }
}
