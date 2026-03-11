namespace PTXLayoutViewer.Models;

/// <summary>MATERIALS record. Only geometry-relevant fields are modeled.</summary>
public sealed class PtxMaterial
{
    public int JobIndex { get; init; }
    public int MatIndex { get; init; }
    public string Code { get; init; } = "";
    public string Description { get; init; } = "";
    public double ThicknessMm { get; init; }
    public int Book { get; init; }
    public double KerfRipMm { get; init; }
    public double KerfCrossMm { get; init; }
    public double TrimFixedRipMm { get; init; }
    public double TrimVariableRipMm { get; init; }
    public double TrimFixedCrossMm { get; init; }
    public double TrimVariableCrossMm { get; init; }
    public double TrimHeadMm { get; init; }
    public double TrimFixedRecutMm { get; init; }
    public double TrimVariableRecutMm { get; init; }
    public int Rule1 { get; init; }
    public int Rule2 { get; init; }
    public int Rule3 { get; init; }
    public int Rule4 { get; init; }
    public string MaterialParameter { get; init; } = "";
    public int Grain { get; init; }
    public string Picture { get; init; } = "";
    public double Density { get; init; }
}
