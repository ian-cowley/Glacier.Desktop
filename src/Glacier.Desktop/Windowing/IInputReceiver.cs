namespace Glacier.Desktop.Windowing;

/// <summary>
/// Defines the contract for an entity capable of receiving low-latency hardware input events.
/// </summary>
public interface IInputReceiver
{
    /// <summary>
    /// Processes a single hardware input event.
    /// </summary>
    /// <param name="evt">The hardware input event payload.</param>
    void OnInput(in InputEvent evt);
}
