using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Foundation;
using LibHac.Ncm;

namespace NX_Game_Info
{
    public static class Common
    {
        private static readonly string SettingsDirectory =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NX_Game_Info");

        public static readonly string APPLICATION_DIRECTORY_PATH_PREFIX =
            (NSBundle.MainBundle?.BundleUrl?.Path ?? AppContext.BaseDirectory).TrimEnd('/') + "/";

        public static readonly string USER_PROFILE_PATH_PREFIX =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch") + Path.DirectorySeparatorChar;

        public const string LOG_FILE = "debug.log";

        public const string USER_SETTINGS = "settings.json";
        private const string HISTORY_FILE = "history.json";
        private const string RECENT_DIRECTORIES_FILE = "recent-directories.json";
        public const int HISTORY_SIZE = 10;

        public const string PROD_KEYS = "prod.keys";
        public const string TITLE_KEYS = "title.keys";
        public const string CONSOLE_KEYS = "console.keys";
        public const string HAC_VERSIONLIST = "hac_versionlist.json";
        public const string TITLE_KEYS_URI = "https://gist.githubusercontent.com/gneurshkgau/81bcaa7064bd8f98d7dffd1a1f1781a7/raw/title.keys";
        public const string HAC_VERSIONLIST_URI = "https://raw.githubusercontent.com/blawar/titledb/refs/heads/master/versions.json";

        private static string GetSettingsFilePath(string fileName)
        {
            Directory.CreateDirectory(SettingsDirectory);
            return Path.Combine(SettingsDirectory, fileName);
        }

        private static class SettingsStore
        {
            private static readonly JsonSerializerOptions SerializerOptions = new()
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            public static T Load<T>(string fileName, Func<T> factory) where T : class
            {
                var path = GetSettingsFilePath(fileName);
                if (!File.Exists(path))
                {
                    return factory();
                }

                try
                {
                    var json = File.ReadAllText(path);
                    var instance = JsonSerializer.Deserialize<T>(json, SerializerOptions);
                    return instance ?? factory();
                }
                catch
                {
                    return factory();
                }
            }

            public static void Save<T>(string fileName, T instance)
            {
                var path = GetSettingsFilePath(fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var json = JsonSerializer.Serialize(instance, SerializerOptions);
                File.WriteAllText(path, json);
            }
        }

        public sealed class Settings
        {
            private static readonly Lazy<Settings> LazyDefault = new(() => SettingsStore.Load(USER_SETTINGS, () => new Settings()));
            public static Settings Default => LazyDefault.Value;

            public int Version { get; set; }
            public string InitialDirectory { get; set; } = string.Empty;
            public string SDCardDirectory { get; set; } = string.Empty;
            public bool DebugLog { get; set; }
            public char CsvSeparator { get; set; } = ',';
            public string RenameFormat { get; set; } = "{n} [{i}][v{v}]";
            public bool NszExtension { get; set; }

            public void Save() => SettingsStore.Save(USER_SETTINGS, this);
            public void Upgrade()
            {
                // Retained for compatibility with previous ApplicationSettingsBase usage.
            }
        }

        public sealed class History
        {
            private static readonly Lazy<History> LazyDefault = new(() => SettingsStore.Load(HISTORY_FILE, () => new History()));
            public static History Default => LazyDefault.Value;

            public List<ArrayOfTitle> Titles { get; set; } = new();

            public void Save() => SettingsStore.Save(HISTORY_FILE, this);
            public void Upgrade()
            {
            }
        }

        public sealed class RecentDirectories
        {
            private static readonly Lazy<RecentDirectories> LazyDefault = new(() => SettingsStore.Load(RECENT_DIRECTORIES_FILE, () => new RecentDirectories()));
            public static RecentDirectories Default => LazyDefault.Value;

            public List<ArrayOfTitle> Titles { get; set; } = new();

            public void Save() => SettingsStore.Save(RECENT_DIRECTORIES_FILE, this);
            public void Upgrade()
            {
            }
        }

        public class ArrayOfTitle
        {
            public string description { get; set; } = string.Empty;
            public List<Title> title { get; set; } = new();

            public override string ToString() => description;
        }

