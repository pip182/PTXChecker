namespace PTXLayoutViewer.Models;

/// <summary>In-memory PTX document.</summary>
public sealed class PtxDocument
{
    public PtxHeader? Header { get; init; }
    public IReadOnlyList<PtxJob> Jobs { get; init; } = Array.Empty<PtxJob>();
    public IReadOnlyList<PtxBoard> Boards { get; init; } = Array.Empty<PtxBoard>();
    public IReadOnlyList<PtxMaterial> Materials { get; init; } = Array.Empty<PtxMaterial>();
    public IReadOnlyList<PtxPart> Parts { get; init; } = Array.Empty<PtxPart>();
    public IReadOnlyList<PtxPartInfo> PartInfos { get; init; } = Array.Empty<PtxPartInfo>();
    public IReadOnlyList<PtxPartUdi> PartUdis { get; init; } = Array.Empty<PtxPartUdi>();
    public IReadOnlyList<PtxPartDestination> PartDestinations { get; init; } = Array.Empty<PtxPartDestination>();
    public IReadOnlyList<PtxNote> Notes { get; init; } = Array.Empty<PtxNote>();
    public IReadOnlyList<PtxOffcut> Offcuts { get; init; } = Array.Empty<PtxOffcut>();
    public IReadOnlyList<PtxPattern> Patterns { get; init; } = Array.Empty<PtxPattern>();
    public IReadOnlyList<PtxPatternUdi> PatternUdis { get; init; } = Array.Empty<PtxPatternUdi>();
    public IReadOnlyList<PtxCut> Cuts { get; init; } = Array.Empty<PtxCut>();
    public IReadOnlyList<PtxVector> Vectors { get; init; } = Array.Empty<PtxVector>();

    public PtxJob? GetJob(int jobIndex) =>
        Jobs.FirstOrDefault(j => j.JobIndex == jobIndex);

    public PtxBoard? GetBoard(int brdIndex) =>
        Boards.FirstOrDefault(b => b.BrdIndex == brdIndex);

    public PtxPattern? GetPattern(int ptnIndex) =>
        Patterns.FirstOrDefault(p => p.PtnIndex == ptnIndex);

    public PtxMaterial? GetMaterial(int matIndex) =>
        Materials.FirstOrDefault(m => m.MatIndex == matIndex);

    public PtxPart? GetPart(int partIndex) =>
        Parts.FirstOrDefault(p => p.PartIndex == partIndex);

    public PtxPartInfo? GetPartInfo(int partIndex)
    {
        var part = GetPart(partIndex);
        var jobIndex = part?.JobIndex ?? 0;
        return PartInfos.FirstOrDefault(i => i.JobIndex == jobIndex && i.PartIndex == partIndex);
    }

    public PtxOffcut? GetOffcut(int offcutIndex) =>
        Offcuts.FirstOrDefault(o => o.OffcutIndex == offcutIndex);

    /// <summary>Gets PARTS_UDI for a part by part index (first matching job when multiple).</summary>
    public PtxPartUdi? GetUdiForPart(int partIndex)
    {
        var part = GetPart(partIndex);
        var jobIndex = part?.JobIndex ?? 0;
        return PartUdis.FirstOrDefault(u => u.JobIndex == jobIndex && u.PartIndex == partIndex);
    }

    public PtxPartDestination? GetDestinationForPart(int partIndex)
    {
        var part = GetPart(partIndex);
        var jobIndex = part?.JobIndex ?? 0;
        return PartDestinations.FirstOrDefault(d => d.JobIndex == jobIndex && d.PartIndex == partIndex);
    }

    public IReadOnlyList<PtxCut> GetCutsForPattern(int ptnIndex) =>
        Cuts.Where(c => c.PtnIndex == ptnIndex).OrderBy(c => c.Sequence).ThenBy(c => c.CutIndex).ToList();

    public IReadOnlyList<PtxPatternUdi> GetPatternUdis(int ptnIndex) =>
        PatternUdis.Where(u => u.PtnIndex == ptnIndex).OrderBy(u => u.StripIndex).ToList();

    public IReadOnlyList<PtxVector> GetVectorsForPattern(int ptnIndex) =>
        Vectors.Where(v => v.PtnIndex == ptnIndex).ToList();

    public IReadOnlyList<PtxVector> GetVectorsForCut(int ptnIndex, int cutIndex) =>
        Vectors.Where(v => v.PtnIndex == ptnIndex && v.CutIndex == cutIndex).ToList();

    public IReadOnlyList<PtxNote> GetNotesForJob(int jobIndex) =>
        Notes.Where(n => n.JobIndex == jobIndex).OrderBy(n => n.NoteIndex).ToList();
}
