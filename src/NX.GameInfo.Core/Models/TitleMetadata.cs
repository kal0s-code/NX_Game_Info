using System.Linq;
using LibHac.Ncm;
using TitleType = LibHac.Ncm.ContentMetaType;

namespace NX.GameInfo.Core.Models;

/// <summary>
/// Represents the metadata that NX Game Info surfaces for a single title/file.
/// Derived from the legacy <c>Common.Title</c> type but reworked for .NET 9 and non-static usage.
/// </summary>
public sealed class TitleMetadata
{
    public static IReadOnlyDictionary<string, uint> SystemUpdateCodes { get; } = new Dictionary<string, uint>
    {
        { "4f8133e5b3657334e507c8e704011886.cnmt.nca", 450 },       // 1.0.0
        { "734d85b19c5f281e100407d84e8cbfb2.cnmt.nca", 65796 },     // 2.0.0
        { "a88bff745e9631e1bbe3ead2e1b8985e.cnmt.nca", 131162 },    // 2.1.0
        { "3ffb630a6ea3842dc6995e61944436ff.cnmt.nca", 196628 },    // 2.2.0
        { "01cca46a5c854c9240568cb0cce0cfd4.cnmt.nca", 262164 },    // 2.3.0
        { "7bef244b45bf63efb4bf47a236975ec6.cnmt.nca", 201327002 }, // 3.0.0
        { "9a78e13d48ca44b1987412352a1183a1.cnmt.nca", 201392178 }, // 3.0.1
        { "16729a20392d179306720f202f37990e.cnmt.nca", 201457684 }, // 3.0.2
        { "6602cb7e2626e61b86d8017b8102011a.cnmt.nca", 268501002 }, // 4.0.1
        { "27478e35cc6872b4b4e508b3c03a4c8f.cnmt.nca", 269484082 }, // 4.1.0
        { "faa857ad6e82f472863e97f810de036a.cnmt.nca", 335544750 }, // 5.0.0
        { "7f5529b7a092b77bf093bdf2f9a3bf96.cnmt.nca", 335609886 }, // 5.0.1
        { "df2b1a655168750bd19458fadac56439.cnmt.nca", 335675432 }, // 5.0.2
        { "de702a1b297bf45f15222e09ebd652b7.cnmt.nca", 336592976 }, // 5.1.0
        { "68649ec371f03e30d981796e516ff38e.cnmt.nca", 402653544 }, // 6.0.0
        { "e4d205cd07c87946566980c78e2f9577.cnmt.nca", 402718730 }, // 6.0.1
        { "455d71f72ea0e91e038c9844cd62efbc.cnmt.nca", 404750376 }, // 6.2.0
        { "b1ff802ffd764cc9a06382207a59409b.cnmt.nca", 469762248 }, // 7.0.0
        { "a39e61c1cce0c86e6e4292d9e5e254e7.cnmt.nca", 469827614 }, // 7.0.1
        { "f4698de5525da797c76740f38a1c08a0.cnmt.nca", 536871502 }, // 8.0.0
        { "197d36b9f1564710dae6edb9a73f03b7.cnmt.nca", 536936528 }, // 8.0.1
        { "68173bf86aa0884f2c989acc4102072f.cnmt.nca", 537919608 }, // 8.1.0
        { "9bde0122ff0c7611460165d3a7adb795.cnmt.nca", 603980216 }, // 9.0.0
        { "9684add4b199811749665b84d27c8cd9.cnmt.nca", 604045412 }, // 9.0.1
        { "7f12839dea0870d71187d0ebeed53270.cnmt.nca", 605028592 }, // 9.1.0
        { "8dec844718aae2464fa9f96865582c08.cnmt.nca", 606076948 }, // 9.2.0
        { "d508702ca7c50d1233662ed6b4993a09.cnmt.nca", 671089000 }, // 10.0.0
        { "2847d2f1dfeb7cd1bde2a7dcf2b67397.cnmt.nca", 671154196 }, // 10.0.1
        { "4ba2c6ae6f8f40f1c44fb82f12af2dde.cnmt.nca", 671219752 }, // 10.0.2
        { "fd7c2112250b321fe1e278dfaf11cd8d.cnmt.nca", 671285268 }, // 10.0.3
        { "8fcd9e3c1938ead201dc790138b595d3.cnmt.nca", 671350804 }, // 10.0.4
        { "528d5b06298ba4c5b656ab6472240821.cnmt.nca", 672137336 }, // 10.1.0
        { "7746448930b5db17c75227dd4a9b2f20.cnmt.nca", 673185852 }, // 10.2.0
    };

