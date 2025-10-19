using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Fs.Fsa;
using LibHac.Tools.Fs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Mono.Options;
using NX.GameInfo.Cli.Configuration;
using NX.GameInfo.Core.Infrastructure;
using NX.GameInfo.Core.Models;
using NX.GameInfo.Core.Services;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace NX.GameInfo.Cli;

internal static class Program
{
    private static readonly string[] SupportedExtensions = { ".xci", ".nsp", ".nro" };
    private static readonly string[] SupportedCompressedExtensions = { ".xci", ".nsp", ".nro", ".xcz", ".nsz" };

    private static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        PrintBanner();

        var settingsProvider = new CliSettingsProvider();
        var configuration = settingsProvider.Load();
        var settings = configuration.CliSettings ?? new CliSettingsOptions();
        var keysetOptions = configuration.SwitchKeysetOptions ?? new SwitchKeysetOptions();

        bool showHelp = false;
        bool sdCardMode = false;
        bool saveSettings = false;
        bool resetSettings = false;
        bool refreshVersionList = false;
        string sortBy = NormalizeSort(settings.DefaultSort);
        string exportPath = string.Empty;

        var optionSet = new OptionSet
        {
            { "c|sdcard", "open path as sdcard", _ => sdCardMode = true },
            { "s|sort=", "sort by titleid, titlename or filename [default: filename]", v =>
                {
                    if (!string.IsNullOrWhiteSpace(v))
                    {
                        sortBy = NormalizeSort(v);
                    }
                }
            },
            { "x|export=", "export filename, only *.csv or *.xlsx supported", v =>
                {
                    if (!string.IsNullOrEmpty(v))
                    {
                        exportPath = v;
                    }
                }
            },
            { "l|delimiter=", "csv delimiter character [default: ,]", v =>
                {
                    if (!string.IsNullOrEmpty(v))
                    {
                        string trimmed = v.Trim();
                        if (trimmed.Length > 0)
                        {
                            settings.CsvSeparator = trimmed[0].ToString();
                        }
                    }
                }
            },
            { "h|help", "show this help message and exit", _ => showHelp = true },
            { "z|nsz", "enable nsz extension", _ => settings.AllowCompressed = true },
            { "Z|no-nsz", "disable nsz extension", _ => settings.AllowCompressed = false },
            { "d|debug", "enable debug log", _ => settings.DebugLog = true },
            { "D|no-debug", "disable debug log", _ => settings.DebugLog = false },
            { "V|refresh-version-list", "download the latest hac_versionlist.json before scanning", _ => refreshVersionList = true },
            { "k|keys=", "set explicit keys directory (overrides discovery)", v =>
                {
                    if (!string.IsNullOrWhiteSpace(v))
                    {
                        keysetOptions.KeysDirectory = v.Trim();
                    }
                }
            },
            { "S|save-settings", "persist current options as defaults", _ => saveSettings = true },
            { "R|reset-settings", "reset saved settings and exit", _ => resetSettings = true },
        };

        List<string> paths;
        try
        {
            paths = optionSet.Parse(args);
        }
        catch (OptionException ex)
        {
            Console.Error.WriteLine(ex.Message);
            ShowHelp(optionSet, settingsProvider.UserSettingsPath);
            return 1;
        }

        if (resetSettings)
        {
            bool removed = settingsProvider.Reset();
            Console.Error.WriteLine(removed
                ? $"Removed saved CLI settings at {settingsProvider.UserSettingsPath}."
                : "No saved CLI settings were found.");
            return 0;
        }

        if (showHelp || paths.Count == 0)
        {
            ShowHelp(optionSet, settingsProvider.UserSettingsPath);
            return 1;
        }

        sortBy = NormalizeSort(sortBy);
        keysetOptions.EnableDebugLogging = settings.DebugLog;

        if (!string.IsNullOrWhiteSpace(keysetOptions.KeysDirectory))
        {
            try
            {
                string expanded = Environment.ExpandEnvironmentVariables(keysetOptions.KeysDirectory);
                keysetOptions.KeysDirectory = System.IO.Path.GetFullPath(expanded);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.Error.WriteLine($"Invalid keys directory '{keysetOptions.KeysDirectory}': {ex.Message}");
                Console.ResetColor();
                return 1;
            }
        }
        else
        {
            keysetOptions.KeysDirectory = null;
        }

        using var serviceProvider = BuildServiceProvider();

