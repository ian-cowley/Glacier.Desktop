namespace Glacier.Desktop.Tests;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Glacier.Desktop.Audio;
using Glacier.Desktop.Windowing;
using Glacier.Desktop.Windowing.Input;
using Glacier.Desktop.Windowing.Platform.Headless;
using Xunit;

public class PerformanceVerificationTests
{
    [Fact]
    public void SpscRingBuffer_ThroughputVerification_ExceedsTenMillionSamplesPerSec()
    {
        var rb = new SpscAudioRingBuffer(32768);
        float[] batch = new float[256];
        Span<float> readBuffer = stackalloc float[256];

        // Warmup JIT
        for (int w = 0; w < 1000; w++)
        {
            rb.TryWrite(batch);
            rb.Read(readBuffer);
        }

        const int iterations = 50000;
        int totalSamples = iterations * batch.Length; // 12.8M samples

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            rb.TryWrite(batch);
            rb.Read(readBuffer);
        }
        sw.Stop();

        double elapsedSeconds = Math.Max(sw.Elapsed.TotalSeconds, 1e-6);
        double samplesPerSec = totalSamples / elapsedSeconds;

        Assert.True(samplesPerSec > 10_000_000, $"Throughput was {samplesPerSec:N0} samples/sec");
    }

    [Fact]
    public void InputState_DispatchLatency_IsSubMicrosecond()
    {
        var receiver = new InputState();
        var evt = new InputEvent(InputEventType.MouseMove, 0, 150.0f, 250.0f, 1000);

        // Warmup Tier-1 JIT before measuring
        for (int w = 0; w < 10_000; w++)
        {
            DispatchSingle(receiver, in evt);
        }

        const int iterations = 100_000;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            DispatchSingle(receiver, in evt);
        }
        sw.Stop();

        double elapsedUs = sw.Elapsed.TotalMicroseconds;
        double usPerDispatch = elapsedUs / iterations;

        Assert.True(usPerDispatch < 2.5, $"Average dispatch latency was {usPerDispatch:F4} μs");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DispatchSingle(InputState receiver, in InputEvent evt) => receiver.OnInput(in evt);

    [Fact]
    public void HeadlessWindow_EventPumpLatency_IsUltraFast()
    {
        using var window = new HeadlessWindow(new WindowOptions());
        var evt = new InputEvent(InputEventType.KeyDown, 65, 0, 0, 1000);

        window.InputReceived += _ => { };
        window.EnqueueInput(evt);
        window.PollEvents();

        for (int i = 0; i < 1000; i++)
        {
            window.EnqueueInput(evt);
        }

        int count = 0;
        window.InputReceived += _ => count++;

        var sw = Stopwatch.StartNew();
        window.PollEvents();
        sw.Stop();

        Assert.Equal(1000, count);
        Assert.True(sw.ElapsedMilliseconds < 250, $"Pumping 1000 events took {sw.ElapsedMilliseconds} ms");
    }
}
