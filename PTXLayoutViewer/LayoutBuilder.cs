using PTXLayoutViewer.Models;

namespace PTXLayoutViewer;

/// <summary>Reconstructs rough rectangular part layout from CUTS for a pattern.</summary>
public static class LayoutBuilder
{
    /// <summary>Builds layout rectangles for one pattern. Origin top-left; X = length axis, Y = width axis. Rip = Y boundary, Cross = X boundary.</summary>
    public static IReadOnlyList<LayoutRectangle> BuildLayout(PtxDocument doc, int ptnIndex, double kerfMm = 0)
    {
        var board = doc.Patterns.FirstOrDefault(p => p.PtnIndex == ptnIndex) is { } pattern
            ? doc.GetBoard(pattern.BrdIndex)
            : doc.Boards.FirstOrDefault();
        if (board == null) return Array.Empty<LayoutRectangle>();

        var cuts = doc.GetCutsForPattern(ptnIndex);
        if (cuts.Count == 0) return Array.Empty<LayoutRectangle>();

        double marginX = 0, marginY = 0;
        if (doc.Patterns.FirstOrDefault(p => p.PtnIndex == ptnIndex) is { } ptn)
        {
            marginX = ptn.Margin1Mm;
            marginY = ptn.Margin2Mm;
        }

        var result = new List<LayoutRectangle>();
        double ripStart = marginY;
        double ripEnd = marginY;
        double crossStart = marginX;

        foreach (var cut in cuts)
        {
            // Dummy record: dimension=0, qty_rpt=0, part_index≠0 — skip for geometry
            if (cut.DimensionMm <= 0 && cut.QtyRpt == 0 && cut.PartIndex != 0)
                continue;

            if (cut.Function == 1) // Rip: boundary perpendicular to width (Y)
            {
                if (cut.DimensionMm > 0)
                {
                    ripStart = ripEnd;             // Advance to strip starting at previous boundary
                    ripEnd = cut.DimensionMm;     // This strip ends here
                    // Clamp to board so strip and yTop stay within bounds (avoids negative Y / clipped parts)
                    if (ripEnd > board.WidthMm) ripEnd = board.WidthMm;
                    crossStart = marginX;         // Reset cross position for new strip
                }
            }
            else if (cut.Function >= 2 && cut.Function <= 9) // Cross (2) and recut phases (3–9) per spec §5
            {
                if (cut.DimensionMm > 0 && ripEnd > ripStart)
                {
                    double segW = cut.DimensionMm;
                    double segH = ripEnd - ripStart;
                    if (cut.PartIndex > 0 && segW > 0 && segH > 0)
                    {
                        var part = doc.GetPart(cut.PartIndex);
                        // PTX rip DIMENSION is typically from reference edge (bottom); convert to top-origin for display
                        double yTop = board.WidthMm - ripEnd;
                        result.Add(new LayoutRectangle
                        {
                            XMm = crossStart,
                            YMm = yTop,
                            WidthMm = segW,
                            HeightMm = segH,
                            PartIndex = cut.PartIndex,
                            PartName = part?.PartName ?? $"P{cut.PartIndex}"
                        });
                    }
                    crossStart += cut.DimensionMm;
                }
            }
        }

        return result;
    }

    /// <summary>Builds layouts for all patterns in the document.</summary>
    public static IReadOnlyList<(int PtnIndex, IReadOnlyList<LayoutRectangle> Rectangles)> BuildAllLayouts(PtxDocument doc, double kerfMm = 0)
    {
        var patternIndices = doc.Cuts.Select(c => c.PtnIndex).Distinct().ToList();
        if (patternIndices.Count == 0 && doc.Patterns.Count > 0)
            patternIndices = doc.Patterns.Select(p => p.PtnIndex).Distinct().ToList();
        if (patternIndices.Count == 0)
            return Array.Empty<(int, IReadOnlyList<LayoutRectangle>)>();

        return patternIndices
            .Select(ptn => (ptn, Rectangles: BuildLayout(doc, ptn, kerfMm)))
            .Where(x => x.Rectangles.Count > 0)
            .ToList();
    }
}
