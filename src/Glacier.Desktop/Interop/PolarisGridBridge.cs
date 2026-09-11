namespace Glacier.Desktop.Interop;

using System;
using Glacier.Polaris;
using Glacier.Polaris.Data;

/// <summary>
/// High-throughput interoperability bridge creating and transforming Glacier.Polaris DataFrames for desktop grids.
/// </summary>
public static class PolarisGridBridge
{
    /// <summary>
    /// Generates a synthetic columnar DataFrame with N rows for benchmark and virtualization testing.
    /// </summary>
    public static DataFrame CreateSyntheticBenchmarkFrame(int rowCount)
    {
        var idSeries = new Int32Series("ID", rowCount);
        var valSeries = new Float32Series("Value", rowCount);
        var scoreSeries = new Float32Series("Score", rowCount);

        for (int i = 0; i < rowCount; i++)
        {
            idSeries[i] = i + 1;
            valSeries[i] = (float)(i * 1.5);
            scoreSeries[i] = (float)(100.0 - (i % 100));
        }

        return new DataFrame([idSeries, valSeries, scoreSeries]);
    }
}
