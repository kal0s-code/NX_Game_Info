using LibHac.Ncm;
using NX.GameInfo.Core.Models;
using NX.GameInfo.Core.Services;
using Xunit;

namespace NX.GameInfo.Core.Tests;

public sealed class GameInfoProcessorTests
{
    [Fact]
    public void ApplySystemUpdateFallback_SetsPatchVersionWhenUnset()
    {
        var metadata = new TitleMetadata();

        GameInfoProcessor.ApplySystemUpdateFallback(metadata, ContentMetaType.Patch, 0);
        Assert.Equal(uint.MaxValue, metadata.SystemUpdate);

        GameInfoProcessor.ApplySystemUpdateFallback(metadata, ContentMetaType.Patch, 1966080);
        Assert.Equal(1966080U, metadata.SystemUpdate);
    }

    [Fact]
    public void ApplySystemUpdateFallback_DoesNotOverrideExistingValue()
    {
        var metadata = new TitleMetadata
        {
            SystemUpdate = 131072
        };

        GameInfoProcessor.ApplySystemUpdateFallback(metadata, ContentMetaType.Patch, 1966080);

        Assert.Equal(131072U, metadata.SystemUpdate);
    }

    [Fact]
    public void ApplySystemUpdateFallback_IgnoresNonPatchContent()
    {
        var metadata = new TitleMetadata();

        GameInfoProcessor.ApplySystemUpdateFallback(metadata, ContentMetaType.Application, 1966080);

        Assert.Equal(uint.MaxValue, metadata.SystemUpdate);
    }
}
