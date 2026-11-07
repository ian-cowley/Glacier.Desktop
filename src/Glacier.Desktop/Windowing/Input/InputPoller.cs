namespace Glacier.Desktop.Windowing.Input;

using System;
using System.Diagnostics;
using System.Threading;

/// <summary>
/// Ultra-high-frequency (1000Hz+) input poller for sub-millisecond latency polling loops.
/// </summary>
public sealed class InputPoller : IDisposable
{
    private readonly IWindow _window;
    private readonly Thread _pollingThread;
    private readonly double _targetFrequencyHz;
    private readonly long _targetIntervalTicks;
    private volatile bool _isRunning;
    private bool _disposed;
    private long _totalPollCount;

    public long TotalPollCount => _totalPollCount;
    public double TargetFrequencyHz => _targetFrequencyHz;

    public InputPoller(IWindow window, double targetFrequencyHz = 1000.0)
    {
        ArgumentNullException.ThrowIfNull(window);

        _window = window;
        _targetFrequencyHz = Math.Max(1.0, targetFrequencyHz);
        _targetIntervalTicks = (long)(Stopwatch.Frequency / _targetFrequencyHz);

        _pollingThread = new Thread(PollLoop)
        {
            Name = "Glacier.InputPoller.1000Hz",
            IsBackground = true,
            Priority = ThreadPriority.Highest
        };
    }

    /// <summary>
    /// Starts the asynchronous 1000Hz high-frequency polling loop.
    /// </summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_isRunning) return;

        _isRunning = true;
        _pollingThread.Start();
    }

    /// <summary>
    /// Stops the high-frequency polling loop.
    /// </summary>
    public void Stop()
    {
        _isRunning = false;
    }

    /// <summary>
    /// Executes a single synchronous poll tick.
    /// </summary>
    public void PollTick()
    {
        _window.PollEvents();
        Interlocked.Increment(ref _totalPollCount);
    }

    private void PollLoop()
    {
        long nextTick = Stopwatch.GetTimestamp();

        while (_isRunning)
        {
            PollTick();

            nextTick += _targetIntervalTicks;
            long current = Stopwatch.GetTimestamp();
            long waitTicks = nextTick - current;

            if (waitTicks > 0)
            {
                // Spin/Sleep for precise sub-millisecond scheduling
                long waitMs = (waitTicks * 1000) / Stopwatch.Frequency;
                if (waitMs > 1)
                {
                    Thread.Sleep(1);
                }
                else
                {
                    Thread.SpinWait(50);
                }
            }
            else
            {
                // Behind schedule, catch up immediately
                nextTick = current;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
        GC.SuppressFinalize(this);
    }
}
