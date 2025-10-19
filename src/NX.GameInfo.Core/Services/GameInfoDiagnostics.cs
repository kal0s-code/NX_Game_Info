using System;
using System.Diagnostics.CodeAnalysis;
using LibHac.Common.Keys;
using NX.GameInfo.Core.Models;

namespace NX.GameInfo.Core.Services;

/// <summary>
/// Helper routines to keep diagnostic messaging consistent across the processor and adapters.
/// </summary>
internal static class GameInfoDiagnostics
{
    public static void AppendError(TitleMetadata metadata, [StringSyntax("MessageTemplate")] string? message)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (string.IsNullOrEmpty(metadata.Error))
        {
            metadata.Error = message;
            return;
        }

        if (metadata.Error.Contains(message, StringComparison.Ordinal))
        {
            return;
        }

        metadata.Error = $"{metadata.Error}{Environment.NewLine}{message}";
    }

    public static string FormatMissingKeyMessage(MissingKeyException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        string label = exception.Type == KeyType.Title ? "Title Key" : "Key";

        string name = exception.Name ?? string.Empty;
        if (exception.Type == KeyType.Common && !string.IsNullOrEmpty(name))
        {
            name = name.Replace("key_area_key_application", "master_key", StringComparison.OrdinalIgnoreCase);
        }

        return $"Missing {label}: {name}";
    }
}
