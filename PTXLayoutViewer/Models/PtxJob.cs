namespace PTXLayoutViewer.Models;

/// <summary>JOBS record.</summary>
public sealed class PtxJob
{
    public int JobIndex { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string OrderDate { get; init; } = "";
    public string CutDate { get; init; } = "";
    public string Customer { get; init; } = "";
    public int Status { get; init; }
    public string OptimizerParameter { get; init; } = "";
    public string SawParameter { get; init; } = "";
    public double CutTimeSeconds { get; init; }
    public double WastePercent { get; init; }
}
