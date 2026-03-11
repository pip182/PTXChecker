namespace PTXLayoutViewer.Models;

/// <summary>User-defined part data from PARTS_UDI. Keyed by (JobIndex, PartIndex). Values align with canonical field indices 0..29.</summary>
public sealed class PtxPartUdi
{
    public int JobIndex { get; init; }
    public int PartIndex { get; init; }
    /// <summary>UDI values by canonical index (0 = Project Name, 1 = Part Description, …). Missing indices are empty.</summary>
    public IReadOnlyList<string> Values { get; init; } = Array.Empty<string>();

    /// <summary>Canonical PARTS_UDI field labels (index 0..29) for display.</summary>
    public static readonly IReadOnlyList<string> FieldLabels = new[]
    {
        "Project Name",
        "Part Description",
        "Work Order Name",
        "Part File Name",
        "Part Face 6 File Name",
        "Part Machining Picture",
        "Part Face 6 Machining Picture",
        "Edgeband Left",
        "Edgeband Right",
        "Edgeband Top",
        "Edgeband Bottom",
        "Job Number",
        "Part LinkID",
        "File Name",
        "Part Face 6 File Name",
        "Part Machining Picture",
        "Part Face 6 Machining Picture",
        "Part Comments",
        "Part Comments",
        "Part Total Qty",
        "Room Name",
        "Product Name",
        "Product Item Number",
        "Product Qty",
        "Part Material Code",
        "Part Material Code",
        "Part RunField",
        "Part Grain Flag",
        "Part Comments",
        "Project Number"
    };

    /// <summary>Gets the value for a given field index, or empty string if out of range.</summary>
    public string GetValue(int fieldIndex)
    {
        if (fieldIndex < 0 || fieldIndex >= Values.Count) return "";
        var v = Values[fieldIndex];
        return string.IsNullOrWhiteSpace(v) ? "" : v.Trim();
    }
}
