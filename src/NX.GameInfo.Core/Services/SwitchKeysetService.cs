using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using LibHac.Common.Keys;
using LibHac.Tools;
using Microsoft.Extensions.Logging;
using NX.GameInfo.Core.Models;
using NX.GameInfo.Core.Infrastructure;

namespace NX.GameInfo.Core.Services;

/// <summary>
/// Encapsulates discovery of Switch key material, cached version metadata, and refresh operations.
/// </summary>
public sealed class SwitchKeysetService : IDisposable
{
    private readonly ILogger<SwitchKeysetService> _logger;
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly HttpClient? _httpClient;
    private static readonly Regex TitleVersionRegex = new("v(?<ver>\\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private HttpClient HttpClient => _httpClientFactory?.CreateClient("nx-game-info") ?? _httpClient ?? new HttpClient();
    private readonly LibHacDiagnosticsLogger _diagnostics = new();

    public SwitchKeysetService(ILogger<SwitchKeysetService> logger, IHttpClientFactory? httpClientFactory = null, HttpClient? httpClient = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClientFactory = httpClientFactory;
        _httpClient = httpClient;
    }

    internal LibHacDiagnosticsLogger DiagnosticsLogger => _diagnostics;

    public async Task<SwitchKeysetContext> LoadAsync(SwitchKeysetOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.TryResolveKeysDirectory(out var directory))
        {
            throw new FileNotFoundException($"Unable to locate {options.ProdKeysFileName}. Supply SwitchKeysetOptions.KeysDirectory.");
        }

        Directory.CreateDirectory(directory);

        var prodKeysPath = Path.Combine(directory, options.ProdKeysFileName);
        if (!File.Exists(prodKeysPath))
        {
            throw new FileNotFoundException($"Missing required key file '{prodKeysPath}'.");
        }

        var titleKeysCandidate = Path.Combine(directory, options.TitleKeysFileName);
        var consoleKeysCandidate = Path.Combine(directory, options.ConsoleKeysFileName);
        var titleKeysPath = File.Exists(titleKeysCandidate) ? titleKeysCandidate : null;
        var consoleKeysPath = File.Exists(consoleKeysCandidate) ? consoleKeysCandidate : null;

        _logger.LogInformation("Reading key set from {Directory}", directory);
        var keySet = ExternalKeyReader.ReadKeyFile(prodKeysPath, titleKeysPath, consoleKeysPath, _diagnostics);
        var context = new SwitchKeysetContext(keySet, directory);

        context.ProdKeysPath = prodKeysPath;
        context.TitleKeysPath = titleKeysPath;
        context.ConsoleKeysPath = consoleKeysPath;
        context.VersionListPath = Path.Combine(directory, options.VersionListFileName);

        PopulateTitleMetadata(context);

        if (options.EnableDebugLogging)
        {
            var logPath = Path.Combine(directory, options.LogFileName);
            context.LogWriter = new StreamWriter(File.Open(logPath, FileMode.Append, FileAccess.Write, FileShare.Read))
            {
                AutoFlush = true
            };
            await LogAsync(context, $"--------------------------------------------------------------{Environment.NewLine}");
            await LogAsync(context, $"Application starts at {DateTime.UtcNow:F} (UTC){Environment.NewLine}");
        }

        await LoadVersionListAsync(context, options, cancellationToken).ConfigureAwait(false);
        RelayDiagnostics(context);
        return context;
    }

    public async Task<bool> RefreshTitleKeysAsync(SwitchKeysetContext context, SwitchKeysetOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        if (options.TitleKeysUri is null)
        {
            return false;
        }

        try
        {
            var client = HttpClient;
            using var response = await client.GetAsync(options.TitleKeysUri, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(payload))
            {
                return false;
            }

            var titleKeysPath = Path.Combine(context.KeysDirectory, options.TitleKeysFileName);
            await File.WriteAllTextAsync(titleKeysPath, payload, cancellationToken).ConfigureAwait(false);
            context.TitleKeysPath = titleKeysPath;

            await using var stream = File.OpenRead(titleKeysPath);
            ExternalKeyReader.ReadTitleKeys(context.Keyset, stream, _diagnostics);
            PopulateTitleMetadata(context);
            RelayDiagnostics(context);

            await LogAsync(context, $"Fetched {context.Keyset.ExternalKeySet.ToList().Count} title keys.{Environment.NewLine}");
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            _logger.LogWarning(ex, "Failed to refresh title keys from {Uri}", options.TitleKeysUri);
            await LogAsync(context, $"Failed to refresh title keys: {ex.Message}{Environment.NewLine}");
            return false;
        }
    }

