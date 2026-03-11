namespace PTXLayoutViewer.Models;

/// <summary>Part requirement from PARTS_REQ.</summary>
public sealed class PtxPart
{
    public int JobIndex { get; init; }
    public int PartIndex { get; init; }
    public string Code { get; init; } = "";
    public int MatIndex { get; init; }
    public double WidthMm { get; init; }
    public double HeightMm { get; init; }
    public int QtyReq { get; init; }
    public int QtyOver { get; init; }
    public int QtyUnder { get; init; }
    /// <summary>0 = no grain, 1 = along board length, 2 = along board width.</summary>
    public int GrainDirection { get; init; }
    public int QtyProduced { get; init; }
    public int UnderProducedError { get; init; }
    public int UnderProducedAllowed { get; init; }
    public int UnderProducedPlusPart { get; init; }

    // Legacy UI compatibility.
    public string PartName => Code;
}
