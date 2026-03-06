namespace PTXLayoutViewer.Models;

/// <summary>Cut instruction from CUTS record. FUNCTION: 1=rip, 2=cross, etc.</summary>
public sealed class PtxCut
{
    public int JobIndex { get; init; }
    public int PtnIndex { get; init; }
    public int CutIndex { get; init; }
    public int Sequence { get; init; }
    /// <summary>1=rip, 2=cross, 3+=recut phases.</summary>
    public int Function { get; init; }
    /// <summary>Distance from reference edge in mm.</summary>
    public double DimensionMm { get; init; }
    public int QtyRpt { get; init; }
    /// <summary>PART_INDEX; 0=waste; X+OFC_INDEX=offcut.</summary>
    public int PartIndex { get; init; }
    public int QtyParts { get; init; }
    public string Comment { get; init; } = "";
}
