namespace PTXLayoutViewer.Models;

/// <summary>NOTES record.</summary>
public sealed class PtxNote
{
    public int JobIndex { get; init; }
    public int NoteIndex { get; init; }
    public string Text { get; init; } = "";
}