    public static IReadOnlyList<string> PropertyNames { get; } = new[]
    {
        "Title ID",
        "Base Title ID",
        "Title Name",
        "Display Version",
        "Version",
        "Latest Version",
        "System Update",
        "System Version",
        "Application Version",
        "Masterkey",
        "Title Key",
        "Publisher",
        "Languages",
        "Filename",
        "Filesize",
        "Type",
        "Distribution",
        "Structure",
        "Signature",
        "Permission",
        "Error",
    };

    public static IReadOnlyList<string> LanguageCodes { get; } = new[]
    {
        "en-US",
        "en-GB",
        "ja",
        "fr",
        "de",
        "es-419",
        "es",
        "it",
        "nl",
        "fr-CA",
        "pt",
        "ru",
        "ko",
        "zh-TW",
        "zh-CN",
    };

    public enum Distribution
    {
        Digital,
        Cartridge,
        Homebrew,
        Filesystem,
        Invalid = -1
    }

    public enum TitleStructure
    {
        CnmtXml,
        CnmtNca,
        Cert,
        Tik,
        LegalinfoXml,
        NacpXml,
        PrograminfoXml,
        CardspecXml,
        AuthoringtoolinfoXml,
        RootPartition,
        UpdatePartition,
        NormalPartition,
        SecurePartition,
        LogoPartition,
        Invalid = -1
    }

    public enum Permission
    {
        Safe,
        Unsafe,
        Dangerous,
        Invalid = -1
    }

