namespace NX.GameInfo.Cli;

/// <summary>
/// Represents persisted preferences for the CLI experience.
/// </summary>
public sealed class CliSettingsOptions
{
    /// <summary>
    /// CSV delimiter persisted as a single-character string for easy configuration binding.
    /// </summary>
    public string CsvSeparator { get; set; } = ",";

    /// <summary>
    /// Allows processing compressed NSP/XCI containers by default.
    /// </summary>
    public bool AllowCompressed { get; set; }

    /// <summary>
    /// Enables verbose logging and legacy debug.log output.
    /// </summary>
    public bool DebugLog { get; set; }

    /// <summary>
    /// Default sort column used when scanning multiple entries.
    /// </summary>
    public string DefaultSort { get; set; } = "filename";

    public char GetSeparatorOrDefault()
    {
        if (string.IsNullOrEmpty(CsvSeparator))
        {
            return ',';
        }

        return CsvSeparator[0];
    }
}
