using System;
using System.IO;
using NX.GameInfo.Cli;
using NX.GameInfo.Cli.Configuration;
using NX.GameInfo.Core.Services;
using Xunit;

namespace NX.GameInfo.Core.Tests;

public sealed class CliSettingsOptionsTests
{
    // TODO: add snapshot-style CLI integration tests once fixture content is available (Phase 2 follow-up).
    [Fact]
    public void GetSeparatorOrDefault_FallsBackToComma()
    {
        var options = new CliSettingsOptions { CsvSeparator = string.Empty };

        Assert.Equal(',', options.GetSeparatorOrDefault());
    }

    [Fact]
    public void GetSeparatorOrDefault_UsesFirstCharacter()
    {
        var options = new CliSettingsOptions { CsvSeparator = "::" };

        Assert.Equal(':', options.GetSeparatorOrDefault());
    }

    [Fact]
    public void Provider_Load_UsesDefaultsWhenFilesMissing()
    {
        string baseDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(baseDirectory);

        try
        {
            var provider = new CliSettingsProvider(userSettingsPath: Path.Combine(baseDirectory, "cli-settings.json"), baseDirectory: baseDirectory);
            var configuration = provider.Load();

            Assert.NotNull(configuration.CliSettings);
            Assert.Equal("filename", configuration.CliSettings!.DefaultSort);
            Assert.NotNull(configuration.SwitchKeysetOptions);
            Assert.False(configuration.SwitchKeysetOptions!.EnableDebugLogging);
        }
        finally
        {
            Directory.Delete(baseDirectory, recursive: true);
        }
    }

    [Fact]
    public void Provider_Save_RoundTripsConfiguration()
    {
        string baseDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(baseDirectory);

        string userSettingsPath = Path.Combine(baseDirectory, "cli-settings.json");
        var provider = new CliSettingsProvider(userSettingsPath, baseDirectory);

        var configuration = new CliUserConfiguration
        {
            CliSettings = new CliSettingsOptions
            {
                CsvSeparator = "|",
                AllowCompressed = true,
                DebugLog = true,
                DefaultSort = "titlename"
            },
            SwitchKeysetOptions = new SwitchKeysetOptions
            {
                KeysDirectory = "/tmp/keys",
                EnableDebugLogging = true
            }
        };

        provider.Save(configuration);
        var roundTripped = provider.Load();

        Assert.True(File.Exists(userSettingsPath));
        Assert.Equal("titlename", roundTripped.CliSettings!.DefaultSort);
        Assert.True(roundTripped.CliSettings.AllowCompressed);
        Assert.Equal("/tmp/keys", roundTripped.SwitchKeysetOptions!.KeysDirectory);
    }
}