    public async Task<bool> RefreshVersionListAsync(SwitchKeysetContext context, SwitchKeysetOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        if (options.VersionListUri is null)
        {
            return false;
        }

        try
        {
            var client = HttpClient;
            using var response = await client.GetAsync(options.VersionListUri, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            var document = JsonSerializer.Deserialize(json, VersionListSerializerContext.Default.BlawarVersionListDocument);
            if (document is null || document.Titles.Count == 0)
            {
                _logger.LogWarning("Failed to parse version list from {Uri}. Delete cached hac_versionlist.json and retry with -V flag.", options.VersionListUri);
                return false;
            }

            ApplyBlawarVersionList(context, document);

            var versionListPath = Path.Combine(context.KeysDirectory, options.VersionListFileName);
            await File.WriteAllTextAsync(versionListPath, json, cancellationToken).ConfigureAwait(false);
            context.VersionListPath = versionListPath;
            RelayDiagnostics(context);

            await LogAsync(context, $"Fetched version list containing {context.VersionList.Count} entries.{Environment.NewLine}");
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or JsonException)
        {
            _logger.LogWarning(ex, "Failed to refresh version list from {Uri}", options.VersionListUri);
            await LogAsync(context, $"Failed to refresh version list: {ex.Message}{Environment.NewLine}");
            return false;
        }
    }

    private async Task LoadVersionListAsync(SwitchKeysetContext context, SwitchKeysetOptions options, CancellationToken cancellationToken)
    {
        var versionListPath = context.VersionListPath;
        if (string.IsNullOrWhiteSpace(versionListPath))
        {
            versionListPath = Path.Combine(context.KeysDirectory, options.VersionListFileName);
        }

        if (!File.Exists(versionListPath))
        {
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(versionListPath, cancellationToken).ConfigureAwait(false);
            var document = JsonSerializer.Deserialize(json, VersionListSerializerContext.Default.BlawarVersionListDocument);
            if (document is null || document.Titles.Count == 0)
            {
                _logger.LogWarning("Failed to parse cached version list at {Path}. Delete the file and redownload with -V flag.", versionListPath);
                await LogAsync(context, $"Failed to parse cached version list. Delete {versionListPath} and retry with -V.{Environment.NewLine}");
                return;
            }

            ApplyBlawarVersionList(context, document);
            context.VersionListPath = versionListPath;
            await LogAsync(context, $"Loaded cached version list containing {context.VersionList.Count} entries.{Environment.NewLine}");
            RelayDiagnostics(context);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            _logger.LogWarning(ex, "Failed to read cached version list at {Path}. Delete the file and redownload with -V flag.", versionListPath);
            await LogAsync(context, $"Failed to read cached version list: {ex.Message}. Delete {versionListPath} and retry with -V.{Environment.NewLine}");
            RelayDiagnostics(context);
        }
    }

    private void PopulateTitleMetadata(SwitchKeysetContext context)
    {
        context.TitleNames.Clear();
        context.TitleVersions.Clear();

        if (context.TitleKeysPath is null || !File.Exists(context.TitleKeysPath))
        {
            return;
        }

        foreach (var (titleId, name, version) in ParseTitleKeys(context.TitleKeysPath))
        {
            if (!string.IsNullOrEmpty(name))
            {
                context.TitleNames[titleId] = name;
            }

            if (version.HasValue)
            {
                context.TitleVersions[titleId] = version.Value;
            }
        }
    }

    private void RelayDiagnostics(SwitchKeysetContext context)
    {
        _diagnostics.DrainTo(message =>
        {
            context.RecordDiagnostic(message);
            _logger.LogDebug("LibHac: {Message}", message);
        });
    }

    private static IEnumerable<(string TitleId, string? Name, uint? Version)> ParseTitleKeys(string path)
    {
        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            if (line.StartsWith("#", StringComparison.Ordinal) ||
                line.StartsWith("//", StringComparison.Ordinal) ||
                line.StartsWith(";", StringComparison.Ordinal))
            {
                continue;
            }

            var (index, length) = FindCommentSpan(line);
            string comment = string.Empty;

            if (index >= 0)
            {
                comment = line[(index + length)..].Trim();
                line = line[..index].Trim();
            }

            var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                continue;
            }

            var rightsId = parts[0];
            if (rightsId.Length < 16 || !IsHexString(rightsId))
            {
                continue;
            }

            var titleId = rightsId[..16].ToUpperInvariant();
            string? name = string.IsNullOrWhiteSpace(comment) ? null : comment;

            uint? version = null;
            if (!string.IsNullOrEmpty(name))
            {
                var match = TitleVersionRegex.Match(name);
                if (match.Success && uint.TryParse(match.Groups["ver"].Value, out var parsed))
                {
                    version = parsed;
                }
            }

            yield return (titleId, name, version);
        }
    }

    private static (int index, int length) FindCommentSpan(string text)
    {
        int slashIndex = text.IndexOf("//", StringComparison.Ordinal);
        int hashIndex = text.IndexOf('#');
        int semicolonIndex = text.IndexOf(';');

        int index = MinNonNegative(slashIndex, hashIndex, semicolonIndex);
        if (index == -1)
        {
            return (-1, 0);
        }

        int length = slashIndex == index ? 2 : 1;
        return (index, length);
    }

    private static int MinNonNegative(params int[] values)
    {
        int index = -1;
        foreach (var value in values)
        {
            if (value >= 0 && (index == -1 || value < index))
            {
                index = value;
            }
        }

        return index;
    }

    private static bool IsHexString(string value)
    {
        foreach (var ch in value)
        {
            if (!Uri.IsHexDigit(ch))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Applies blawar/titledb version list format to context.
    /// Format: { "titleId": { "version": "date", ... }, ... }
    /// The latest version is the maximum numeric key in each title's version map.
    /// </summary>
    private static void ApplyBlawarVersionList(SwitchKeysetContext context, BlawarVersionListDocument document)
    {
        context.VersionList.Clear();
        foreach (var (titleId, versionMapElement) in document.Titles)
        {
            if (versionMapElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            // Parse all version keys and find the maximum
            uint maxVersion = 0;
            foreach (var versionProperty in versionMapElement.EnumerateObject())
            {
                if (uint.TryParse(versionProperty.Name, out var version) && version > maxVersion)
                {
                    maxVersion = version;
                }
            }

            if (maxVersion > 0)
            {
                var normalized = NormalizeTitleId(titleId);
                if (!context.VersionList.TryGetValue(normalized, out var existing) || maxVersion > existing)
                {
                    context.VersionList[normalized] = maxVersion;
                }
            }
        }
    }

    private static string NormalizeTitleId(string titleId)
    {
        if (string.IsNullOrWhiteSpace(titleId))
        {
            return string.Empty;
        }

        var trimmed = titleId.Trim().ToUpperInvariant();
        if (trimmed.EndsWith("800", StringComparison.Ordinal))
        {
            return trimmed[..Math.Min(trimmed.Length, 13)] + "000";
        }

        return trimmed;
    }

    private static async Task LogAsync(SwitchKeysetContext context, string message)
    {
        if (context.LogWriter is null)
        {
            return;
        }

        await context.LogWriter.WriteAsync(message).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

}

/// <summary>
/// Source generated serializer context for the version list document improves startup and reduces allocations.
/// </summary>
[JsonSerializable(typeof(VersionListDocument))]
[JsonSerializable(typeof(BlawarVersionListDocument))]
internal partial class VersionListSerializerContext : JsonSerializerContext
{
}
