namespace PTXLayoutViewer.Models;

/// <summary>Flattened debug row for inspecting layout reconstruction.</summary>
public sealed class DebugLayoutRow
{
    public int PatternIndex { get; init; }
    public int PartIndex { get; init; }
    public string PartName { get; init; } = "";
    public int GrainDirection { get; init; }
    public double PartWidthMm { get; init; }
    public double PartHeightMm { get; init; }
    public double LayoutWidthMm { get; init; }
    public double LayoutHeightMm { get; init; }
    public double RegionWidthMm { get; init; }
    public double RegionHeightMm { get; init; }
    public double XMm { get; init; }
    public double YMm { get; init; }
    public bool FitsRegion { get; init; }
    public bool FitsBoard { get; init; }
    public string Warning { get; init; } = "";
}
