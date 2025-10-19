using LibHac.Common.Keys;
using NX.GameInfo.Core.Models;
using NX.GameInfo.Core.Services;
using Xunit;

namespace NX.GameInfo.Core.Tests;

public sealed class GameInfoDiagnosticsTests
{
    [Fact]
    public void FormatMissingKeyMessage_RewritesApplicationKeyName()
    {
        var exception = new MissingKeyException(
            message: "Missing key",
            name: "key_area_key_application_05",
            keyType: KeyType.Common);

        string message = GameInfoDiagnostics.FormatMissingKeyMessage(exception);

        Assert.Equal("Missing Key: master_key_05", message);
    }

    [Fact]
    public void AppendError_AppendsUniqueMessages()
    {
        var metadata = new TitleMetadata();

        GameInfoDiagnostics.AppendError(metadata, "Missing Key: master_key_00");
        GameInfoDiagnostics.AppendError(metadata, "Missing Key: master_key_00");
        GameInfoDiagnostics.AppendError(metadata, "Unable to read NPDM");

        Assert.Equal($"Missing Key: master_key_00{System.Environment.NewLine}Unable to read NPDM", metadata.Error);
    }
}
