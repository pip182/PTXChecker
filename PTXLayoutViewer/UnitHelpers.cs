namespace PTXLayoutViewer;

/// <summary>Unit conversion and formatting for mm and inches.</summary>
public static class UnitHelpers
{
    public const double MmPerInch = 25.4;

    public static double InchToMm(double inches) => inches * MmPerInch;

    public static double MmToInch(double mm) => mm / MmPerInch;

    /// <summary>Formats length as "X.XX mm (Y.YY in)".</summary>
    public static string FormatMmAndInch(double mm)
    {
        double inch = MmToInch(mm);
        return $"{mm:F2} mm ({inch:F2} in)";
    }
}
