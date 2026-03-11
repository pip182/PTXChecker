namespace PTXLayoutViewer.Models;

/// <summary>HEADER record. ORIGIN affects VECTORS coordinate normalization.</summary>
public sealed class PtxHeader
{
    public string Version { get; init; } = "";
    public string Title { get; init; } = "";
    public int Units { get; init; }
    public int Origin { get; init; }
    public int TrimType { get; init; }
}
