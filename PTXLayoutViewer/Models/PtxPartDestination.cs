namespace PTXLayoutViewer.Models;

/// <summary>PARTS_DST record with stacking and station metadata.</summary>
public sealed class PtxPartDestination
{
    public int JobIndex { get; init; }
    public int PartIndex { get; init; }
    public int PartsPerStackLength { get; init; }
    public int PartsPerStackWidth { get; init; }
    public int PartLayoutOrientation { get; init; }
    public int StackHeightQuantity { get; init; }
    public double StackHeightDimensionMm { get; init; }
    public string Station { get; init; } = "";
    public int QuantityStacks { get; init; }
    public string BottomType { get; init; } = "";
    public string BottomDescription { get; init; } = "";
    public string BottomMaterial { get; init; } = "";
    public double BottomLengthMm { get; init; }
    public double BottomWidthMm { get; init; }
    public double BottomThicknessMm { get; init; }
    public double OverhangLengthMm { get; init; }
    public double OverhangWidthMm { get; init; }
    public string TopType { get; init; } = "";
    public string TopDescription { get; init; } = "";
    public string TopMaterial { get; init; } = "";
    public string SupportType { get; init; } = "";
    public string SupportDescription { get; init; } = "";
    public string SupportMaterial { get; init; } = "";
    public string Station2 { get; init; } = "";
}
