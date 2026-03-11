namespace PTXLayoutViewer.Models;

/// <summary>Cut instruction from CUTS record.</summary>
public sealed class PtxCut
{
    public int JobIndex { get; init; }
    public int PtnIndex { get; init; }
    public int CutIndex { get; init; }
    public int Sequence { get; init; }
    public int Function { get; init; }
    /// <summary>Falling-piece size from the relevant reference edge in mm.</summary>
    public double DimensionMm { get; init; }
    public int QtyRpt { get; init; }
    /// <summary>Resolved numeric PART_INDEX when this cut produces a part; otherwise 0.</summary>
    public int PartIndex { get; init; }
    /// <summary>Original PART_INDEX token, including offcut references like X3.</summary>
    public string PartIndexToken { get; init; } = "";
    public bool IsOffcut { get; init; }
    public int? OffcutIndex { get; init; }
    public int QtyParts { get; init; }
    public string Comment { get; init; } = "";

    public bool IsDummyLabel => DimensionMm <= 0 && QtyRpt == 0 && PartIndex > 0;
    public bool HasGeometry => DimensionMm > 0;
}
