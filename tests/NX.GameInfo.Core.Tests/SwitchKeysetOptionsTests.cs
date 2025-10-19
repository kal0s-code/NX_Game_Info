using System;
using System.IO;
using System.Linq;
using NX.GameInfo.Core.Services;
using Xunit;

namespace NX.GameInfo.Core.Tests;

public sealed class SwitchKeysetOptionsTests
{
    [Fact]
    public void EnumerateCandidateDirectories_StopsAfterExplicitDirectory()
    {
        var options = new SwitchKeysetOptions
        {
            KeysDirectory = "/explicit",
            ApplicationDirectory = "/application",
            UserProfileDirectory = "/user"
        };

        var candidates = options.EnumerateCandidateDirectories().ToArray();

        Assert.Single(candidates);
        Assert.Equal("/explicit", candidates[0]);
    }

    [Fact]
    public void TryResolveKeysDirectory_PrefersDirectoryContainingProdKeys()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string applicationDirectory = Path.Combine(root, "app");
        string userDirectory = Path.Combine(root, "user");

        Directory.CreateDirectory(applicationDirectory);
        Directory.CreateDirectory(userDirectory);

        try
        {
            File.WriteAllText(Path.Combine(userDirectory, "prod.keys"), string.Empty);

            var options = new SwitchKeysetOptions
            {
                ApplicationDirectory = applicationDirectory,
                UserProfileDirectory = userDirectory
            };

            bool success = options.TryResolveKeysDirectory(out var resolved);

            Assert.True(success);
            Assert.Equal(userDirectory, resolved);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TryResolveKeysDirectory_FallsBackToFirstCandidateWhenMissing()
    {
        string applicationDirectory = "/application";
        string userDirectory = "/user";

        var options = new SwitchKeysetOptions
        {
            ApplicationDirectory = applicationDirectory,
            UserProfileDirectory = userDirectory
        };

        bool success = options.TryResolveKeysDirectory(out var resolved);

        Assert.True(success);
        Assert.Equal(applicationDirectory, resolved);
    }
}
