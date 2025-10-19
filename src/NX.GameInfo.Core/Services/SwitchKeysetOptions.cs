using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace NX.GameInfo.Core.Services;

/// <summary>
/// Options that control how key files and auxiliary data (version lists, logs) are discovered.
/// </summary>
public sealed class SwitchKeysetOptions
{
    /// <summary>
    /// Optional explicit directory that contains the key files. When not supplied the service
    /// will probe <see cref="ApplicationDirectory"/> then <see cref="UserProfileDirectory"/>.
    /// </summary>
    public string? KeysDirectory { get; set; }

    /// <summary>
    /// Directory that contains the executing application. Defaults to <see cref="AppContext.BaseDirectory"/>.
    /// </summary>
    public string ApplicationDirectory { get; set; } = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    /// <summary>
    /// Directory used to store per-user key material. Defaults to <c>$HOME/.switch</c>.
    /// </summary>
    public string UserProfileDirectory { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch");

    /// <summary>
    /// When true the service will emit verbose diagnostic logging about file discovery as it runs.
    /// </summary>
    public bool EnableDebugLogging { get; set; }

    /// <summary>
    /// Log file name that will be created inside the effective key directory when <see cref="EnableDebugLogging"/> is true.
    /// </summary>
    public string LogFileName { get; init; } = "debug.log";

    /// <summary>
    /// Name of the prod key file.
    /// </summary>
    public string ProdKeysFileName { get; init; } = "prod.keys";

    /// <summary>
    /// Name of the title key file.
    /// </summary>
    public string TitleKeysFileName { get; init; } = "title.keys";

    /// <summary>
    /// Name of the console key file.
    /// </summary>
    public string ConsoleKeysFileName { get; init; } = "console.keys";

    /// <summary>
    /// Name of the cached HAC version list file.
    /// </summary>
    public string VersionListFileName { get; init; } = "hac_versionlist.json";

    /// <summary>
    /// URI used to fetch latest title keys when requested.
    /// </summary>
    public Uri TitleKeysUri { get; init; } = new("https://gist.githubusercontent.com/gneurshkgau/81bcaa7064bd8f98d7dffd1a1f1781a7/raw/title.keys");

    /// <summary>
    /// URI used to fetch the latest version list from blawar/titledb when requested.
    /// Format: Dictionary of title IDs to version maps (e.g., {"titleId": {"65536": "2020-01-01", ...}})
    /// </summary>
    public Uri VersionListUri { get; init; } = new("https://raw.githubusercontent.com/blawar/titledb/refs/heads/master/versions.json");

    /// <summary>
    /// Attempts to resolve the directory order based on current settings.
    /// </summary>
    public IEnumerable<string> EnumerateCandidateDirectories()
    {
        if (!string.IsNullOrWhiteSpace(KeysDirectory))
        {
            yield return KeysDirectory!;
            yield break;
        }

        if (!string.IsNullOrWhiteSpace(ApplicationDirectory))
        {
            yield return ApplicationDirectory!;
        }

        if (!string.IsNullOrWhiteSpace(UserProfileDirectory))
        {
            yield return UserProfileDirectory!;
        }
    }

    public bool TryResolveKeysDirectory([NotNullWhen(true)] out string? directory)
    {
        foreach (var candidate in EnumerateCandidateDirectories())
        {
            if (!string.IsNullOrWhiteSpace(candidate) &&
                File.Exists(Path.Combine(candidate, ProdKeysFileName)))
            {
                directory = candidate;
                return true;
            }
        }

        directory = EnumerateCandidateDirectories().FirstOrDefault();
        return directory is not null;
    }
}
