namespace Glacier.Desktop.Tests;

using System;
using Glacier.Desktop.Audio;
using Glacier.Desktop.Audio.Alsa;
using Glacier.Desktop.Audio.CoreAudio;
using Glacier.Desktop.Audio.Simulated;
using Glacier.Desktop.Audio.Wasapi;
using Xunit;

public class WasapiAndAudioTests
{
    [Fact]
    public void SimulatedAudioDevice_PlaybackAndDrain_TrackingCorrect()
    {
        using var audio = new SimulatedAudioDevice(sampleRate: 48000, channels: 2, bufferCapacity: 1024);

        Assert.Equal(48000, audio.SampleRate);
        Assert.Equal(2, audio.Channels);
        Assert.Equal(1024, audio.BufferCapacity);
        Assert.Equal(1.0f, audio.MasterVolume);
        Assert.Equal(0, audio.TotalSamplesPlayed);
        Assert.Equal(0, audio.TotalSamplesWritten);

        float[] stereoSamples = new float[256];
        for (int i = 0; i < stereoSamples.Length; i++)
        {
            stereoSamples[i] = (float)Math.Sin(i * 0.1);
        }

        audio.Play(stereoSamples);
        Assert.Equal(256, audio.TotalSamplesWritten);
        Assert.Equal(256, audio.AvailableRead);

        int drained = audio.Drain(128);
        Assert.Equal(128, drained);
        Assert.Equal(128, audio.TotalSamplesPlayed);
        Assert.Equal(128, audio.AvailableRead);

        int remainder = audio.Drain(200);
        Assert.Equal(128, remainder);
        Assert.Equal(256, audio.TotalSamplesPlayed);
        Assert.Equal(0, audio.AvailableRead);

        audio.SetMasterVolume(0.75f);
        Assert.Equal(0.75f, audio.MasterVolume);

        audio.SetMasterVolume(-0.5f);
        Assert.Equal(0.0f, audio.MasterVolume);

        audio.SetMasterVolume(1.5f);
        Assert.Equal(1.0f, audio.MasterVolume);
    }

    [Fact]
    public void AudioFactory_CreateSimulated_ReturnsFunctionalDevice()
    {
        using var sim = AudioFactory.CreateSimulated(96000, 6, 2048);
        Assert.NotNull(sim);
        Assert.Equal(96000, sim.SampleRate);
        Assert.Equal(6, sim.Channels);
        Assert.Equal(2048, sim.BufferCapacity);

        sim.Start();
        Assert.True(sim.IsRunning);

        sim.Stop();
        Assert.False(sim.IsRunning);
    }

    [Fact]
    public void AudioFactory_CreateDefaultDeviceAndStream_ReturnsValidInstanceOnAnyHost()
    {
        using var device = AudioFactory.CreateDefaultDevice(48000, 2);
        Assert.NotNull(device);
        Assert.Equal(48000, device.SampleRate);
        Assert.Equal(2, device.Channels);
        device.SetMasterVolume(0.5f);
        device.Play(new float[64]);

        using var stream = AudioFactory.CreateStream(48000, 2, 4096);
        Assert.NotNull(stream);
        Assert.Equal(48000, stream.SampleRate);
        Assert.Equal(2, stream.Channels);
        stream.Start();
        Assert.True(stream.Write(new float[64]));
        stream.Stop();
    }

    [Fact]
    public void NativeAudioBackends_EnforcePlatformAndHardwareAvailability()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Throws<PlatformNotSupportedException>(() => new WasapiAudioStream(48000, 2, 4096));
            Assert.Throws<PlatformNotSupportedException>(() => new WasapiAudioDevice(48000, 2));
        }
        else if (WasapiAudioStream.IsSupported)
        {
            using var stream = new WasapiAudioStream(48000, 2, 4096);
            Assert.Equal(48000, stream.SampleRate);
            Assert.Equal(2, stream.Channels);
            Assert.Equal(4096, stream.BufferCapacity);
            stream.Volume = 0.0f; // Mute during automated test run
            stream.Start();
            Assert.True(stream.Write(new float[128]));
            stream.Stop();
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => new WasapiAudioStream(48000, 2, 4096));
        }

        if (!AlsaAudioDevice.IsSupported)
        {
            Assert.Throws<PlatformNotSupportedException>(() => new AlsaAudioDevice(48000, 2));
        }

        if (!CoreAudioDevice.IsSupported)
        {
            Assert.Throws<PlatformNotSupportedException>(() => new CoreAudioDevice(48000, 2));
        }
    }
}
