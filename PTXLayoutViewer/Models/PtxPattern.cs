namespace PTXLayoutViewer.Models;

/// <summary>Pattern from PATTERNS record.</summary>
public sealed class PtxPattern
{
    public int JobIndex { get; init; }
    public int PtnIndex { get; init; }
    public int BrdIndex { get; init; }
    public int Type { get; init; }
    public int QtyRun { get; init; }
    public int QtyCycles { get; init; }
    public int MaxBook { get; init; }
    public string Picture { get; init; } = "";
    public double CycleTimeSeconds { get; init; }
    public double TotalTimeSeconds { get; init; }
    public string PatternProcessing { get; init; } = "";

    // Legacy UI compatibility.
    public string PatternName => string.IsNullOrWhiteSpace(Picture) ? $"Type {Type}" : Picture;
}
