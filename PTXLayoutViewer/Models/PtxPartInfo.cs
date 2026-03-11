namespace PTXLayoutViewer.Models;

/// <summary>PARTS_INF record with the most useful downstream metadata.</summary>
public sealed class PtxPartInfo
{
    public int JobIndex { get; init; }
    public int PartIndex { get; init; }
    public string Description { get; init; } = "";
    public int LabelQuantity { get; init; }
    public double FinishedLengthMm { get; init; }
    public double FinishedWidthMm { get; init; }
    public string Order { get; init; } = "";
    public string Edge1 { get; init; } = "";
    public string Edge2 { get; init; } = "";
    public string Edge3 { get; init; } = "";
    public string Edge4 { get; init; } = "";
    public string FaceLaminate { get; init; } = "";
    public string BackLaminate { get; init; } = "";
    public string Core { get; init; } = "";
    public string Drawing { get; init; } = "";
    public string Product { get; init; } = "";
    public string ProductInfo { get; init; } = "";
    public double ProductWidthMm { get; init; }
    public double ProductHeightMm { get; init; }
    public double ProductDepthMm { get; init; }
    public string ProductNumber { get; init; } = "";
    public string Room { get; init; } = "";
    public string Barcode1 { get; init; } = "";
    public string Barcode2 { get; init; } = "";
    public string Colour { get; init; } = "";
    public double SecondCutLengthMm { get; init; }
    public double SecondCutWidthMm { get; init; }
}