    public string TitleId { get; set; } = string.Empty;
    public string BaseTitleId { get; set; } = string.Empty;
    public string TitleName { get; set; } = string.Empty;
    public string DisplayVersion { get; set; } = string.Empty;
    public uint Version { get; set; } = uint.MaxValue;
    public uint LatestVersion { get; set; } = uint.MaxValue;
    public uint SystemUpdate { get; set; } = uint.MaxValue;
    public uint SystemVersion { get; set; } = uint.MaxValue;
    public uint ApplicationVersion { get; set; } = uint.MaxValue;
    public uint MasterKey { get; set; } = uint.MaxValue;
    public string TitleKey { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public HashSet<string> Languages { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string Filename { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public TitleType Type { get; set; } = TitleType.Application;
    public Distribution DistributionType { get; set; } = Distribution.Invalid;
    public HashSet<TitleStructure> Structure { get; } = new();
    public bool? SignatureValid { get; set; }
    public Permission PermissionLevel { get; set; } = Permission.Invalid;
    public string Error { get; set; } = string.Empty;

    public string VersionString => FormatVersion(Version);
    public string LatestVersionString => FormatVersion(LatestVersion);
    public string SystemUpdateString => FormatSystemVersion(SystemUpdate);
    public string SystemVersionString => FormatSystemVersion(SystemVersion);
    public string ApplicationVersionString => ApplicationVersion != uint.MaxValue ? ApplicationVersion.ToString() : string.Empty;
    public string MasterKeyString => MasterKey switch
    {
        uint.MaxValue => string.Empty,
        0 => "0 (1.0.0-2.3.0)",
        1 => "1 (3.0.0)",
        2 => "2 (3.0.1-3.0.2)",
        3 => "3 (4.0.0-4.1.0)",
        4 => "4 (5.0.0-5.1.0)",
        5 => "5 (6.0.0-6.1.0)",
        6 => "6 (6.2.0)",
        7 => "7 (7.0.0-8.0.1)",
        8 => "8 (8.1.0)",
        9 => "9 (9.0.0-9.0.1)",
        10 => "10 (9.1.0-10.2.0)",
        _ => MasterKey.ToString()
    };
    public string LanguagesString => string.Join(",", Languages.Where(l => !string.IsNullOrWhiteSpace(l)));
    public string FileSizeString => FormatFileSize(FileSize);
    public string TypeString => Type switch
    {
        TitleType.Application => "Base",
        TitleType.Patch => "Update",
        TitleType.AddOnContent => "DLC",
        _ => string.Empty
    };
    public string StructureString => GetStructureSummary();
    public string SignatureString => SignatureValid is null ? string.Empty : SignatureValid.Value ? "Passed" : "Not Passed";
    public string PermissionString => PermissionLevel == Permission.Invalid ? string.Empty : PermissionLevel.ToString();

    private static string FormatVersion(uint value)
    {
        if (value == uint.MaxValue)
        {
            return string.Empty;
        }

        return value >= 65536 ? $"{value} ({value / 65536})" : value.ToString();
    }

    private static string FormatSystemVersion(uint value)
    {
        if (value == uint.MaxValue)
        {
            return string.Empty;
        }

        if (value == 0)
        {
            return "0";
        }

        if (value <= 450)
        {
            return "1.0.0";
        }

        if (value <= 65796)
        {
            return "2.0.0";
        }

        if (value <= 131162)
        {
            return "2.1.0";
        }

        if (value <= 196628)
        {
            return "2.2.0";
        }

        if (value <= 262164)
        {
            return "2.3.0";
        }

        int major = (int)((value >> 26) & 0x3F);
        int minor = (int)((value >> 20) & 0x3F);
        int patch = (int)((value >> 16) & 0x0F);

        return $"{major}.{minor}.{patch}";
    }

    private static string FormatFileSize(long size)
    {
        if (size < 0)
        {
            return size.ToString("0 B");
        }

        // Reuse legacy behaviour for consistency with existing exports.
        long absolute = size;
        string suffix;
        double readable;

        if (absolute >= 0x1000000000000000) // Exabyte
        {
            suffix = "EB";
            readable = size >> 50;
        }
        else if (absolute >= 0x4000000000000) // Petabyte
        {
            suffix = "PB";
            readable = size >> 40;
        }
        else if (absolute >= 0x10000000000) // Terabyte
        {
            suffix = "TB";
            readable = size >> 30;
        }
        else if (absolute >= 0x40000000) // Gigabyte
        {
            suffix = "GB";
            readable = size >> 20;
        }
        else if (absolute >= 0x100000) // Megabyte
        {
            suffix = "MB";
            readable = size >> 10;
        }
        else if (absolute >= 0x400) // Kilobyte
        {
            suffix = "KB";
            readable = size;
        }
        else
        {
            return size.ToString("0 B");
        }

        readable /= 1024;
        return readable.ToString("0.## ") + suffix;
    }

    private string GetStructureSummary()
    {
        return DistributionType switch
        {
            Distribution.Cartridge => StructureContainsAll(TitleStructure.UpdatePartition, TitleStructure.SecurePartition)
                                      && StructureContainsAny(TitleStructure.RootPartition, TitleStructure.NormalPartition)
                ? "Scene"
                : StructureContainsAll(TitleStructure.SecurePartition)
                    ? "Converted"
                    : "Not complete",
            Distribution.Digital => StructureContainsAll(
                                        TitleStructure.LegalinfoXml,
                                        TitleStructure.NacpXml,
                                        TitleStructure.PrograminfoXml,
                                        TitleStructure.CardspecXml)
                                    ? "Scene"
                                    : StructureContainsAll(TitleStructure.AuthoringtoolinfoXml)
                                        ? "Homebrew"
                                        : StructureContainsAll(TitleStructure.Cert, TitleStructure.Tik)
                                            ? "CDN"
                                            : StructureContainsAll(TitleStructure.CnmtXml)
                                                ? "Converted"
                                                : "Not complete",
            Distribution.Filesystem => "Filesystem",
            _ => string.Empty
        };
    }

    private bool StructureContainsAll(params TitleStructure[] values) => values.All(Structure.Contains);

    private bool StructureContainsAny(params TitleStructure[] values) => values.Any(Structure.Contains);
}