        SwitchKeysetContext context;
        try
        {
            var switchKeysetService = serviceProvider.GetRequiredService<SwitchKeysetService>();
            context = switchKeysetService.LoadAsync(keysetOptions).GetAwaiter().GetResult();
            EmitLibHacDiagnostics(context, settings.DebugLog);

            if (refreshVersionList)
            {
                bool refreshed = switchKeysetService.RefreshVersionListAsync(context, keysetOptions).GetAwaiter().GetResult();
                EmitLibHacDiagnostics(context, settings.DebugLog);
                if (!refreshed)
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.Error.WriteLine("Warning: Failed to refresh hac_versionlist.json; continuing with cached data.");
                    Console.ResetColor();
                }
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.Error.WriteLine($"Failed to load keyset: {ex.Message}");
            Console.ResetColor();
            return 1;
        }

        var processor = ActivatorUtilities.CreateInstance<GameInfoProcessor>(serviceProvider, context);
        var metadataResults = new List<TitleMetadata>();
        bool diagnosticsToConsole = settings.DebugLog;

        foreach (string path in paths)
        {
            if (Directory.Exists(path))
            {
                if (sdCardMode)
                {
                    metadataResults.AddRange(ProcessSdCard(processor, context, path, diagnosticsToConsole));
                }
                else
                {
                    metadataResults.AddRange(ProcessDirectory(processor, context, path, settings, diagnosticsToConsole));
                }
                EmitLibHacDiagnostics(context, diagnosticsToConsole);
            }
            else if (File.Exists(path))
            {
                if (!IsSupportedFile(path, settings.AllowCompressed))
                {
                    WarnUnsupported(path);
                    continue;
                }

                var metadata = processor.ProcessFile(path);
                if (metadata != null)
                {
                    metadataResults.Add(metadata);
                    EmitLibHacDiagnostics(context, diagnosticsToConsole);
                }
            }
            else
            {
                WarnUnsupported(path);
            }
        }

        var distinct = metadataResults
            .GroupBy(m => $"{m.TitleId}|{m.BaseTitleId}|{m.Filename}", StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        List<TitleMetadata> ordered = sortBy switch
        {
            "titleid" => distinct.OrderBy(m => m.TitleId, StringComparer.OrdinalIgnoreCase).ToList(),
            "titlename" => distinct.OrderBy(m => m.TitleName, StringComparer.OrdinalIgnoreCase).ToList(),
            _ => distinct.OrderBy(m => m.Filename, StringComparer.OrdinalIgnoreCase).ToList()
        };

        EmitLibHacDiagnostics(context, diagnosticsToConsole);

        PrintTitles(ordered);

        if (!string.IsNullOrEmpty(exportPath))
        {
            try
            {
                ExportTitles(ordered, exportPath, settings);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.Error.WriteLine($"Export failed: {ex.Message}");
                Console.ResetColor();
                return 1;
            }
        }

        if (saveSettings)
        {
            settings.DefaultSort = sortBy;
            configuration.CliSettings = settings;
            configuration.SwitchKeysetOptions = keysetOptions;

            try
            {
                settingsProvider.Save(configuration);
                Console.Error.WriteLine($"Saved CLI settings to {settingsProvider.UserSettingsPath}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.Error.WriteLine($"Failed to save CLI settings: {ex.Message}");
                Console.ResetColor();
            }
        }

        return 0;
    }

    private static string NormalizeSort(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "titleid" => "titleid",
        "titlename" => "titlename",
        _ => "filename"
    };

    private static IEnumerable<TitleMetadata> ProcessDirectory(GameInfoProcessor processor, SwitchKeysetContext context, string path, CliSettingsOptions settings, bool diagnosticsToConsole)
    {
        var extensions = settings.AllowCompressed ? SupportedCompressedExtensions : SupportedExtensions;
        var files = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
            .Where(file => extensions.Contains(System.IO.Path.GetExtension(file).ToLowerInvariant()))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Console.Error.WriteLine($"Opening {files.Count()} files from directory {path}");

        foreach (string file in files)
        {
            var metadata = processor.ProcessFile(file);
            if (metadata != null)
            {
                yield return metadata;
                EmitLibHacDiagnostics(context, diagnosticsToConsole);
            }
        }
    }

    private static IEnumerable<TitleMetadata> ProcessSdCard(GameInfoProcessor processor, SwitchKeysetContext context, string sdPath, bool diagnosticsToConsole)
    {
        using var baseFs = new UniqueRef<IAttributeFileSystem>(new LocalFileSystem(sdPath));

        if (!context.TryValidateSdCardKeys(out var missingKeys))
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.Error.WriteLine("Cannot open SD card: missing required key material ({0}).",
                string.Join(", ", missingKeys));
            Console.ResetColor();
            yield break;
        }

        SwitchFs switchFs;
        try
        {
            using var capture = ConsoleCaptureScope.Redirect(captured => LogLibHacDiagnostics(context, captured));
            switchFs = SwitchFs.OpenSdCard(context.Keyset, ref baseFs.Ref);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Error.WriteLine($"Could not open SD card at {sdPath}: {ex.Message}");
            Console.ResetColor();
            yield break;
        }

        using (switchFs)
        {
            foreach (var metadata in processor.ProcessSwitchFileSystem(switchFs))
            {
                yield return metadata;
                EmitLibHacDiagnostics(context, diagnosticsToConsole);
            }
        }

        EmitLibHacDiagnostics(context, diagnosticsToConsole);
    }

