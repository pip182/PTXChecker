namespace PTXLayoutViewer.Models;

/// <summary>Board stock definition from BOARDS record. Length/Width in mm.</summary>
public sealed class PtxBoard
{
    public int JobIndex { get; init; }
    public int BrdIndex { get; init; }
    public string MaterialCode { get; init; } = "";
    /// <summary>Board length (first dimension) in mm.</summary>
    public double LengthMm { get; init; }
    /// <summary>Board width (second dimension) in mm.</summary>
    public double WidthMm { get; init; }
}
