namespace PTXLayoutViewer.Models;

/// <summary>Pattern from PATTERNS record. Links to board; margins in mm.</summary>
public sealed class PtxPattern
{
    public int JobIndex { get; init; }
    public int PtnIndex { get; init; }
    public int BrdIndex { get; init; }
    public string PatternName { get; init; } = "";
    /// <summary>Margin from reference edge (e.g. left), mm.</summary>
    public double Margin1Mm { get; init; }
    /// <summary>Margin from other edge (e.g. top), mm.</summary>
    public double Margin2Mm { get; init; }
}