    private static bool IsSupportedFile(string path, bool allowCompressed)
    {
        string extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
        return allowCompressed ? SupportedCompressedExtensions.Contains(extension) : SupportedExtensions.Contains(extension);
    }

    private static void PrintTitles(IReadOnlyCollection<TitleMetadata> titles)
    {
        foreach (var metadata in titles)
        {
            Console.ForegroundColor = metadata.PermissionLevel switch
            {
                TitleMetadata.Permission.Dangerous => ConsoleColor.DarkRed,
                TitleMetadata.Permission.Unsafe => ConsoleColor.DarkMagenta,
                _ => ConsoleColor.Green
            };

            Console.WriteLine();
            Console.WriteLine(metadata.Filename);
            Console.ResetColor();

            var values = GetPropertyValues(metadata);
            for (int i = 0; i < TitleMetadata.PropertyNames.Count; i++)
            {
                Console.WriteLine(i == TitleMetadata.PropertyNames.Count - 1
                    ? $"└ {TitleMetadata.PropertyNames[i]}: {values[i]}"
                    : $"├ {TitleMetadata.PropertyNames[i]}: {values[i]}");
            }
        }

        Console.Error.WriteLine($"\n{titles.Count} titles processed");
    }

    private static string[] GetPropertyValues(TitleMetadata metadata)
    {
        return new[]
        {
            metadata.TitleId,
            metadata.BaseTitleId,
            metadata.TitleName,
            metadata.DisplayVersion,
            metadata.VersionString,
            metadata.LatestVersionString,
            metadata.SystemUpdateString,
            metadata.SystemVersionString,
            metadata.ApplicationVersionString,
            metadata.MasterKeyString,
            metadata.TitleKey,
            metadata.Publisher,
            metadata.LanguagesString,
            metadata.Filename,
            metadata.FileSizeString,
            metadata.TypeString,
            metadata.DistributionType.ToString(),
            metadata.StructureString,
            metadata.SignatureString,
            metadata.PermissionString,
            metadata.Error
        };
    }

    private static void ExportTitles(IReadOnlyList<TitleMetadata> titles, string path, CliSettingsOptions settings)
    {
        string extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
        if (extension == ".csv")
        {
            ExportCsv(titles, path, settings.GetSeparatorOrDefault());
        }
        else if (extension == ".xlsx")
        {
            ExportExcel(titles, path);
        }
        else
        {
            throw new InvalidOperationException($"Export to {extension} is not supported.");
        }
    }

