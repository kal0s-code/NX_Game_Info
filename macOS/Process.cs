using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IOPath = System.IO.Path;
using System.Threading.Tasks;
using LibHac.Common.Keys;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Ncm;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Util;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NX.GameInfo.Core.Models;
using NX.GameInfo.Core.Services;
using NX.GameInfo.Core.Infrastructure;
using ArrayOfTitle = NX_Game_Info.Common.ArrayOfTitle;
using TitleType = NX_Game_Info.Common.Title.TitleType;

namespace NX_Game_Info
{
    public static class Process
    {
        public static SwitchKeysetContext? Context { get; private set; }
        public static GameInfoProcessor? Processor { get; private set; }
        public static StreamWriter? log;

        public static KeySet? keyset => Context?.Keyset;

        public static readonly Dictionary<string, uint> versionList = new(StringComparer.OrdinalIgnoreCase);
        public static readonly Dictionary<string, string> titleNames = new(StringComparer.OrdinalIgnoreCase);
        public static readonly Dictionary<string, uint> titleVersions = new(StringComparer.OrdinalIgnoreCase);
        public static readonly Dictionary<string, uint> latestVersions = new(StringComparer.OrdinalIgnoreCase);

        public static string path_prefix = Common.APPLICATION_DIRECTORY_PATH_PREFIX;

        private static SwitchKeysetService _keysetService = new(NullLogger<SwitchKeysetService>.Instance);
        private static ILogger<GameInfoProcessor> _processorLogger = NullLogger<GameInfoProcessor>.Instance;

        public static void ConfigureServices(SwitchKeysetService keysetService, ILogger<GameInfoProcessor>? processorLogger = null)
        {
            _keysetService = keysetService ?? throw new ArgumentNullException(nameof(keysetService));
            _processorLogger = processorLogger ?? NullLogger<GameInfoProcessor>.Instance;
        }

        public static bool initialize(out List<string> messages)
        {
            messages = new List<string>();

            try
            {
                string applicationDirectory = Common.APPLICATION_DIRECTORY_PATH_PREFIX;
                string userDirectory = Common.USER_PROFILE_PATH_PREFIX;

                var options = new SwitchKeysetOptions
                {
                    ApplicationDirectory = applicationDirectory.TrimEnd(IOPath.DirectorySeparatorChar),
                    UserProfileDirectory = userDirectory.TrimEnd(IOPath.DirectorySeparatorChar),
                    EnableDebugLogging = Common.Settings.Default.DebugLog
                };

                if (File.Exists(IOPath.Combine(applicationDirectory, Common.PROD_KEYS)))
                {
                    options.KeysDirectory = applicationDirectory;
                }
                else if (File.Exists(IOPath.Combine(userDirectory, Common.PROD_KEYS)))
                {
                    options.KeysDirectory = userDirectory;
                }

                Context = Task.Run(async () => await _keysetService.LoadAsync(options).ConfigureAwait(false)).GetAwaiter().GetResult();
                Processor = new GameInfoProcessor(Context, _processorLogger);
                log = Context.LogWriter;

                path_prefix = IOPath.GetDirectoryName(Context.ProdKeysPath) is { Length: > 0 } directory
                    ? directory.TrimEnd(IOPath.DirectorySeparatorChar) + IOPath.DirectorySeparatorChar
                    : applicationDirectory;

                versionList.Clear();
                foreach (var pair in Context.VersionList)
                {
                    versionList[pair.Key] = pair.Value;
                }

                titleNames.Clear();
                foreach (var pair in Context.TitleNames)
                {
                    titleNames[pair.Key] = pair.Value;
                }

                titleVersions.Clear();
                foreach (var pair in Context.TitleVersions)
                {
                    titleVersions[pair.Key] = pair.Value;
                }

                latestVersions.Clear();

                if (Common.Settings.Default.DebugLog && Context.LogWriter == null)
                {
                    try
                    {
                        log = File.AppendText(IOPath.Combine(path_prefix, Common.LOG_FILE));
                        log.AutoFlush = true;
                    }
                    catch (IOException ex)
                    {
                        messages.Add($"Unable to open log file: {ex.Message}");
                    }
                }

                log ??= Context.LogWriter;
                log?.WriteLine("--------------------------------------------------------------");
                log?.WriteLine("Application starts at {0}", DateTime.Now.ToString("F"));
                log?.WriteLine("Keys loaded from {0}", path_prefix);

                return true;
            }
            catch (Exception ex)
            {
                messages.Add($"Failed to load keys: {ex.Message}");
                return false;
            }
        }

