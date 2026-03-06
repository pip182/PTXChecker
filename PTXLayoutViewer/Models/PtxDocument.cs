namespace PTXLayoutViewer.Models;

/// <summary>In-memory PTX document (boards, parts, patterns, cuts).</summary>
public sealed class PtxDocument
{
    public IReadOnlyList<PtxBoard> Boards { get; init; } = Array.Empty<PtxBoard>();
    public IReadOnlyList<PtxPart> Parts { get; init; } = Array.Empty<PtxPart>();
    public IReadOnlyList<PtxPattern> Patterns { get; init; } = Array.Empty<PtxPattern>();
    public IReadOnlyList<PtxCut> Cuts { get; init; } = Array.Empty<PtxCut>();

    public PtxBoard? GetBoard(int brdIndex) =>
        Boards.FirstOrDefault(b => b.BrdIndex == brdIndex);

    public PtxPart? GetPart(int partIndex) =>
        Parts.FirstOrDefault(p => p.PartIndex == partIndex);

    public IReadOnlyList<PtxCut> GetCutsForPattern(int ptnIndex) =>
        Cuts.Where(c => c.PtnIndex == ptnIndex).OrderBy(c => c.Sequence).ToList();
}
