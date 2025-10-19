using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using LibHac;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Ns;
using LibHac.Tools.Es;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using LibHac.Tools.Ncm;
using LibHac.Tools.Ro;
using LibHac.Tools.Npdm;
using Microsoft.Extensions.Logging;
using NX.GameInfo.Core.Models;
using NX.GameInfo.Core.Infrastructure;
using LibHac.Spl;

namespace NX.GameInfo.Core.Services;

/// <summary>
/// Provides high-level metadata extraction for Switch content archives (XCI/NSP/NRO).
/// </summary>
public sealed class GameInfoProcessor
{
    private readonly SwitchKeysetContext _context;
    private readonly ILogger<GameInfoProcessor> _logger;
    private readonly Dictionary<string, uint> _latestVersions = new(StringComparer.OrdinalIgnoreCase);
    private const int MaxLanguageEntries = 16;

    public GameInfoProcessor(SwitchKeysetContext context, ILogger<GameInfoProcessor> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public TitleMetadata? ProcessFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path must be provided", nameof(path));
        }

        string extension = System.IO.Path.GetExtension(path).ToLowerInvariant();

        try
        {
            return extension switch
            {
                ".xci" or ".xcz" => ProcessXci(path),
                ".nsp" or ".nsz" => ProcessNsp(path),
                ".nro" => ProcessNro(path),
                _ => null
            };
        }
        catch (Exception ex) when (ex is IOException or LibHacException or InvalidDataException)
        {
            _logger.LogWarning(ex, "Failed to process file {Path}", path);
            return null;
        }
    }

    private TitleMetadata ProcessXci(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var storage = stream.AsStorage(keepOpen: false);

        var metadata = CreateSkeleton(path, TitleMetadata.Distribution.Cartridge, stream.Length);

        var xci = new Xci(_context.Keyset, storage);

        metadata.Structure.Add(TitleMetadata.TitleStructure.RootPartition);

        if (xci.HasPartition(XciPartitionType.Update))
        {
            metadata.Structure.Add(TitleMetadata.TitleStructure.UpdatePartition);
            var updatePartition = xci.OpenPartition(XciPartitionType.Update);
            PopulateSystemUpdate(metadata, updatePartition);
        }

        if (xci.HasPartition(XciPartitionType.Normal))
        {
            metadata.Structure.Add(TitleMetadata.TitleStructure.NormalPartition);
        }

        if (xci.HasPartition(XciPartitionType.Secure))
        {
            var securePartition = xci.OpenPartition(XciPartitionType.Secure);
            metadata.Structure.Add(TitleMetadata.TitleStructure.SecurePartition);

            PopulateFromContentFileSystem(metadata, securePartition, path);
        }

        FinalizeMetadata(metadata);
        return metadata;
    }

    private TitleMetadata ProcessNsp(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var storage = stream.AsStorage(keepOpen: false);

        var metadata = CreateSkeleton(path, TitleMetadata.Distribution.Digital, stream.Length);

        var pfs = new PartitionFileSystem();
        pfs.Initialize(storage).ThrowIfFailure();

        PopulateFromContentFileSystem(metadata, pfs, path);

        DetermineDigitalStructure(metadata, pfs);

        FinalizeMetadata(metadata);
        return metadata;
    }

    private TitleMetadata ProcessNro(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var storage = stream.AsStorage(keepOpen: false);

        var metadata = CreateSkeleton(path, TitleMetadata.Distribution.Homebrew, stream.Length);

        var nro = new Nro(storage);

        using var nacpStorage = nro.OpenNroAssetSection(NroAssetType.Nacp, leaveOpen: false);
        if (nacpStorage is not null && nacpStorage.GetSize(out long nacpSize).IsSuccess() && nacpSize > 0)
        {
            using var nacpStream = nacpStorage.AsStream();
            PopulateFromNacp(metadata, nacpStream);
        }

        FinalizeMetadata(metadata);
        return metadata;
    }

    private void PopulateFromContentFileSystem(TitleMetadata metadata, IFileSystem fileSystem, string sourcePath)
    {
        using var capture = ConsoleCaptureScope.Redirect(RelayLibHacDiagnostics);
        using var switchFs = SwitchFs.OpenNcaDirectory(_context.Keyset, fileSystem);

        ProcessPartitionEntries(metadata, fileSystem);

        var application = SelectApplication(switchFs);
        if (application != null)
        {
            PopulateFromApplication(metadata, application);
        }

        _logger.LogDebug("Processed content file system for {Path}", sourcePath);
    }

    private void PopulateFromApplication(TitleMetadata metadata, Application application, Title? preferredTitle = null)
    {
        if (application == null)
        {
            return;
        }

        var mainTitle = preferredTitle ?? application.Main ?? application.Patch ?? application.AddOnContent.FirstOrDefault();
        if (mainTitle == null)
        {
            return;
        }

        metadata.TitleId = FormatId(mainTitle.Id);
        metadata.BaseTitleId = FormatId(application.TitleId != 0 ? application.TitleId : mainTitle.Metadata.ApplicationTitleId);
        metadata.Type = mainTitle.Metadata.Type;
        metadata.Version = mainTitle.Version?.Version ?? 0;
        metadata.DisplayVersion = application.DisplayVersion ?? string.Empty;
        metadata.TitleName = string.IsNullOrWhiteSpace(application.Name) ? metadata.TitleName : application.Name;

        if (mainTitle.Metadata.MinimumSystemVersion != null)
        {
            metadata.SystemVersion = mainTitle.Metadata.MinimumSystemVersion.Version;
        }

        ApplySystemUpdateFallback(metadata,
            mainTitle.Metadata.Type,
            mainTitle.Metadata.MinimumSystemVersion?.Version ?? 0);

        if (mainTitle.Metadata.MinimumApplicationVersion != null)
        {
            metadata.ApplicationVersion = mainTitle.Metadata.MinimumApplicationVersion.Version;
        }

        if (mainTitle.MainNca != null)
        {
            PopulateProgramMetadata(metadata, mainTitle.MainNca.Nca);
        }

        if (mainTitle.ControlNca != null)
        {
            PopulateControlMetadata(metadata, mainTitle.ControlNca.Nca);
        }

        ApplyNacp(metadata, application.Nacp.Value);

        if (mainTitle.Metadata.ContentEntries != null)
        {
            foreach (CnmtContentEntry entry in mainTitle.Metadata.ContentEntries)
            {
                switch (entry.Type)
                {
                    case LibHac.Ncm.ContentType.Program:
                    case LibHac.Ncm.ContentType.Data:
                        metadata.Structure.Add(TitleMetadata.TitleStructure.CnmtNca);
                        break;
                    case LibHac.Ncm.ContentType.Control:
                        metadata.Structure.Add(TitleMetadata.TitleStructure.NacpXml);
                        break;
                }
            }
        }
    }

    private static Application? SelectApplication(SwitchFs switchFs)
    {
        var application = switchFs.Applications.Values.FirstOrDefault();
        if (application != null)
        {
            return application;
        }

        var fallbackTitle = switchFs.Titles.Values.FirstOrDefault();
        return fallbackTitle != null ? CreateApplicationFromTitle(fallbackTitle) : null;
    }

    public IEnumerable<TitleMetadata> ProcessSwitchFileSystem(SwitchFs switchFs)
    {
        if (switchFs == null)
        {
            yield break;
        }

        foreach (var fsTitle in switchFs.Titles.Values.OrderBy(title => title.Id))
        {
            var application = FindApplicationForTitle(switchFs, fsTitle) ?? CreateApplicationFromTitle(fsTitle);

            string filename = fsTitle.MainNca?.Filename
                               ?? fsTitle.ControlNca?.Filename
                               ?? $"{fsTitle.Id:X16}";

            var metadata = CreateSkeleton(filename, TitleMetadata.Distribution.Filesystem, fsTitle.GetSize());
            PopulateFromApplication(metadata, application, fsTitle);
            metadata.DistributionType = TitleMetadata.Distribution.Filesystem;

            FinalizeMetadata(metadata);
            yield return metadata;
        }
    }

    public TitleMetadata FromSwitchTitle(SwitchFs switchFs, Title title)
    {
        if (switchFs == null) throw new ArgumentNullException(nameof(switchFs));
        if (title == null) throw new ArgumentNullException(nameof(title));

        var application = FindApplicationForTitle(switchFs, title) ?? CreateApplicationFromTitle(title);

        string filename = title.MainNca?.Filename
                           ?? title.ControlNca?.Filename
                           ?? $"{title.Id:X16}";

        var metadata = CreateSkeleton(filename, TitleMetadata.Distribution.Filesystem, title.GetSize());
        PopulateFromApplication(metadata, application, title);
        metadata.DistributionType = TitleMetadata.Distribution.Filesystem;
        FinalizeMetadata(metadata);
        return metadata;
    }

    private static Application? FindApplicationForTitle(SwitchFs switchFs, Title title)
    {
        foreach (Application application in switchFs.Applications.Values)
        {
            if (ReferenceEquals(application.Main, title) ||
                ReferenceEquals(application.Patch, title) ||
                application.AddOnContent.Contains(title))
            {
                return application;
            }
        }

        return null;
    }

    private static Application CreateApplicationFromTitle(Title title)
    {
        var application = new Application();
        application.AddTitle(title);
        return application;
    }

    private void ProcessPartitionEntries(TitleMetadata metadata, IFileSystem fileSystem)
    {
        foreach (DirectoryEntryEx entry in fileSystem.EnumerateEntries("/", "*", SearchOptions.Default))
        {
            switch (System.IO.Path.GetExtension(entry.Name).ToLowerInvariant())
            {
                case ".cnmt":
                case ".nca" when entry.Name.EndsWith(".cnmt.nca", StringComparison.OrdinalIgnoreCase):
                    metadata.Structure.Add(TitleMetadata.TitleStructure.CnmtNca);
                    break;
                case ".nacp":
                case ".xml" when entry.Name.EndsWith("nacp.xml", StringComparison.OrdinalIgnoreCase):
                    metadata.Structure.Add(TitleMetadata.TitleStructure.NacpXml);
                    break;
                case ".xml" when entry.Name.EndsWith(".cnmt.xml", StringComparison.OrdinalIgnoreCase):
                    metadata.Structure.Add(TitleMetadata.TitleStructure.CnmtXml);
                    break;
                case ".tik":
                    metadata.Structure.Add(TitleMetadata.TitleStructure.Tik);
                    PopulateTitleKey(metadata, fileSystem, entry.FullPath);
                    break;
                case ".cert":
                    metadata.Structure.Add(TitleMetadata.TitleStructure.Cert);
                    break;
                case ".xml" when entry.Name.EndsWith("legalinfo.xml", StringComparison.OrdinalIgnoreCase):
                    metadata.Structure.Add(TitleMetadata.TitleStructure.LegalinfoXml);
                    break;
                case ".xml" when entry.Name.EndsWith("programinfo.xml", StringComparison.OrdinalIgnoreCase):
                    metadata.Structure.Add(TitleMetadata.TitleStructure.PrograminfoXml);
                    break;
                case ".xml" when entry.Name.Equals("cardspec.xml", StringComparison.OrdinalIgnoreCase):
                    metadata.Structure.Add(TitleMetadata.TitleStructure.CardspecXml);
                    break;
                case ".xml" when entry.Name.Equals("authoringtoolinfo.xml", StringComparison.OrdinalIgnoreCase):
                    metadata.Structure.Add(TitleMetadata.TitleStructure.AuthoringtoolinfoXml);
                    break;
            }
        }
    }

    private void PopulateSystemUpdate(TitleMetadata metadata, IFileSystem updatePartition)
    {
        foreach (DirectoryEntryEx entry in updatePartition.EnumerateEntries("/", "*.cnmt.nca", SearchOptions.Default))
        {
            if (TitleMetadata.SystemUpdateCodes.TryGetValue(entry.Name, out uint version))
            {
                metadata.SystemUpdate = version;
                break;
            }
        }
    }

    private void DetermineDigitalStructure(TitleMetadata metadata, IFileSystem fileSystem)
    {
        bool hasLegalinfo = metadata.Structure.Contains(TitleMetadata.TitleStructure.LegalinfoXml);
        bool hasNacp = metadata.Structure.Contains(TitleMetadata.TitleStructure.NacpXml);
        bool hasPrograminfo = metadata.Structure.Contains(TitleMetadata.TitleStructure.PrograminfoXml);
        bool hasCardspec = metadata.Structure.Contains(TitleMetadata.TitleStructure.CardspecXml);
        bool hasAuthoring = metadata.Structure.Contains(TitleMetadata.TitleStructure.AuthoringtoolinfoXml);
        bool hasCert = metadata.Structure.Contains(TitleMetadata.TitleStructure.Cert);
        bool hasTik = metadata.Structure.Contains(TitleMetadata.TitleStructure.Tik);

        if (hasLegalinfo && hasNacp && hasPrograminfo && hasCardspec)
        {
            metadata.Structure.Add(TitleMetadata.TitleStructure.CnmtXml);
        }

        if (hasAuthoring)
        {
            metadata.DistributionType = TitleMetadata.Distribution.Homebrew;
        }
        else if (hasCert && hasTik)
        {
            metadata.Structure.Add(TitleMetadata.TitleStructure.CnmtXml);
        }
    }

    private void PopulateProgramMetadata(TitleMetadata metadata, Nca nca)
    {
        if (nca.Header.KeyGeneration > 0)
        {
            metadata.MasterKey = (uint)Math.Max((int)nca.Header.KeyGeneration - 1, 0);
        }
        metadata.SignatureValid = metadata.SignatureValid ?? TryVerifyNca(nca);
        PopulatePermissions(metadata, nca);
    }

    private void PopulateControlMetadata(TitleMetadata metadata, Nca nca)
    {
        using var romfs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.ErrorOnInvalid);

        using var controlFile = new UniqueRef<IFile>();
        romfs.OpenFile(ref controlFile.Ref, "/control.nacp"u8, OpenMode.Read).ThrowIfFailure();

        metadata.Structure.Add(TitleMetadata.TitleStructure.NacpXml);

        var nacp = new ApplicationControlProperty();
        controlFile.Get.Read(out _, 0, MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref nacp, 1))).ThrowIfFailure();
        ApplyNacp(metadata, nacp);
    }

    private void PopulateFromNacp(TitleMetadata metadata, Stream nacpStream)
    {
        var nacp = new ApplicationControlProperty();
        nacpStream.ReadExactly(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref nacp, 1)));
        ApplyNacp(metadata, nacp);
    }

    private void ApplyNacp(TitleMetadata metadata, ApplicationControlProperty nacp)
    {
        if (!nacp.DisplayVersionString.IsEmpty())
        {
            metadata.DisplayVersion = nacp.DisplayVersionString.ToString();
        }

        bool titleSet = !string.IsNullOrWhiteSpace(metadata.TitleName);
        string? publisher = string.IsNullOrWhiteSpace(metadata.Publisher) ? null : metadata.Publisher;
        int maxEntries = Math.Min(MaxLanguageEntries, TitleMetadata.LanguageCodes.Count);

        for (int i = 0; i < maxEntries; i++)
        {
            ref readonly var title = ref nacp.Title[i];

            if (!title.NameString.IsEmpty())
            {
                if (!titleSet)
                {
                    metadata.TitleName = title.NameString.ToString();
                    titleSet = true;
                }

                metadata.Languages.Add(TitleMetadata.LanguageCodes[i]);
            }

            if (publisher is null && !title.PublisherString.IsEmpty())
            {
                publisher = title.PublisherString.ToString();
            }
        }

        if (publisher is not null)
        {
            metadata.Publisher = publisher;
        }
    }

    private void PopulateTitleKey(TitleMetadata metadata, IFileSystem fileSystem, string path)
    {
        using var file = new UniqueRef<IFile>();
        fileSystem.OpenFile(ref file.Ref, path.ToU8Span(), OpenMode.Read).ThrowIfFailure();

        using var ticketStream = file.Release().AsStream();
        var ticket = new Ticket(ticketStream);

        byte[]? titleKey = null;
        AccessKey? accessKey = null;
        try
        {
            titleKey = ticket.GetTitleKey(_context.Keyset);
            if (titleKey is { Length: 0x10 })
            {
                accessKey = new AccessKey(titleKey);
            }
        }
        catch (Exception ex) when (ex is CryptographicException or InvalidOperationException)
        {
            _logger.LogDebug(ex, "Failed to decrypt title key for {Path}", metadata.Filename);
        }

        if (titleKey is null)
        {
            if (ticket.TitleKeyBlock?.Length >= 0x10)
            {
                titleKey = new byte[0x10];
                Array.Copy(ticket.TitleKeyBlock, 0, titleKey, 0, 0x10);
                accessKey = new AccessKey(titleKey);
            }
        }

        if (accessKey is not null && ticket.RightsId is { Length: 16 })
        {
            try
            {
                var rightsId = new RightsId(ticket.RightsId);
                Result result = _context.Keyset.ExternalKeySet.Add(rightsId, accessKey.Value);
                if (result.IsFailure())
                {
                    _logger.LogDebug("ExternalKeySet.Add returned {Result} while registering {Path}", result, metadata.Filename);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to register external title key for {Path}", metadata.Filename);
            }
        }

        if (titleKey != null)
        {
            metadata.TitleKey = Convert.ToHexString(titleKey);
        }
    }

    internal static void ApplySystemUpdateFallback(TitleMetadata metadata, LibHac.Ncm.ContentMetaType contentType, uint minimumSystemVersion)
    {
        if (metadata == null)
        {
            throw new ArgumentNullException(nameof(metadata));
        }

        if (metadata.SystemUpdate == uint.MaxValue &&
            contentType == LibHac.Ncm.ContentMetaType.Patch &&
            minimumSystemVersion > 0)
        {
            metadata.SystemUpdate = minimumSystemVersion;
        }
    }

    private bool? TryVerifyNca(Nca nca)
    {
        // Use lightweight header signature validation (matches legacy behaviour)
        // instead of streaming entire partitions through VerifyNca(), which caused hangs.
        var keySet = _context.Keyset;
        var originalMode = keySet.CurrentMode;

        try
        {
            foreach (var mode in EnumerateModes(originalMode))
            {
                keySet.SetMode(mode);

                bool? result = VerifyWithCurrentMode();
                if (result.HasValue)
                {
                    return result;
                }
            }

            return null;
        }
        catch (Exception ex) when (ex is CryptographicException or InvalidOperationException or LibHacException)
        {
            _logger.LogDebug(ex, "Failed to verify NCA signature");
            return null;
        }
        finally
        {
            keySet.SetMode(originalMode);
        }

        bool? VerifyWithCurrentMode()
        {
            bool sawInvalid = false;
            var signingKeys = keySet.NcaHeaderSigningKeyParams;

            for (int i = 0; i < signingKeys.Length; i++)
            {
                byte[]? modulus = signingKeys[i].Modulus;
                if (modulus == null || modulus.Length == 0)
                {
                    continue;
                }

                Validity validity = nca.Header.VerifySignature1(modulus);
                if (validity == Validity.Valid)
                {
                    return true;
                }

                if (validity == Validity.Invalid)
                {
                    sawInvalid = true;
                }
            }

            return sawInvalid ? false : null;
        }

        static IEnumerable<KeySet.Mode> EnumerateModes(KeySet.Mode original)
        {
            if (original == KeySet.Mode.Prod)
            {
                yield return KeySet.Mode.Prod;
                yield return KeySet.Mode.Dev;
            }
            else
            {
                yield return KeySet.Mode.Dev;
                yield return KeySet.Mode.Prod;
            }
        }
    }

    private void PopulatePermissions(TitleMetadata metadata, Nca nca)
    {
        if (metadata.PermissionLevel != TitleMetadata.Permission.Invalid || !nca.CanOpenSection(NcaSectionType.Code))
        {
            return;
        }

        try
        {
            using var codeFileSystem = nca.OpenFileSystem(NcaSectionType.Code, IntegrityCheckLevel.None);

            using var npdmFile = new UniqueRef<IFile>();
            Result result = codeFileSystem.OpenFile(ref npdmFile.Ref, "main.npdm"u8, OpenMode.Read);
            if (result.IsFailure())
            {
                result = codeFileSystem.OpenFile(ref npdmFile.Ref, "/main.npdm"u8, OpenMode.Read);
                if (result.IsFailure())
                {
                    return;
                }
            }

            using var stream = npdmFile.Release().AsStream();
            var npdm = new NpdmBinary(stream, _context.Keyset);

            metadata.PermissionLevel = EvaluatePermissionLevel(npdm);
        }
        catch (MissingKeyException ex)
        {
            GameInfoDiagnostics.AppendError(metadata, GameInfoDiagnostics.FormatMissingKeyMessage(ex));
            _logger.LogDebug(ex, "Missing key while reading NPDM for {Title}", metadata.TitleId);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or LibHacException)
        {
            _logger.LogDebug(ex, "Failed to read NPDM for {Title}", metadata.TitleId);
        }
    }

    private static TitleMetadata.Permission EvaluatePermissionLevel(NpdmBinary npdm)
    {
        var serviceEntries = npdm.AciD?.ServiceAccess?.Services ?? npdm.Aci0?.ServiceAccess?.Services;
        bool hasServices = serviceEntries is { Count: > 0 };
        bool hasFilesystemService = hasServices && serviceEntries!.Any(service =>
            service.Item1.StartsWith("fsp-", StringComparison.OrdinalIgnoreCase));

        if (!hasServices || hasFilesystemService)
        {
            ulong bitmask = npdm.AciD?.FsAccess?.PermissionsBitmask
                            ?? npdm.Aci0?.FsPermissionsBitmask
                            ?? 0;

            if (bitmask == ulong.MaxValue)
            {
                return TitleMetadata.Permission.Dangerous;
            }

            if ((bitmask & 0x8000000000000000UL) != 0)
            {
                return TitleMetadata.Permission.Unsafe;
            }

            return TitleMetadata.Permission.Safe;
        }

        return TitleMetadata.Permission.Safe;
    }

    private void FinalizeMetadata(TitleMetadata metadata)
    {
        if (string.IsNullOrEmpty(metadata.BaseTitleId))
        {
            metadata.BaseTitleId = DeriveBaseTitleId(metadata.TitleId, metadata.Type);
        }

        string lookupId = metadata.Type == LibHac.Ncm.ContentMetaType.AddOnContent
            ? metadata.TitleId
            : metadata.BaseTitleId;

        if (!string.IsNullOrEmpty(lookupId) && _context.VersionList.TryGetValue(lookupId, out uint latest))
        {
            metadata.LatestVersion = latest;
        }

        UpdateLatestVersions(metadata);
    }

    private void UpdateLatestVersions(TitleMetadata metadata)
    {
        string key = metadata.Type == LibHac.Ncm.ContentMetaType.AddOnContent ? metadata.TitleId : metadata.BaseTitleId;
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        if (_latestVersions.TryGetValue(key, out uint existing))
        {
            if (metadata.Version > existing)
            {
                _latestVersions[key] = metadata.Version;
            }
        }
        else
        {
            _latestVersions[key] = metadata.Version;
        }
    }

    private static string FormatId(ulong value) => value.ToString("X16", CultureInfo.InvariantCulture);

    private static string DeriveBaseTitleId(string titleId, LibHac.Ncm.ContentMetaType type)
    {
        if (string.IsNullOrEmpty(titleId))
        {
            return string.Empty;
        }

        return type == LibHac.Ncm.ContentMetaType.AddOnContent
            ? titleId
            : titleId[..13] + "000";
    }

    private static TitleMetadata CreateSkeleton(string path, TitleMetadata.Distribution distribution, long size)
    {
        return new TitleMetadata
        {
            Filename = path,
            FileSize = size,
            DistributionType = distribution
        };
    }

    private void RelayLibHacDiagnostics(string capturedOutput)
    {
        if (string.IsNullOrWhiteSpace(capturedOutput))
        {
            return;
        }

        var segments = capturedOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var raw in segments)
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            _context.RecordDiagnostic(line);
            _logger.LogDebug("LibHac: {Message}", line);
        }
    }
}