        public static void migrateSettings()
        {
            // no version specific migrations required at the moment
        }

        public static bool updateTitleKeys()
        {
            if (Context == null || Processor == null)
            {
                return false;
            }

            try
            {
                var options = new SwitchKeysetOptions
                {
                    KeysDirectory = IOPath.GetDirectoryName(Context.ProdKeysPath),
                };

                if (_keysetService.RefreshTitleKeysAsync(Context, options).GetAwaiter().GetResult())
                {
                    RebuildTitleMetadata();
                    return true;
                }
            }
            catch (Exception ex)
            {
                log?.WriteLine("Failed to refresh title keys: {0}", ex);
            }

            return false;
        }

        public static bool updateVersionList()
        {
            if (Context == null || Processor == null)
            {
                return false;
            }

            try
            {
                var options = new SwitchKeysetOptions
                {
                    KeysDirectory = IOPath.GetDirectoryName(Context.ProdKeysPath),
                };

                if (_keysetService.RefreshVersionListAsync(Context, options).GetAwaiter().GetResult())
                {
                    versionList.Clear();
                    foreach (var pair in Context.VersionList)
                    {
                        versionList[pair.Key] = pair.Value;
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                log?.WriteLine("Failed to refresh version list: {0}", ex);
            }

            return false;
        }

        public static Common.Title? processFile(string filename)
        {
            if (Processor == null)
            {
                return null;
            }

            var metadata = Processor.ProcessFile(filename);
            if (metadata == null)
            {
                return null;
            }

            return Convert(metadata);
        }

        public static List<Common.Title>? processSd(string path)
        {
            if (Context == null || Processor == null)
            {
                return null;
            }

            if (!Context.TryValidateSdCardKeys(out var missingKeys))
            {
                string message = $"Missing required SD card keys: {string.Join(", ", missingKeys)}";
                log?.WriteLine(message);
                return null;
            }

            try
            {
                using var baseFs = new UniqueRef<IAttributeFileSystem>(new LocalFileSystem(path));
                using var capture = ConsoleCaptureScope.Redirect(message => Context.RecordDiagnostic(message));
                using var switchFs = SwitchFs.OpenSdCard(Context.Keyset, ref baseFs.Ref);

                return Processor.ProcessSwitchFileSystem(switchFs)
                    .Select(Convert)
                    .Where(t => t != null)
                    .ToList()!;
            }
            catch (Exception ex)
            {
                log?.WriteLine("Failed to process SD card: {0}", ex);
                return null;
            }
        }

        public static List<Common.Title> convert(IEnumerable<TitleMetadata> metadataCollection)
        {
            return metadataCollection.Select(Convert).Where(t => t != null).ToList()!;
        }

        private static Common.Title Convert(TitleMetadata metadata)
        {
            var title = new Common.Title
            {
                titleID = metadata.TitleId,
                baseTitleID = metadata.BaseTitleId,
                titleName = metadata.TitleName,
                displayVersion = metadata.DisplayVersion,
                version = metadata.Version,
                latestVersion = metadata.LatestVersion,
                systemUpdate = metadata.SystemUpdate,
                systemVersion = metadata.SystemVersion,
                applicationVersion = metadata.ApplicationVersion,
                masterkey = metadata.MasterKey,
                titleKey = metadata.TitleKey,
                publisher = metadata.Publisher,
                filename = metadata.Filename,
                filesize = metadata.FileSize,
                type = metadata.Type switch
                {
                    ContentMetaType.Patch => TitleType.Patch,
                    ContentMetaType.AddOnContent => TitleType.AddOnContent,
                    _ => TitleType.Application
                },
                distribution = metadata.DistributionType switch
                {
                    TitleMetadata.Distribution.Cartridge => Common.Title.Distribution.Cartridge,
                    TitleMetadata.Distribution.Homebrew => Common.Title.Distribution.Homebrew,
                    TitleMetadata.Distribution.Filesystem => Common.Title.Distribution.Filesystem,
                    _ => Common.Title.Distribution.Digital
                },
                signature = metadata.SignatureValid,
                permission = metadata.PermissionLevel switch
                {
                    TitleMetadata.Permission.Dangerous => Common.Title.Permission.Dangerous,
                    TitleMetadata.Permission.Unsafe => Common.Title.Permission.Unsafe,
                    TitleMetadata.Permission.Safe => Common.Title.Permission.Safe,
                    _ => Common.Title.Permission.Invalid
                },
                error = metadata.Error
            };

            foreach (var language in metadata.Languages)
            {
                title.languages.Add(language);
            }

            foreach (var structure in metadata.Structure)
            {
                var mapped = structure switch
                {
                    TitleMetadata.TitleStructure.CnmtXml => Common.Title.Structure.CnmtXml,
                    TitleMetadata.TitleStructure.CnmtNca => Common.Title.Structure.CnmtNca,
                    TitleMetadata.TitleStructure.Cert => Common.Title.Structure.Cert,
                    TitleMetadata.TitleStructure.Tik => Common.Title.Structure.Tik,
                    TitleMetadata.TitleStructure.LegalinfoXml => Common.Title.Structure.LegalinfoXml,
                    TitleMetadata.TitleStructure.NacpXml => Common.Title.Structure.NacpXml,
                    TitleMetadata.TitleStructure.PrograminfoXml => Common.Title.Structure.PrograminfoXml,
                    TitleMetadata.TitleStructure.CardspecXml => Common.Title.Structure.CardspecXml,
                    TitleMetadata.TitleStructure.AuthoringtoolinfoXml => Common.Title.Structure.AuthoringtoolinfoXml,
                    TitleMetadata.TitleStructure.RootPartition => Common.Title.Structure.RootPartition,
                    TitleMetadata.TitleStructure.UpdatePartition => Common.Title.Structure.UpdatePartition,
                    TitleMetadata.TitleStructure.NormalPartition => Common.Title.Structure.NormalPartition,
                    TitleMetadata.TitleStructure.SecurePartition => Common.Title.Structure.SecurePartition,
                    TitleMetadata.TitleStructure.LogoPartition => Common.Title.Structure.LogoPartition,
                    _ => Common.Title.Structure.Invalid
                };

                if (mapped != Common.Title.Structure.Invalid)
                {
                    title.structure.Add(mapped);
                }
            }

            string titleIdLookup = metadata.Type == LibHac.Ncm.ContentMetaType.AddOnContent
                ? metadata.TitleId
                : metadata.BaseTitleId;

            if (!string.IsNullOrEmpty(titleIdLookup))
            {
                latestVersions[titleIdLookup] = Math.Max(metadata.Version, latestVersions.TryGetValue(titleIdLookup, out uint existing) ? existing : 0);
            }

            if (!string.IsNullOrEmpty(metadata.TitleId))
            {
                titleNames[metadata.TitleId] = metadata.TitleName;
                titleVersions[metadata.TitleId] = metadata.Version;
            }

            if (!string.IsNullOrEmpty(metadata.BaseTitleId))
            {
                titleNames[metadata.BaseTitleId] = metadata.TitleName;
                titleVersions[metadata.BaseTitleId] = metadata.Version;
            }

            return title;
        }

        public static List<Common.Title> processHistory(int index = -1)
        {
            ArrayOfTitle history = index != -1
                ? Common.History.Default.Titles.ElementAtOrDefault(index)
                : Common.History.Default.Titles.LastOrDefault();

            List<Common.Title> titles = history?.title?.ToList() ?? new List<Common.Title>();

            foreach (var title in titles)
            {
                string titleID = title.type == TitleType.AddOnContent ? title.titleID : title.baseTitleID ?? "";

                if (string.IsNullOrEmpty(titleID))
                {
                    continue;
                }

                if (latestVersions.TryGetValue(titleID, out uint version))
                {
                    if (title.version > version)
                    {
                        latestVersions[titleID] = title.version;
                    }
                }
                else
                {
                    latestVersions[titleID] = title.version;
                }

                if (!string.IsNullOrEmpty(title.titleID))
                {
                    titleNames[title.titleID] = title.titleName;
                    titleVersions[title.titleID] = title.version;
                }

                if (!string.IsNullOrEmpty(title.baseTitleID))
                {
                    titleNames[title.baseTitleID] = title.titleName;
                    titleVersions[title.baseTitleID] = title.version;
                }
            }

            return titles;
        }

        private static void RebuildTitleMetadata()
        {
            if (Context == null)
            {
                return;
            }

            titleNames.Clear();
            foreach (var pair in Context.TitleNames)
            {
                titleNames[pair.Key] = pair.Value;
            }

            titleVersions.Clear();
            foreach (var pair in Context.TitleVersions)
            {
                titleVersions[pair.Key] = pair.Value;
            }
        }
    }
}
