namespace PTXLayoutViewer.Models;

/// <summary>Part requirement from PARTS_REQ. Dimensions in mm.</summary>
public sealed class PtxPart
{
    public int JobIndex { get; init; }
    public int PartIndex { get; init; }
    public string PartName { get; init; } = "";
    public double WidthMm { get; init; }
    public double HeightMm { get; init; }
    public int QtyReq { get; init; }
}
