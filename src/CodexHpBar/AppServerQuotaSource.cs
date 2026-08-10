using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using CodexHpBar.Core;

namespace CodexHpBar;

public sealed class AppServerQuotaSource : IQuotaSource
{
    private readonly ConcurrentDictionary<int, TaskCompletionSource<string>> _pending = new();
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private Process? _process;
    private Task? _readerTask;
    private int _nextId;
    private QuotaSnapshot _current = QuotaSnapshot.Offline();

    public event EventHandler<QuotaSnapshot>? SnapshotChanged;

    public async Task<QuotaSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureStartedAsync(cancellationToken);
            var json = await RequestAsync("account/rateLimits/read", null, cancellationToken);
            if (!QuotaJsonParser.TryParseResponse(json, out var snapshot))
            {
                throw new InvalidDataException("Codex 回傳的額度格式無法辨識");
            }

            _current = snapshot;
            SnapshotChanged?.Invoke(this, snapshot);
            return snapshot;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var stale = _current.OrderedWindows.Count > 0 && DateTimeOffset.UtcNow - _current.UpdatedAt <= TimeSpan.FromMinutes(5)
                ? _current with { IsStale = true, Error = exception.Message }
                : QuotaSnapshot.Offline(exception.Message);
            SnapshotChanged?.Invoke(this, stale);
            return stale;
        }
    }

    private async Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        if (_process is { HasExited: false }) return;
        await _startLock.WaitAsync(cancellationToken);
        try
        {
            if (_process is { HasExited: false }) return;
            var executable = CodexDetector.FindAppServerExecutable() ?? throw new FileNotFoundException("找不到 Codex app-server 執行檔");
            var startInfo = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("app-server");
            startInfo.ArgumentList.Add("--stdio");
            _process = Process.Start(startInfo) ?? throw new InvalidOperationException("無法啟動 Codex app-server");
            _readerTask = Task.Run(() => ReadLoopAsync(_process));
            _ = Task.Run(async () =>
            {
                while (!_process.HasExited) _ = await _process.StandardError.ReadLineAsync();
            });

            _ = await RequestAsync("initialize", new
            {
                clientInfo = new { name = "codex_hp_bar", title = "Codex HP Bar", version = "0.2.0" }
            }, cancellationToken);
            await SendAsync(new { method = "initialized", @params = new { } }, cancellationToken);
        }
        finally
        {
            _startLock.Release();
        }
    }

    private async Task<string> RequestAsync(string method, object? parameters, CancellationToken cancellationToken)
    {
        var id = Interlocked.Increment(ref _nextId);
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = completion;
        await SendAsync(parameters is null ? new { method, id } : new { method, id, @params = parameters }, cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var registration = timeout.Token.Register(() => completion.TrySetCanceled(timeout.Token));
        try
        {
            return await completion.Task;
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    private async Task SendAsync(object payload, CancellationToken cancellationToken)
    {
        if (_process is null) throw new InvalidOperationException("app-server 尚未啟動");
        var json = JsonSerializer.Serialize(payload);
        await _process.StandardInput.WriteLineAsync(json.AsMemory(), cancellationToken);
        await _process.StandardInput.FlushAsync(cancellationToken);
    }

    private async Task ReadLoopAsync(Process process)
    {
        while (!process.HasExited)
        {
            var line = await process.StandardOutput.ReadLineAsync();
            if (line is null) break;
            try
            {
                using var document = JsonDocument.Parse(line);
                if (document.RootElement.TryGetProperty("id", out var idElement) && idElement.TryGetInt32(out var id) && _pending.TryGetValue(id, out var pending))
                {
                    pending.TrySetResult(line);
                    continue;
                }

                if (document.RootElement.TryGetProperty("method", out var method) && method.GetString() == "account/rateLimits/updated" &&
                    QuotaJsonParser.TryParseResponse(line, out var update))
                {
                    _current = QuotaJsonParser.Merge(_current, update);
                    SnapshotChanged?.Invoke(this, _current);
                }
            }
            catch (JsonException)
            {
                // Ignore diagnostics that are not protocol messages.
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_process is not null)
        {
            try { _process.StandardInput.Close(); } catch { }
            if (!_process.HasExited)
            {
                try { _process.Kill(true); } catch { }
            }
            if (_readerTask is not null) await _readerTask.ConfigureAwait(false);
            _process.Dispose();
        }
        _startLock.Dispose();
    }
}
