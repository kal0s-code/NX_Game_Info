using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NX.GameInfo.Core.Models;

/// <summary>
/// Legacy tagaya version list format (array of title entries).
/// Preserved for backward compatibility with cached files or custom URLs.
/// </summary>
public sealed record VersionEntry(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("version")] uint Version,
    [property: JsonPropertyName("required_version")] uint RequiredVersion);

/// <summary>
/// Legacy tagaya version list document format.
/// Preserved for backward compatibility with cached files or custom URLs.
/// </summary>
public sealed class VersionListDocument
{
    [JsonPropertyName("titles")]
    public IReadOnlyList<VersionEntry> Titles { get; init; } = ImmutableArray<VersionEntry>.Empty;

    [JsonPropertyName("format_version")]
    public uint FormatVersion { get; init; }

    [JsonPropertyName("last_modified")]
    public uint LastModified { get; init; }
}

/// <summary>
/// blawar/titledb version list format (dictionary of title ID -> version map).
/// Each title ID maps to a dictionary of version numbers to release dates.
/// The latest version is the maximum key in each title's version map.
/// </summary>
public sealed class BlawarVersionListDocument
{
    /// <summary>
    /// Root object is a dictionary where:
    /// - Key: Title ID (lowercase hex, e.g., "01006f8002326800")
    /// - Value: JsonElement containing a dictionary of version numbers to release dates
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement> Titles { get; set; } = new();
}
