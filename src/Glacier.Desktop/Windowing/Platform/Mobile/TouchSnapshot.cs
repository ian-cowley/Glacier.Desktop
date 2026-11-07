namespace Glacier.Desktop.Windowing.Platform.Mobile;

/// <summary>
/// Status phase of an active touch contact point.
/// </summary>
public enum TouchPhase : byte
{
    Began = 0,
    Moved = 1,
    Stationary = 2,
    Ended = 3,
    Cancelled = 4
}

/// <summary>
/// Low-latency touch snapshot structure suitable for zero-allocation span-based dispatch.
/// </summary>
/// <param name="PointerId">Hardware finger or stylus tracking index.</param>
/// <param name="X">Normalized or coordinate X position.</param>
/// <param name="Y">Normalized or coordinate Y position.</param>
/// <param name="Pressure">Normalized contact pressure (0.0f to 1.0f).</param>
/// <param name="Phase">Current contact phase.</param>
/// <param name="TimestampNs">Nanosecond precision hardware timestamp.</param>
public readonly record struct TouchSnapshot(
    int PointerId,
    float X,
    float Y,
    float Pressure,
    TouchPhase Phase,
    ulong TimestampNs);