        public class Title
        {
            public static IReadOnlyDictionary<string, uint> SystemUpdate { get; } = new Dictionary<string, uint>
            {
                { "4f8133e5b3657334e507c8e704011886.cnmt.nca", 450 },
                { "734d85b19c5f281e100407d84e8cbfb2.cnmt.nca", 65796 },
                { "a88bff745e9631e1bbe3ead2e1b8985e.cnmt.nca", 131162 },
                { "3ffb630a6ea3842dc6995e61944436ff.cnmt.nca", 196628 },
                { "01cca46a5c854c9240568cb0cce0cfd4.cnmt.nca", 262164 },
                { "7bef244b45bf63efb4bf47a236975ec6.cnmt.nca", 201327002 },
                { "9a78e13d48ca44b1987412352a1183a1.cnmt.nca", 201392178 },
                { "16729a20392d179306720f202f37990e.cnmt.nca", 201457684 },
                { "6602cb7e2626e61b86d8017b8102011a.cnmt.nca", 268501002 },
                { "27478e35cc6872b4b4e508b3c03a4c8f.cnmt.nca", 269484082 },
                { "faa857ad6e82f472863e97f810de036a.cnmt.nca", 335544750 },
                { "7f5529b7a092b77bf093bdf2f9a3bf96.cnmt.nca", 335609886 },
                { "df2b1a655168750bd19458fadac56439.cnmt.nca", 335675432 },
                { "de702a1b297bf45f15222e09ebd652b7.cnmt.nca", 336592976 },
                { "68649ec371f03e30d981796e516ff38e.cnmt.nca", 402653544 },
                { "e4d205cd07c87946566980c78e2f9577.cnmt.nca", 402718730 },
                { "455d71f72ea0e91e038c9844cd62efbc.cnmt.nca", 404750376 },
                { "b1ff802ffd764cc9a06382207a59409b.cnmt.nca", 469762248 },
                { "a39e61c1cce0c86e6e4292d9e5e254e7.cnmt.nca", 469827614 },
                { "f4698de5525da797c76740f38a1c08a0.cnmt.nca", 536871502 },
                { "197d36b9f1564710dae6edb9a73f03b7.cnmt.nca", 536936528 },
                { "68173bf86aa0884f2c989acc4102072f.cnmt.nca", 537919608 },
                { "9bde0122ff0c7611460165d3a7adb795.cnmt.nca", 603980216 },
                { "9684add4b199811749665b84d27c8cd9.cnmt.nca", 604045412 },
                { "7f12839dea0870d71187d0ebeed53270.cnmt.nca", 605028592 },
                { "8dec844718aae2464fa9f96865582c08.cnmt.nca", 606076948 },
                { "d508702ca7c50d1233662ed6b4993a09.cnmt.nca", 671089000 },
                { "2847d2f1dfeb7cd1bde2a7dcf2b67397.cnmt.nca", 671154196 },
                { "4ba2c6ae6f8f40f1c44fb82f12af2dde.cnmt.nca", 671219752 },
                { "fd7c2112250b321fe1e278dfaf11cd8d.cnmt.nca", 671285268 },
                { "8fcd9e3c1938ead201dc790138b595d3.cnmt.nca", 671350804 },
                { "528d5b06298ba4c5b656ab6472240821.cnmt.nca", 672137336 },
                { "7746448930b5db17c75227dd4a9b2f20.cnmt.nca", 673185852 },
            };

            public static IReadOnlyList<string> Properties { get; } = new[]
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

            public static IReadOnlyList<string> LanguageCode { get; } = new[]
            {
                "en-US", "en-GB", "ja", "fr", "de", "es-419", "es", "it",
                "nl", "fr-CA", "pt", "ru", "ko", "zh-TW", "zh-CN",
            };

        public enum Distribution
        {
            Digital,
            Cartridge,
            Homebrew,
            Filesystem,
            Invalid = -1
        }

        public enum TitleType
        {
            Application,
            Patch,
            AddOnContent,
            Invalid = -1
        }

            public enum Structure
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

