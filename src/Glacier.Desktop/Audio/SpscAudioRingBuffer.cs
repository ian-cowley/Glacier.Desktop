namespace Glacier.Desktop.Audio;

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

/// <summary>
/// High-performance lock-free Single-Producer Single-Consumer (SPSC) ring buffer for audio samples.
/// Utilizes 128-byte cache-line isolation to eliminate false sharing between producer and consumer threads.
/// </summary>
public sealed class SpscAudioRingBuffer
{
    private readonly float[] _buffer;
    private readonly int _mask;
    private readonly int _capacity;

    // Cache-line isolation: Head and Tail live on distinct 128-byte cache lines.
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    private struct PaddedLong
    {
        [FieldOffset(0)]
        public long Value;
    }

    private PaddedLong _tail; // Written by Producer
    private PaddedLong _head; // Written by Consumer

    /// <summary>
    /// Gets the total capacity in float samples (always a power of two).
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// Gets the number of samples available to be read by the consumer.
    /// </summary>
    public int AvailableRead
    {
        get
        {
            long tail = Volatile.Read(ref _tail.Value);
            long head = Volatile.Read(ref _head.Value);
            return (int)(tail - head);
        }
    }

    /// <summary>
    /// Gets the number of samples available to be written by the producer.
    /// </summary>
    public int AvailableWrite
    {
        get
        {
            long head = Volatile.Read(ref _head.Value);
            long tail = Volatile.Read(ref _tail.Value);
            return _capacity - (int)(tail - head);
        }
    }

    /// <summary>
    /// Initializes a new SPSC audio ring buffer with the specified minimum sample capacity.
    /// </summary>
    /// <param name="minCapacity">Minimum number of samples (rounded up to nearest power of two, minimum 64).</param>
    public SpscAudioRingBuffer(int minCapacity = 16384)
    {
        if (minCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minCapacity), "Capacity must be positive.");
        }

        _capacity = (int)BitOperations.RoundUpToPowerOf2((uint)Math.Max(64, minCapacity));
        _mask = _capacity - 1;
        _buffer = new float[_capacity];
    }

    /// <summary>
    /// Attempts to write the entire span of samples into the buffer.
    /// </summary>
    /// <param name="samples">PCM float samples to write.</param>
    /// <returns>True if all samples were written; false if there was insufficient space.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryWrite(ReadOnlySpan<float> samples)
    {
        if (samples.IsEmpty) return true;
        if (samples.Length > AvailableWrite) return false;

        WriteUnchecked(samples);
        return true;
    }

    /// <summary>
    /// Writes as many samples as possible into the buffer without blocking.
    /// </summary>
    /// <param name="samples">PCM float samples to write.</param>
    /// <returns>Actual number of samples written.</returns>
    public int Write(ReadOnlySpan<float> samples)
    {
        if (samples.IsEmpty) return 0;

        int toWrite = Math.Min(samples.Length, AvailableWrite);
        if (toWrite <= 0) return 0;

        WriteUnchecked(samples[..toWrite]);
        return toWrite;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteUnchecked(ReadOnlySpan<float> samples)
    {
        long tail = _tail.Value;
        int startIndex = (int)(tail & _mask);
        int chunk1 = Math.Min(samples.Length, _capacity - startIndex);
        int chunk2 = samples.Length - chunk1;

        samples[..chunk1].CopyTo(_buffer.AsSpan(startIndex, chunk1));
        if (chunk2 > 0)
        {
            samples[chunk1..].CopyTo(_buffer.AsSpan(0, chunk2));
        }

        Volatile.Write(ref _tail.Value, tail + samples.Length);
    }

    /// <summary>
    /// Reads up to destination.Length samples from the buffer into the provided span.
    /// </summary>
    /// <param name="destination">Target span to receive samples.</param>
    /// <returns>Actual number of samples read.</returns>
    public int Read(Span<float> destination)
    {
        if (destination.IsEmpty) return 0;

        int available = AvailableRead;
        int toRead = Math.Min(destination.Length, available);
        if (toRead <= 0) return 0;

        long head = _head.Value;
        int startIndex = (int)(head & _mask);
        int chunk1 = Math.Min(toRead, _capacity - startIndex);
        int chunk2 = toRead - chunk1;

        _buffer.AsSpan(startIndex, chunk1).CopyTo(destination[..chunk1]);
        if (chunk2 > 0)
        {
            _buffer.AsSpan(0, chunk2).CopyTo(destination[chunk1..toRead]);
        }

        Volatile.Write(ref _head.Value, head + toRead);
        return toRead;
    }

    /// <summary>
    /// Clears all pending samples and resets head and tail indices.
    /// </summary>
    public void Clear()
    {
        long currentTail = Volatile.Read(ref _tail.Value);
        Volatile.Write(ref _head.Value, currentTail);
    }
}
