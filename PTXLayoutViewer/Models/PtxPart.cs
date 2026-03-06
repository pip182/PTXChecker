namespace PTXLayoutViewer.Models;

/// <summary>Part requirement from PARTS_REQ. Dimensions in mm. Grain: 0=no grain, 1=along board length, 2=along board width.</summary>
public sealed class PtxPart
{
    public int JobIndex { get; init; }
    public int PartIndex { get; init; }
    public string PartName { get; init; } = "";
    public double WidthMm { get; init; }
    public double HeightMm { get; init; }
    public int QtyReq { get; init; }
    /// <summary>Grain direction: 0 = no grain, 1 = grain along board length (same orientation), 2 = grain along board width (part rotated 90° on board).</summary>
    public int GrainDirection { get; init; }
}