            public string titleID { get; set; } = string.Empty;
            public string baseTitleID { get; set; } = string.Empty;
            public string titleName { get; set; } = string.Empty;
            public string displayVersion { get; set; } = string.Empty;
            public uint version { get; set; } = uint.MaxValue;
            public string versionString => FormatVersion(version);
            public uint latestVersion { get; set; } = uint.MaxValue;
            public string latestVersionString => FormatVersion(latestVersion);
            public uint systemUpdate { get; set; } = uint.MaxValue;
            public string systemUpdateString => FormatSystemVersion(systemUpdate);
            public uint systemVersion { get; set; } = uint.MaxValue;
            public string systemVersionString => FormatSystemVersion(systemVersion);
            public uint applicationVersion { get; set; } = uint.MaxValue;
            public string applicationVersionString => applicationVersion != uint.MaxValue ? applicationVersion.ToString() : string.Empty;
            public uint masterkey { get; set; } = uint.MaxValue;
            public string masterkeyString => FormatMasterKey(masterkey);
            public string titleKey { get; set; } = string.Empty;
            public string publisher { get; set; } = string.Empty;
            public HashSet<string> languages { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public string languagesString => string.Join(",", languages.Where(l => !string.IsNullOrEmpty(l)));
            public string filename { get; set; } = string.Empty;
            public long filesize { get; set; }
            public string filesizeString => FormatFileSize(filesize);
            public TitleType type { get; set; } = TitleType.Application;
            public string typeString => type switch
            {
                TitleType.Application => "Base",
                TitleType.Patch => "Update",
                TitleType.AddOnContent => "DLC",
                _ => string.Empty
            };
            public Distribution distribution { get; set; } = Distribution.Invalid;
            public HashSet<Structure> structure { get; set; } = new();
            public string structureString => GetStructureSummary();
            public bool? signature { get; set; }
            public string signatureString => signature is null ? string.Empty : signature.Value ? "Passed" : "Not Passed";
            public Permission permission { get; set; } = Permission.Invalid;
            public string permissionString => permission == Permission.Invalid ? string.Empty : permission.ToString();
            public string error { get; set; } = string.Empty;

            private static string FormatVersion(uint value)
            {
                if (value == uint.MaxValue)
                {
                    return string.Empty;
                }

                return value >= 65536 ? $"{value} ({value / 65536})" : value.ToString(CultureInfo.InvariantCulture);
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

            private static string FormatMasterKey(uint value)
            {
                return value switch
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
                    _ => value.ToString(CultureInfo.InvariantCulture)
                };
            }

            private static string FormatFileSize(long size)
            {
                if (size < 0)
                {
                    return size.ToString("0 B", CultureInfo.InvariantCulture);
                }

                double readable = size;
                string suffix = "B";

                if (size >= 1L << 40)
                {
                    readable = size / (double)(1L << 40);
                    suffix = "TB";
                }
                else if (size >= 1L << 30)
                {
                    readable = size / (double)(1L << 30);
                    suffix = "GB";
                }
                else if (size >= 1L << 20)
                {
                    readable = size / (double)(1L << 20);
                    suffix = "MB";
                }
                else if (size >= 1L << 10)
                {
                    readable = size / (double)(1L << 10);
                    suffix = "KB";
                }

                return $"{readable:0.##} {suffix}";
            }

            private string GetStructureSummary()
            {
                return distribution switch
                {
                    Distribution.Cartridge => structure.Contains(Structure.UpdatePartition) && structure.Contains(Structure.SecurePartition)
                        ? "Scene"
                        : structure.Contains(Structure.SecurePartition) ? "Converted" : "Not complete",
                    Distribution.Digital => structure.Contains(Structure.LegalinfoXml) &&
                                             structure.Contains(Structure.NacpXml) &&
                                             structure.Contains(Structure.PrograminfoXml) &&
                                             structure.Contains(Structure.CardspecXml)
                        ? "Scene"
                        : structure.Contains(Structure.AuthoringtoolinfoXml) ? "Homebrew"
                            : structure.Contains(Structure.Cert) && structure.Contains(Structure.Tik) ? "CDN"
                                : structure.Contains(Structure.CnmtXml) ? "Converted"
                                    : "Not complete",
                    Distribution.Filesystem => "Filesystem",
                    _ => string.Empty
                };
            }
        }
    }

    public static class StringExtensions
    {
        public static string Quote(this string text, char separator = ' ')
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text.Contains(separator) ? $"\"{text}\"" : text;
        }
    }
}
