namespace PTXLayoutViewer.Models;

/// <summary>Board stock definition from BOARDS record.</summary>
public sealed class PtxBoard
{
    public int JobIndex { get; init; }
    public int BrdIndex { get; init; }
    public string Code { get; init; } = "";
    public int MatIndex { get; init; }
    public double LengthMm { get; init; }
    public double WidthMm { get; init; }
    public int QtyStock { get; init; }
    public int QtyUsed { get; init; }
    public double Cost { get; init; }
    public string Information { get; init; } = "";
    public int Grain { get; init; }
    public int Type { get; init; }
    public int CostMethod { get; init; }

    // Legacy UI compatibility.
    public string MaterialCode => Code;
}
