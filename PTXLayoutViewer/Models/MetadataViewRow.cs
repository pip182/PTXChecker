namespace PTXLayoutViewer.Models;

/// <summary>Flattened metadata row for the viewer and CSV export.</summary>
public sealed class MetadataViewRow
{
    public string TableName { get; init; } = "";
    public string Scope { get; init; } = "";
    public string RecordKey { get; init; } = "";
    public string Summary { get; init; } = "";
    public string Details { get; init; } = "";
    /// <summary>When set, selecting this row can highlight the corresponding part on the layout.</summary>
    public int? PartIndex { get; init; }
}
