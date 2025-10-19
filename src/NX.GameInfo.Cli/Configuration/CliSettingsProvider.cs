using System.Text.Json;
using Microsoft.Extensions.Configuration;
using NX.GameInfo.Core.Services;

namespace NX.GameInfo.Cli.Configuration;

/// <summary>
/// Loads and persists CLI preferences plus keyset discovery overrides.
/// </summary>
public sealed class CliSettingsProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _baseDirectory;

    public CliSettingsProvider(string? userSettingsPath = null, string? baseDirectory = null)
    {
        _baseDirectory = string.IsNullOrWhiteSpace(baseDirectory) ? AppContext.BaseDirectory : baseDirectory!;
        UserSettingsPath = string.IsNullOrWhiteSpace(userSettingsPath)
            ? Path.Combine(GetUserSettingsDirectory(), "cli-settings.json")
            : userSettingsPath;
    }

    public string UserSettingsPath { get; }

    public CliUserConfiguration Load()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(_baseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

        if (File.Exists(UserSettingsPath))
        {
            builder.AddJsonFile(UserSettingsPath, optional: true, reloadOnChange: false);
        }

        IConfigurationRoot configuration = builder.Build();
        var result = new CliUserConfiguration();
        configuration.Bind(result);

        result.CliSettings ??= new CliSettingsOptions();
        result.SwitchKeysetOptions ??= new SwitchKeysetOptions();

        return result;
    }

    public void Save(CliUserConfiguration configuration)
    {
        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(UserSettingsPath)!);
        string payload = JsonSerializer.Serialize(configuration, SerializerOptions);
        File.WriteAllText(UserSettingsPath, payload);
    }

    public bool Reset()
    {
        if (!File.Exists(UserSettingsPath))
        {
            return false;
        }

        File.Delete(UserSettingsPath);
        return true;
    }

    private static string GetUserSettingsDirectory()
    {
        string basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        if (string.IsNullOrWhiteSpace(basePath))
        {
            basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        }

        return Path.Combine(basePath, "NX_Game_Info");
    }
}

public sealed class CliUserConfiguration
{
    public CliSettingsOptions? CliSettings { get; set; } = new();
    public SwitchKeysetOptions? SwitchKeysetOptions { get; set; } = new();
}
