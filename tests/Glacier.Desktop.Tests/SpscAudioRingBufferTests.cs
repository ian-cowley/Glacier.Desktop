namespace Glacier.Desktop.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Glacier.Desktop.Audio;
using Xunit;

public class SpscAudioRingBufferTests
{
    [Fact]
    public void Constructor_RoundsUpToPowerOfTwo()
    {
        var rb1 = new SpscAudioRingBuffer(60);
        Assert.Equal(64, rb1.Capacity);

        var rb2 = new SpscAudioRingBuffer(100);
        Assert.Equal(128, rb2.Capacity);

        var rb3 = new SpscAudioRingBuffer(1024);
        Assert.Equal(1024, rb3.Capacity);

        var rb4 = new SpscAudioRingBuffer(5000);
        Assert.Equal(8192, rb4.Capacity);

        Assert.Equal(rb4.Capacity, rb4.AvailableWrite);
        Assert.Equal(0, rb4.AvailableRead);
    }

    [Fact]
    public void SingleThread_WriteAndRead_ExactMatch()
    {
        var rb = new SpscAudioRingBuffer(128);
        float[] source = [0.1f, 0.2f, 0.3f, 0.4f, 0.5f];
        Span<float> dest = stackalloc float[source.Length];

        bool success = rb.TryWrite(source);
        Assert.True(success);
        Assert.Equal(source.Length, rb.AvailableRead);
        Assert.Equal(rb.Capacity - source.Length, rb.AvailableWrite);

        int read = rb.Read(dest);
        Assert.Equal(source.Length, read);

        for (int i = 0; i < source.Length; i++)
        {
            Assert.Equal(source[i], dest[i]);
        }

        Assert.Equal(0, rb.AvailableRead);
        Assert.Equal(rb.Capacity, rb.AvailableWrite);
    }

    [Fact]
    public void WrapAround_ChunkedWritingAndReading_WorksSeamlessly()
    {
        var rb = new SpscAudioRingBuffer(64);

        // Fill 50 samples
        float[] p1 = new float[50];
        for (int i = 0; i < p1.Length; i++) p1[i] = i;
        Assert.True(rb.TryWrite(p1));

        // Read 50 samples
        float[] r1 = new float[50];
        Assert.Equal(50, rb.Read(r1));

        // Now head and tail are at index 50. Writing 30 samples will wrap around index 64 -> 0.
        float[] p2 = new float[30];
        for (int i = 0; i < p2.Length; i++) p2[i] = 100.0f + i;
        Assert.True(rb.TryWrite(p2));

        Assert.Equal(30, rb.AvailableRead);

        float[] r2 = new float[30];
        Assert.Equal(30, rb.Read(r2));

        for (int i = 0; i < p2.Length; i++)
        {
            Assert.Equal(p2[i], r2[i]);
        }
    }

    [Fact]
    public void OverCapacity_TryWriteReturnsFalse_WriteClamps()
    {
        var rb = new SpscAudioRingBuffer(64);
        float[] big = new float[70];

        bool fullSuccess = rb.TryWrite(big);
        Assert.False(fullSuccess);
        Assert.Equal(0, rb.AvailableRead);

        int partialWritten = rb.Write(big);
        Assert.Equal(64, partialWritten);
        Assert.Equal(64, rb.AvailableRead);
        Assert.Equal(0, rb.AvailableWrite);

        // Additional write fails
        Assert.Equal(0, rb.Write(big));
    }

    [Fact]
    public void Clear_ResetsIndices()
    {
        var rb = new SpscAudioRingBuffer(64);
        rb.Write(new float[32]);
        Assert.Equal(32, rb.AvailableRead);

        rb.Clear();
        Assert.Equal(0, rb.AvailableRead);
        Assert.Equal(rb.Capacity, rb.AvailableWrite);
    }

    [Fact]
    public async Task Multithreaded_SPSC_StressTest_ZeroDataLoss()
    {
        const int totalSamples = 1_000_000;
        const int ringCapacity = 4096;
        var rb = new SpscAudioRingBuffer(ringCapacity);

        long consumerSum = 0;
        int consumerReadCount = 0;
        bool consumerError = false;

        var consumer = Task.Run(() =>
        {
            float[] buffer = new float[256];
            float expectedNext = 1.0f;

            while (consumerReadCount < totalSamples)
            {
                int read = rb.Read(buffer);
                if (read == 0)
                {
                    Thread.SpinWait(10);
                    continue;
                }

                for (int i = 0; i < read; i++)
                {
                    if (Math.Abs(buffer[i] - expectedNext) > 0.0001f)
                    {
                        consumerError = true;
                    }
                    expectedNext += 1.0f;
                    consumerSum += (long)buffer[i];
                }
                consumerReadCount += read;
            }
        });

        var producer = Task.Run(() =>
        {
            float[] batch = new float[128];
            int produced = 0;
            float currentVal = 1.0f;

            while (produced < totalSamples)
            {
                int toSend = Math.Min(batch.Length, totalSamples - produced);
                for (int i = 0; i < toSend; i++)
                {
                    batch[i] = currentVal + i;
                }

                int written = rb.Write(batch.AsSpan(0, toSend));
                if (written == 0)
                {
                    Thread.SpinWait(10);
                    continue;
                }

                currentVal += written;
                produced += written;
            }
        });

        await Task.WhenAll(producer, consumer);

        Assert.False(consumerError, "Consumer detected disordered or corrupted samples!");
        Assert.Equal(totalSamples, consumerReadCount);
        Assert.True(consumerSum > 0);
    }
}