    private static void ExportCsv(IReadOnlyList<TitleMetadata> titles, string path, char separator)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path) ?? ".");

        using var writer = new StreamWriter(path);

        if (separator != '\0')
        {
            writer.WriteLine($"sep={separator}");
        }

        var info = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location);
        writer.WriteLine(info != null
            ? $"# publisher {info.ProductName} {info.ProductVersion}"
            : $"# publisher {Assembly.GetExecutingAssembly().GetName().Name} {Assembly.GetExecutingAssembly().GetName().Version}");

        writer.WriteLine($"# updated {DateTime.Now:F}");
        writer.WriteLine(string.Join(separator, TitleMetadata.PropertyNames));

        foreach (var metadata in titles)
        {
            string[] values = GetPropertyValues(metadata)
                .Select(value => QuoteIfNeeded(value, separator))
                .ToArray();
            writer.WriteLine(string.Join(separator, values));
        }

        Console.Error.WriteLine($"\n{titles.Count} titles exported to {path}");
    }

    private static void ExportExcel(IReadOnlyList<TitleMetadata> titles, string path)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path) ?? ".");
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Titles");

        worksheet.Cells[1, 1].LoadFromArrays(new[] { TitleMetadata.PropertyNames.ToArray() });

        for (int i = 0; i < titles.Count; i++)
        {
            var metadata = titles[i];
            string[] row = GetPropertyValues(metadata);
            worksheet.Cells[i + 2, 1].LoadFromArrays(new[] { row });

            bool outdated = metadata.LatestVersion != uint.MaxValue && metadata.Version > metadata.LatestVersion;
            bool invalidSignature = metadata.SignatureValid == false;

            if (outdated)
            {
                ApplyFill(worksheet, i + 2, Color.LightYellow);
            }
            else if (invalidSignature)
            {
                ApplyFill(worksheet, i + 2, Color.WhiteSmoke);
            }

            if (metadata.PermissionLevel == TitleMetadata.Permission.Dangerous)
            {
                ApplyFontColor(worksheet, i + 2, Color.DarkRed);
            }
            else if (metadata.PermissionLevel == TitleMetadata.Permission.Unsafe)
            {
                ApplyFontColor(worksheet, i + 2, Color.Indigo);
            }
        }

        using (var range = worksheet.Cells[1, 1, titles.Count + 1, TitleMetadata.PropertyNames.Count])
        {
            range.Style.Border.Top.Style = ExcelBorderStyle.Thin;
            range.Style.Border.Left.Style = ExcelBorderStyle.Thin;
            range.Style.Border.Right.Style = ExcelBorderStyle.Thin;
            range.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
        }

        worksheet.Cells.AutoFitColumns();
        package.SaveAs(new FileInfo(path));

        Console.Error.WriteLine($"\n{titles.Count} titles exported to {path}");
    }

    private static void ApplyFill(ExcelWorksheet worksheet, int row, Color color)
    {
        var range = worksheet.Cells[row, 1, row, TitleMetadata.PropertyNames.Count];
        range.Style.Fill.PatternType = ExcelFillStyle.Solid;
        range.Style.Fill.BackgroundColor.SetColor(color);
    }

    private static void ApplyFontColor(ExcelWorksheet worksheet, int row, Color color)
    {
        var range = worksheet.Cells[row, 1, row, TitleMetadata.PropertyNames.Count];
        range.Style.Font.Color.SetColor(color);
    }

    private static string QuoteIfNeeded(string? value, char separator)
    {
        value ??= string.Empty;
        return value.Contains(separator) || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    private static void ShowHelp(OptionSet optionSet, string settingsPath)
    {
        string exeName = Assembly.GetExecutingAssembly().GetName().Name ?? "nx-game-info-cli";
        Console.Error.WriteLine($"usage: {exeName} [options] paths...\n");
        optionSet.WriteOptionDescriptions(Console.Error);
        Console.Error.WriteLine();
        Console.Error.WriteLine($"Use --save-settings to persist defaults; saved settings live at: {settingsPath}");
    }

    private static void PrintBanner()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var info = FileVersionInfo.GetVersionInfo(assembly.Location);

        if (info != null)
        {
            Console.WriteLine($"{info.ProductName} {info.ProductVersion}");
            Console.WriteLine($"{info.LegalCopyright} {info.CompanyName}");
        }
        else
        {
            Console.WriteLine($"{assembly.GetName().Name} {assembly.GetName().Version}");
        }

        Console.WriteLine();
    }

    private static void EmitLibHacDiagnostics(SwitchKeysetContext context, bool toConsole)
    {
        foreach (var message in context.ConsumeDiagnostics())
        {
            if (toConsole)
            {
                Console.Error.WriteLine($"[libhac] {message}");
            }
        }
    }

    private static void LogLibHacDiagnostics(SwitchKeysetContext context, string captured)
    {
        if (string.IsNullOrWhiteSpace(captured))
        {
            return;
        }

        var segments = captured.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var raw in segments)
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            context.RecordDiagnostic(line);
        }
    }

    private static void WarnUnsupported(string path)
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.Error.WriteLine($"{path} is not supported or not a valid path");
        Console.ResetColor();
    }

    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILogger<SwitchKeysetService>>(_ => NullLogger<SwitchKeysetService>.Instance);
        services.AddSingleton<ILogger<GameInfoProcessor>>(_ => NullLogger<GameInfoProcessor>.Instance);
        services.AddTransient<SwitchKeysetService>();
        services.AddTransient<GameInfoProcessor>();
        return services.BuildServiceProvider();
    }
}
