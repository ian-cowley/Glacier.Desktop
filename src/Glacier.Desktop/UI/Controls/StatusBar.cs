namespace Glacier.Desktop.UI.Controls;

using System;
using Glacier.Desktop.Layout;
using Glacier.Desktop.UI;
using Glacier.Desktop.UI.Containers;

/// <summary>
/// Bottom desktop status bar displaying status messages and telemetry.
/// </summary>
public class StatusBar : FlexRow
{
    private readonly TextBlock _statusText;

    public string Status
    {
        get => _statusText.Text;
        set => _statusText.Text = value;
    }

    public StatusBar(string initialStatus = "Ready") : base(spacing: 8f)
    {
        BackgroundColor = Color4.HeaderBackground;
        Padding = new Thickness(10f, 4f);
        Height = 24f;

        _statusText = new TextBlock(initialStatus)
        {
            FontSize = 11f,
            TextColor = Color4.TextMuted
        };
        Add(_statusText);
    }
}
