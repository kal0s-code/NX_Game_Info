using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LibHac.Common.Keys;
using Microsoft.Extensions.Logging.Abstractions;
using NX.GameInfo.Core.Services;
using Xunit;

namespace NX.GameInfo.Core.Tests;

public sealed class SwitchKeysetServiceTests
{
    [Fact]
    public async Task RefreshVersionListAsync_ParsesBlawarFormatAndNormalizesIds()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            const string payload = """
{
  "01006f8002326000": { "65536": "2020-04-28", "983040": "2021-03-18" },
  "01006f8002326800": { "1966080": "2021-11-04" },
  "0100a3d008c5c000": { "0": "2022-11-18", "393216": "2023-02-01" }
}
""";

            using var handler = new StubHttpMessageHandler(payload);
            using var httpClient = new HttpClient(handler);
            using var service = new SwitchKeysetService(NullLogger<SwitchKeysetService>.Instance, httpClient: httpClient);

            var context = new SwitchKeysetContext(new KeySet(), tempDirectory);
            var options = new SwitchKeysetOptions
            {
                VersionListUri = new Uri("https://example.com/titledb/versions.json"),
                VersionListFileName = "versions.json"
            };

            bool refreshed = await service.RefreshVersionListAsync(context, options);

            Assert.True(refreshed);

            string expectedPath = Path.Combine(tempDirectory, "versions.json");
            Assert.True(File.Exists(expectedPath));
            Assert.Equal(expectedPath, context.VersionListPath);

            Assert.True(context.VersionList.TryGetValue("01006F8002326000", out uint animalCrossing));
            Assert.Equal(1966080U, animalCrossing);

            Assert.True(context.VersionList.TryGetValue("0100A3D008C5C000", out uint scarletVersion));
            Assert.Equal(393216U, scarletVersion);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _payload;
        private readonly HttpStatusCode _statusCode;

        public StubHttpMessageHandler(string payload, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _payload = payload;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_payload, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }

    [Fact]
    public void LoadAsync_WithDebugLogging_CompletesWhenWaitedSynchronously()
    {
        using var syncContext = new SingleThreadSynchronizationContext();
        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(syncContext);

        string tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            File.WriteAllText(Path.Combine(tempDirectory, "prod.keys"), "header_key = 00");

            using var service = new SwitchKeysetService(NullLogger<SwitchKeysetService>.Instance);
            var options = new SwitchKeysetOptions
            {
                KeysDirectory = tempDirectory,
                EnableDebugLogging = true
            };

            var loadTask = Task.Run(() => service.LoadAsync(options));
            bool completed = loadTask.Wait(TimeSpan.FromSeconds(5));

            Assert.True(completed, "SwitchKeysetService.LoadAsync did not complete when waited synchronously with debug logging enabled.");
            Assert.NotNull(loadTask.Result.LogWriter);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private sealed class SingleThreadSynchronizationContext : SynchronizationContext, IDisposable
    {
        private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = new();
        private readonly Thread _thread;

        public SingleThreadSynchronizationContext()
        {
            _thread = new Thread(Run)
            {
                IsBackground = true,
                Name = nameof(SingleThreadSynchronizationContext)
            };
            _thread.Start();
        }

        public override void Post(SendOrPostCallback d, object? state)
        {
            if (!_queue.IsAddingCompleted)
            {
                _queue.Add((d, state));
            }
        }

        public override void Send(SendOrPostCallback d, object? state)
        {
            using var evt = new ManualResetEventSlim(false);
            Exception? captured = null;

            Post(s =>
            {
                try
                {
                    d(s);
                }
                catch (Exception ex)
                {
                    captured = ex;
                }
                finally
                {
                    evt.Set();
                }
            }, state);

            evt.Wait();

            if (captured != null)
            {
                throw new AggregateException(captured);
            }
        }

        private void Run()
        {
            SynchronizationContext.SetSynchronizationContext(this);
            foreach (var (callback, state) in _queue.GetConsumingEnumerable())
            {
                callback(state);
            }
        }

        public void Dispose()
        {
            _queue.CompleteAdding();
            _thread.Join();
        }
    }
}
