using System;
using System.IO;
using System.Text;

namespace NX.GameInfo.Core.Infrastructure;

/// <summary>
/// Temporarily redirects console output, capturing any writes made by LibHac helper classes.
/// </summary>
public sealed class ConsoleCaptureScope : IDisposable
{
    private sealed class TeeTextWriter : TextWriter
    {
        private readonly TextWriter _primary;
        private readonly TextWriter _secondary;

        public TeeTextWriter(TextWriter primary, TextWriter secondary)
        {
            _primary = primary;
            _secondary = secondary;
        }

        public override Encoding Encoding => _primary.Encoding;

        public override void Flush()
        {
            _primary.Flush();
            _secondary.Flush();
        }

        public override void Write(char value)
        {
            _primary.Write(value);
            _secondary.Write(value);
        }

        public override void Write(string? value)
        {
            _primary.Write(value);
            _secondary.Write(value);
        }

        public override void WriteLine(string? value)
        {
            _primary.WriteLine(value);
            _secondary.WriteLine(value);
        }
    }

    private readonly TextWriter _originalOut;
    private readonly StringWriter _captureWriter;
    private readonly Action<string> _flush;
    private bool _disposed;

    private ConsoleCaptureScope(Action<string> flush)
    {
        _flush = flush;
        _originalOut = Console.Out;
        _captureWriter = new StringWriter();
        Console.SetOut(new TeeTextWriter(_originalOut, _captureWriter));
    }

    public static ConsoleCaptureScope Redirect(Action<string> flush) => new(flush);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        Console.Out.Flush();
        Console.SetOut(_originalOut);

        string payload = _captureWriter.ToString();
        _captureWriter.Dispose();

        if (!string.IsNullOrWhiteSpace(payload))
        {
            _flush(payload);
        }
    }
}
