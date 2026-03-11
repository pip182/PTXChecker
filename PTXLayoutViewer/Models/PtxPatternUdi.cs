namespace PTXLayoutViewer.Models;

/// <summary>PTN_UDI record with strip-level matching metadata.</summary>
public sealed class PtxPatternUdi
{
    public int JobIndex { get; init; }
    public int PtnIndex { get; init; }
    public int BrdIndex { get; init; }
    public int StripIndex { get; init; }
    public IReadOnlyList<string> Values { get; init; } = Array.Empty<string>();
}
