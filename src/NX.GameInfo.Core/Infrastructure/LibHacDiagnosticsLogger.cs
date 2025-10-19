using System;
using System.Collections.Concurrent;
using LibHac.Common;

namespace NX.GameInfo.Core.Infrastructure;

/// <summary>
/// Collects LibHac diagnostic messages and exposes them via an event/queue.
/// </summary>
public sealed class LibHacDiagnosticsLogger : IProgressReport
{
    private readonly ConcurrentQueue<string> _messages = new();

    public event Action<string>? MessagePublished;

    public void Report(long value) { }

    public void ReportAdd(long value) { }

    public void SetTotal(long value) { }

    public void LogMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _messages.Enqueue(message);
        MessagePublished?.Invoke(message);
    }

    public void DrainTo(Action<string> consumer)
    {
        while (_messages.TryDequeue(out var message))
        {
            consumer(message);
        }
    }
}
