using System.Runtime.ExceptionServices;
using System.Threading.Channels;

namespace Terrabuild.EventQueue;

public enum Priority {
    Normal,
    Background
}

public interface IEventQueue : IDisposable {
    public void Enqueue(Priority kind, Action action);
    public ExceptionDispatchInfo? WaitCompletion();
}

public sealed class EventQueue : IEventQueue {
    private readonly int _maxConcurrency;
    private readonly int _backgroundMaxConcurrency;

    private readonly Channel<WorkItem> _normal;
    private readonly Channel<WorkItem> _background;

    private readonly Lock _gate = new();

    private volatile bool _started;
    private int _pending;

    private ExceptionDispatchInfo? _lastError;

    private Task[] _workers = [];
    private readonly TaskCompletionSource _drained =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly CancellationTokenSource _cts = new();

    private readonly struct WorkItem(Priority kind, Action run) {
        public Priority Kind { get; } = kind;
        public Action Run { get; } = run;
    }

    public EventQueue(int maxConcurrency) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxConcurrency);

        _maxConcurrency = maxConcurrency;
        _backgroundMaxConcurrency = 4 * maxConcurrency;

        var options = new UnboundedChannelOptions {
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = true
        };

        _normal = Channel.CreateUnbounded<WorkItem>(options);
        _background = Channel.CreateUnbounded<WorkItem>(options);
    }

    public void Enqueue(Priority kind, Action action) {
        ArgumentNullException.ThrowIfNull(action);

        // Once we have an error, normal work is dropped.
        if (kind == Priority.Normal && Volatile.Read(ref _lastError) != null) {
            return;
        }

        Interlocked.Increment(ref _pending);

        var work = new WorkItem(kind, action);
        var writer = kind == Priority.Normal ? _normal.Writer : _background.Writer;

        // Very fast for unbounded channels
        if (!writer.TryWrite(work)) {
            // Extremely rare for unbounded, but safe fallback
            _ = writer.WriteAsync(work, _cts.Token).AsTask().ContinueWith(_ => {
                // If WriteAsync failed, mark as complete so WaitCompletion doesn't hang.
                DecrementPending();
            }, TaskScheduler.Default);
        }
    }

    public ExceptionDispatchInfo? WaitCompletion() {
        EnsureStarted();

        // Wait until everything accepted has finished
        _drained.Task.GetAwaiter().GetResult();

        // Now close channels so workers exit
        _normal.Writer.TryComplete();
        _background.Writer.TryComplete();

        // Ensure workers are done
        Task.WaitAll(_workers);

        return _lastError;
    }

    private void EnsureStarted() {
        if (_started) {
            return;
        }

        lock (_gate) {
            if (_started) {
                return;
            }

            _started = true;

            // If nothing is pending at the moment we start, we are already drained.
            if (Volatile.Read(ref _pending) == 0) {
                _drained.TrySetResult();
            }

            _workers = StartWorkers();
        }
    }

    private Task[] StartWorkers() {
        var tasks = new Task[_maxConcurrency + _backgroundMaxConcurrency];
        var i = 0;

        for (; i < _maxConcurrency; i++) {
            tasks[i] = Task.Run(() => WorkerLoop(_normal.Reader), _cts.Token);
        }

        for (; i < tasks.Length; i++) {
            tasks[i] = Task.Run(() => WorkerLoop(_background.Reader), _cts.Token);
        }

        return tasks;
    }

    private async Task WorkerLoop(ChannelReader<WorkItem> reader) {
        try {
            while (await reader.WaitToReadAsync(_cts.Token).ConfigureAwait(false)) {
                while (reader.TryRead(out var item)) {
                    Run(item);
                }
            }
        } catch (OperationCanceledException) {
            // Dispose / cancellation
        } catch (ChannelClosedException) {
            // In case someone ever completes the channel with an error
        }
    }

    private void Run(WorkItem item) {
        try {
            item.Run();
        } catch (Exception ex) {
            // any error sets lastError + clears normal queue (stops normal scheduling)
            TrySetError(ex);
        } finally {
            DecrementPending();
        }
    }

    private void TrySetError(Exception ex) {
        // Fast-path: already failed
        if (Volatile.Read(ref _lastError) != null) {
            return;
        }

        lock (_gate) {
            if (_lastError != null) {
                return;
            }

            _lastError = ExceptionDispatchInfo.Capture(ex);

            // IMPORTANT: do not fault the channel (no ex argument),
            // otherwise WaitToReadAsync will throw.
            _normal.Writer.TryComplete();

            // Drop any already enqueued normal tasks (and fix pending count)
            DrainAndDrop(_normal.Reader);
        }
    }

    private void DrainAndDrop(ChannelReader<WorkItem> reader) {
        while (reader.TryRead(out _)) {
            DecrementPending();
        }
    }

    private void DecrementPending() {
        var value = Interlocked.Decrement(ref _pending);
        if (_started && value == 0) {
            _drained.TrySetResult();
        }
    }

    public void Dispose() {
        _cts.Cancel();

        _normal.Writer.TryComplete();
        _background.Writer.TryComplete();

        try {
            if (_workers.Length > 0) {
                Task.WaitAll(_workers);
            }
        } catch {
            // ignore on dispose
        }

        _cts.Dispose();
    }
}

