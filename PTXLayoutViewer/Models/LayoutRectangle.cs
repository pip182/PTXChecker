namespace PTXLayoutViewer.Models;

/// <summary>One rectangular part placed on the board (mm, origin top-left).</summary>
public sealed class LayoutRectangle
{
    public double XMm { get; init; }
    public double YMm { get; init; }
    public double WidthMm { get; init; }
    public double HeightMm { get; init; }
    public int PartIndex { get; init; }
    public string PartName { get; init; } = "";
}
