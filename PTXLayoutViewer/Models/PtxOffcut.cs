namespace PTXLayoutViewer.Models;

/// <summary>OFFCUTS record.</summary>
public sealed class PtxOffcut
{
    public int JobIndex { get; init; }
    public int OffcutIndex { get; init; }
    public string Code { get; init; } = "";
    public int MatIndex { get; init; }
    public double LengthMm { get; init; }
    public double WidthMm { get; init; }
    public int Quantity { get; init; }
    public int Grain { get; init; }
    public double Cost { get; init; }
    public int Type { get; init; }
    public string ExtraInformation { get; init; } = "";
    public int CostMethod { get; init; }
}
