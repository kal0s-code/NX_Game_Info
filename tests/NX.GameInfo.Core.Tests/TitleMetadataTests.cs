using NX.GameInfo.Core.Models;
using Xunit;

namespace NX.GameInfo.Core.Tests;

public sealed class TitleMetadataTests
{
    [Fact]
    public void StructureString_RecognizesDigitalScene()
    {
        var metadata = new TitleMetadata
        {
            DistributionType = TitleMetadata.Distribution.Digital
        };

        metadata.Structure.Add(TitleMetadata.TitleStructure.LegalinfoXml);
        metadata.Structure.Add(TitleMetadata.TitleStructure.NacpXml);
        metadata.Structure.Add(TitleMetadata.TitleStructure.PrograminfoXml);
        metadata.Structure.Add(TitleMetadata.TitleStructure.CardspecXml);

        Assert.Equal("Scene", metadata.StructureString);
    }

    [Fact]
    public void StructureString_RecognizesCartridgeScene()
    {
        var metadata = new TitleMetadata
        {
            DistributionType = TitleMetadata.Distribution.Cartridge
        };

        metadata.Structure.Add(TitleMetadata.TitleStructure.SecurePartition);
        metadata.Structure.Add(TitleMetadata.TitleStructure.UpdatePartition);
        metadata.Structure.Add(TitleMetadata.TitleStructure.RootPartition);

        Assert.Equal("Scene", metadata.StructureString);
    }

    [Fact]
    public void VersionString_FormatsWithBuildNumber()
    {
        var metadata = new TitleMetadata
        {
            Version = 131072 // 0x20000 -> build 2
        };

        Assert.Equal("131072 (2)", metadata.VersionString);
    }

    [Fact]
    public void MasterKeyString_DescribesKnownKeys()
    {
        var metadata = new TitleMetadata
        {
            MasterKey = 4
        };

        Assert.Equal("4 (5.0.0-5.1.0)", metadata.MasterKeyString);
    }
}
