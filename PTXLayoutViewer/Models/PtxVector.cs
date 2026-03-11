namespace PTXLayoutViewer.Models;

/// <summary>VECTORS record with absolute coordinates.</summary>
public sealed class PtxVector
{
    public int JobIndex { get; init; }
    public int PtnIndex { get; init; }
    public int CutIndex { get; init; }
    public double XStartMm { get; init; }
    public double YStartMm { get; init; }
    public double XEndMm { get; init; }
    public double YEndMm { get; init; }
}
