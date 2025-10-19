using System;
using System.Collections.Generic;
using System.Linq;
using LibHac.Common.Keys;

namespace NX.GameInfo.Core.Services;

/// <summary>
/// Represents the mutable state associated with a loaded Switch keyset and ancillary data.
/// </summary>
public sealed class SwitchKeysetContext
{
    public SwitchKeysetContext(KeySet keyset, string directory)
    {
        Keyset = keyset ?? throw new ArgumentNullException(nameof(keyset));
        KeysDirectory = directory ?? throw new ArgumentNullException(nameof(directory));
    }

    /// <summary>
    /// The LibHac keyset loaded from the user's key files.
    /// </summary>
    public KeySet Keyset { get; }

    /// <summary>
    /// Directory that contained the key files when the set was loaded.
    /// </summary>
    public string KeysDirectory { get; }

    public string ProdKeysPath { get; internal set; } = string.Empty;
    public string? TitleKeysPath { get; internal set; }
    public string? ConsoleKeysPath { get; internal set; }
    public string VersionListPath { get; internal set; } = string.Empty;

    /// <summary>
    /// Optional log writer used to mirror legacy debug.log behaviour. Callers should dispose this when finished.
    /// </summary>
    public StreamWriter? LogWriter { get; internal set; }

    /// <summary>
    /// Known title names indexed by title ID.
    /// </summary>
    public Dictionary<string, string> TitleNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Known title versions indexed by title ID.
    /// </summary>
    public Dictionary<string, uint> TitleVersions { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Latest version list aggregated from HAC metadata.
    /// </summary>
    public Dictionary<string, uint> VersionList { get; } = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<string> _diagnostics = new();
    private readonly object _diagnosticSync = new();

    /// <summary>
    /// Records a diagnostic message emitted by LibHac for later replay.
    /// </summary>
    public void RecordDiagnostic(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        lock (_diagnosticSync)
        {
            _diagnostics.Add(message);
        }

        LogWriter?.WriteLine(message);
    }

    /// <summary>
    /// Returns any accumulated diagnostics and clears the buffer.
    /// </summary>
    public IReadOnlyList<string> ConsumeDiagnostics()
    {
        lock (_diagnosticSync)
        {
            if (_diagnostics.Count == 0)
            {
                return Array.Empty<string>();
            }

            var copy = _diagnostics.ToArray();
            _diagnostics.Clear();
            return copy;
        }
    }

    /// <summary>
    /// Validates whether the keyset contains the minimum material required to decrypt SD card contents.
    /// </summary>
    /// <param name="missingKeys">
    /// When validation fails, contains the list of logical key names that were not populated.
    /// </param>
    public bool TryValidateSdCardKeys(out IReadOnlyList<string> missingKeys)
    {
        var missing = new List<string>();
        var keySet = Keyset;

        if (keySet.SdCardEncryptionSeed.IsZeros())
        {
            missing.Add("sd_seed");
        }

        if (keySet.SdCardKekSource.IsZeros())
        {
            missing.Add("sd_card_kek_source");
        }

        var sdCardKeySources = keySet.SdCardKeySources;
        if (sdCardKeySources.Length <= 1 || sdCardKeySources[1].IsZeros())
        {
            missing.Add("sd_card_nca_key_source");
        }

        missingKeys = missing;
        return missing.Count == 0;
    }
}
