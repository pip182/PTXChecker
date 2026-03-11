using PTXLayoutViewer.Models;

namespace PTXLayoutViewer;

/// <summary>Builds approximate rectangular part layout for a pattern.</summary>
public static class LayoutBuilder
{
    private const double Epsilon = 0.01;

    private sealed class WorkRegion
    {
        public double XMm { get; set; }
        public double YMm { get; set; }
        public double WidthMm { get; set; }
        public double HeightMm { get; set; }

        public double RightMm => XMm + WidthMm;
        public double BottomMm => YMm + HeightMm;
        public double Area => WidthMm * HeightMm;
    }

    private sealed class VectorCut
    {
        public int CutIndex { get; init; }
        public bool IsVertical { get; init; }
        public double FixedCoordMm { get; init; }
        public double RangeStartMm { get; init; }
        public double RangeEndMm { get; init; }
    }

    /// <summary>Builds layout rectangles for one pattern. Origin top-left; X = board length axis, Y = board width axis.</summary>
    public static IReadOnlyList<LayoutRectangle> BuildLayout(PtxDocument doc, int ptnIndex, double kerfMm = 0)
    {
        var pattern = doc.GetPattern(ptnIndex);
        var board = pattern is { }
            ? doc.GetBoard(pattern.BrdIndex)
            : doc.Boards.FirstOrDefault();
        if (board == null)
            return Array.Empty<LayoutRectangle>();

        if (TryBuildFromVectors(doc, pattern, board, out var vectorLayout))
            return vectorLayout;

        return BuildFromCuts(doc, pattern, board);
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

    private static bool TryBuildFromVectors(PtxDocument doc, PtxPattern? pattern, PtxBoard board, out IReadOnlyList<LayoutRectangle> rectangles)
    {
        rectangles = Array.Empty<LayoutRectangle>();
        if (pattern == null)
            return false;

        var cuts = doc.GetCutsForPattern(pattern.PtnIndex);
        if (cuts.Count == 0)
            return false;

        var allVectors = doc.GetVectorsForPattern(pattern.PtnIndex)
            .Select(v => NormalizeVector(v, board, doc.Header?.Origin ?? 0))
            .Where(v => v != null)
            .Cast<VectorCut>()
            .GroupBy(v => new { v.CutIndex, v.IsVertical, v.FixedCoordMm, v.RangeStartMm, v.RangeEndMm })
            .Select(g => g.First())
            .ToList();

        if (allVectors.Count == 0)
            return false;

        var activeRegions = new List<WorkRegion>
        {
            new()
            {
                XMm = 0,
                YMm = 0,
                WidthMm = board.LengthMm,
                HeightMm = board.WidthMm
            }
        };

        var result = new List<LayoutRectangle>();
        var cutOrder = cuts
            .Select((cut, index) => new { cut.CutIndex, index })
            .ToDictionary(x => x.CutIndex, x => x.index);

        foreach (var cut in cuts)
        {
            if (cut.IsDummyLabel || !cut.HasGeometry)
                continue;

            var cutVectors = allVectors.Where(v => v.CutIndex == cut.CutIndex).ToList();
            if (cutVectors.Count == 0)
                continue;

            foreach (var vector in cutVectors)
            {
                if (!TryApplyVectorCut(doc, cut, activeRegions, allVectors, cutOrder, vector, result))
                    continue;
            }
        }

        var expectedParts = cuts.Count(c => c.PartIndex > 0 && c.HasGeometry && !c.IsDummyLabel);
        if (expectedParts > 0 && result.Count < expectedParts)
            return false;

        rectangles = result;
        return result.Count > 0;
    }

    private static IReadOnlyList<LayoutRectangle> BuildFromCuts(PtxDocument doc, PtxPattern? pattern, PtxBoard board)
    {
        var cuts = pattern is null ? Array.Empty<PtxCut>() : doc.GetCutsForPattern(pattern.PtnIndex);
        if (cuts.Count == 0)
            return Array.Empty<LayoutRectangle>();

        var phaseRegions = new Dictionary<int, Queue<WorkRegion>>
        {
            [GetPhaseKey(cuts[0].Function)] = new Queue<WorkRegion>(new[]
            {
                new WorkRegion
                {
                    XMm = 0,
                    YMm = 0,
                    WidthMm = board.LengthMm,
                    HeightMm = board.WidthMm
                }
            })
        };

        var result = new List<LayoutRectangle>();
        foreach (var cut in cuts)
        {
            if (cut.IsDummyLabel || !cut.HasGeometry)
                continue;

            var phase = GetPhaseKey(cut.Function);
            if (!phaseRegions.TryGetValue(phase, out var queue) || queue.Count == 0)
                continue;

            var region = queue.Peek();
            if (!TrySplitSequentialRegion(region, cut.Function, cut.DimensionMm, pattern?.Type ?? 0, out var falling, out var retained))
                continue;

            if (cut.PartIndex > 0)
            {
                var part = doc.GetPart(cut.PartIndex);
                var produced = ChoosePartSide(part, falling, retained);
                var continuing = ReferenceEquals(produced, falling) ? retained : falling;

                ApplyContinuingRegion(queue, region, continuing);
                if (produced.WidthMm > Epsilon && produced.HeightMm > Epsilon)
                    AddPartRectangle(doc, cut, produced, result);
                continue;
            }

            ApplyContinuingRegion(queue, region, retained);

            if (falling.WidthMm <= Epsilon || falling.HeightMm <= Epsilon)
                continue;

            var nextPhase = phase + 1;
            if (!phaseRegions.TryGetValue(nextPhase, out var nextQueue))
            {
                nextQueue = new Queue<WorkRegion>();
                phaseRegions[nextPhase] = nextQueue;
            }

            nextQueue.Enqueue(falling);
        }

        return result;
    }

    private static bool TryApplyVectorCut(
        PtxDocument doc,
        PtxCut cut,
        List<WorkRegion> activeRegions,
        IReadOnlyList<VectorCut> allVectors,
        IReadOnlyDictionary<int, int> cutOrder,
        VectorCut vector,
        ICollection<LayoutRectangle> result)
    {
        var regionIndex = FindMatchingRegionIndex(activeRegions, vector);
        if (regionIndex < 0)
            return false;

        var region = activeRegions[regionIndex];
        if (!SplitRegion(region, vector, out var a, out var b))
            return false;

        var part = cut.PartIndex > 0 ? doc.GetPart(cut.PartIndex) : null;
        var currentOrder = cutOrder.TryGetValue(cut.CutIndex, out var value) ? value : int.MaxValue;
        var futureVectors = allVectors
            .Where(v => cutOrder.TryGetValue(v.CutIndex, out var futureOrder) && futureOrder > currentOrder)
            .ToList();

        var aFuture = HasFutureGeometry(a, futureVectors);
        var bFuture = HasFutureGeometry(b, futureVectors);
        var produced = ChooseProducedSide(cut, part, vector, a, b, aFuture, bFuture);
        var retained = ReferenceEquals(produced, a) ? b : a;

        activeRegions.RemoveAt(regionIndex);
        if (ShouldKeepRegion(cut, retained, futureVectors))
            activeRegions.Add(retained);

        if (cut.PartIndex > 0)
            AddPartRectangle(doc, cut, produced, result);

        return true;
    }

    private static bool ShouldKeepRegion(PtxCut cut, WorkRegion region, IReadOnlyList<VectorCut> futureVectors)
    {
        if (region.WidthMm <= Epsilon || region.HeightMm <= Epsilon)
            return false;

        if (cut.PartIndex > 0 || cut.IsOffcut)
            return HasFutureGeometry(region, futureVectors);

        return HasFutureGeometry(region, futureVectors);
    }

    private static void AddPartRectangle(PtxDocument doc, PtxCut cut, WorkRegion piece, ICollection<LayoutRectangle> result)
    {
        var part = doc.GetPart(cut.PartIndex);
        var (layoutWidth, layoutHeight) = GetLayoutSize(part, piece);
        result.Add(new LayoutRectangle
        {
            XMm = piece.XMm,
            YMm = piece.YMm,
            WidthMm = layoutWidth,
            HeightMm = layoutHeight,
            RegionWidthMm = piece.WidthMm,
            RegionHeightMm = piece.HeightMm,
            PartIndex = cut.PartIndex,
            PartName = part?.PartName ?? $"P{cut.PartIndex}"
        });
    }

    private static int FindMatchingRegionIndex(IReadOnlyList<WorkRegion> activeRegions, VectorCut vector)
    {
        var bestIndex = -1;
        var bestScore = double.MaxValue;

        for (var i = 0; i < activeRegions.Count; i++)
        {
            var region = activeRegions[i];
            if (!VectorSplitsRegion(region, vector))
                continue;

            var score = region.Area;
            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static bool VectorSplitsRegion(WorkRegion region, VectorCut vector)
    {
        if (vector.IsVertical)
        {
            return vector.FixedCoordMm > region.XMm + Epsilon &&
                   vector.FixedCoordMm < region.RightMm - Epsilon &&
                   vector.RangeStartMm <= region.YMm + Epsilon &&
                   vector.RangeEndMm >= region.BottomMm - Epsilon;
        }

        return vector.FixedCoordMm > region.YMm + Epsilon &&
               vector.FixedCoordMm < region.BottomMm - Epsilon &&
               vector.RangeStartMm <= region.XMm + Epsilon &&
               vector.RangeEndMm >= region.RightMm - Epsilon;
    }

    private static bool SplitRegion(WorkRegion region, VectorCut vector, out WorkRegion a, out WorkRegion b)
    {
        a = new WorkRegion();
        b = new WorkRegion();

        if (vector.IsVertical)
        {
            var leftWidth = vector.FixedCoordMm - region.XMm;
            var rightWidth = region.RightMm - vector.FixedCoordMm;
            if (leftWidth <= Epsilon || rightWidth <= Epsilon)
                return false;

            a = new WorkRegion { XMm = region.XMm, YMm = region.YMm, WidthMm = leftWidth, HeightMm = region.HeightMm };
            b = new WorkRegion { XMm = vector.FixedCoordMm, YMm = region.YMm, WidthMm = rightWidth, HeightMm = region.HeightMm };
            return true;
        }

        var topHeight = vector.FixedCoordMm - region.YMm;
        var bottomHeight = region.BottomMm - vector.FixedCoordMm;
        if (topHeight <= Epsilon || bottomHeight <= Epsilon)
            return false;

        a = new WorkRegion { XMm = region.XMm, YMm = region.YMm, WidthMm = region.WidthMm, HeightMm = topHeight };
        b = new WorkRegion { XMm = region.XMm, YMm = vector.FixedCoordMm, WidthMm = region.WidthMm, HeightMm = bottomHeight };
        return true;
    }

    private static WorkRegion ChooseProducedSide(
        PtxCut cut,
        PtxPart? part,
        VectorCut vector,
        WorkRegion a,
        WorkRegion b,
        bool aFuture,
        bool bFuture)
    {
        if (!cut.IsOffcut && cut.PartIndex == 0)
        {
            if (aFuture && !bFuture)
                return b;
            if (bFuture && !aFuture)
                return a;
        }

        if (part != null)
        {
            var aFits = RegionCanContainPart(a, part);
            var bFits = RegionCanContainPart(b, part);
            if (aFits && !bFits)
                return a;
            if (bFits && !aFits)
                return b;
            if (aFits && bFits)
            {
                if (!aFuture && bFuture)
                    return a;
                if (!bFuture && aFuture)
                    return b;
            }
        }

        var aScore = ScoreProducedSide(cut, part, vector, a);
        var bScore = ScoreProducedSide(cut, part, vector, b);
        return aScore <= bScore ? a : b;
    }

    private static double ScoreProducedSide(PtxCut cut, PtxPart? part, VectorCut vector, WorkRegion region)
    {
        var extent = vector.IsVertical ? region.WidthMm : region.HeightMm;
        var score = Math.Abs(extent - cut.DimensionMm);

        if (part != null)
        {
            var (layoutWidth, layoutHeight) = GetLayoutSize(part, region);
            if (region.WidthMm + Epsilon < layoutWidth || region.HeightMm + Epsilon < layoutHeight)
                score += 1_000_000;
            score += Math.Abs(region.WidthMm - layoutWidth);
            score += Math.Abs(region.HeightMm - layoutHeight);
        }

        return score;
    }

    private static bool HasFutureGeometry(WorkRegion region, IReadOnlyList<VectorCut> futureVectors)
    {
        return futureVectors.Any(v => VectorSplitsRegion(region, v));
    }

    private static VectorCut? NormalizeVector(PtxVector vector, PtxBoard board, int origin)
    {
        var (x1, y1) = NormalizePoint(vector.XStartMm, vector.YStartMm, board, origin);
        var (x2, y2) = NormalizePoint(vector.XEndMm, vector.YEndMm, board, origin);

        if (Math.Abs(x1 - x2) <= Epsilon)
        {
            var fixedCoord = Clamp(x1, 0, board.LengthMm);
            var start = Clamp(Math.Min(y1, y2), 0, board.WidthMm);
            var end = Clamp(Math.Max(y1, y2), 0, board.WidthMm);
            return new VectorCut
            {
                CutIndex = vector.CutIndex,
                IsVertical = true,
                FixedCoordMm = fixedCoord,
                RangeStartMm = start,
                RangeEndMm = end
            };
        }

        if (Math.Abs(y1 - y2) <= Epsilon)
        {
            var fixedCoord = Clamp(y1, 0, board.WidthMm);
            var start = Clamp(Math.Min(x1, x2), 0, board.LengthMm);
            var end = Clamp(Math.Max(x1, x2), 0, board.LengthMm);
            return new VectorCut
            {
                CutIndex = vector.CutIndex,
                IsVertical = false,
                FixedCoordMm = fixedCoord,
                RangeStartMm = start,
                RangeEndMm = end
            };
        }

        return null;
    }

    private static (double XMm, double YMm) NormalizePoint(double xMm, double yMm, PtxBoard board, int origin)
    {
        return origin switch
        {
            1 => (board.LengthMm - xMm, yMm),
            2 => (xMm, board.WidthMm - yMm),
            3 => (board.LengthMm - xMm, board.WidthMm - yMm),
            _ => (xMm, yMm)
        };
    }

    private static bool TrySplitSequentialRegion(
        WorkRegion region,
        int function,
        double dimensionMm,
        int patternType,
        out WorkRegion falling,
        out WorkRegion retained)
    {
        falling = new WorkRegion();
        retained = new WorkRegion();

        if (region.WidthMm <= Epsilon || region.HeightMm <= Epsilon || dimensionMm <= Epsilon)
            return false;

        var removeAlongX = RemovesAlongX(function, patternType);
        if (!removeAlongX)
        {
            var height = Math.Min(dimensionMm, region.HeightMm);
            if (height <= Epsilon)
                return false;

            falling = new WorkRegion
            {
                XMm = region.XMm,
                YMm = region.YMm,
                WidthMm = region.WidthMm,
                HeightMm = height
            };
            retained = new WorkRegion
            {
                XMm = region.XMm,
                YMm = region.YMm + height,
                WidthMm = region.WidthMm,
                HeightMm = region.HeightMm - height
            };
            return true;
        }

        var width = Math.Min(dimensionMm, region.WidthMm);
        if (width <= Epsilon)
            return false;

        falling = new WorkRegion
        {
            XMm = region.XMm,
            YMm = region.YMm,
            WidthMm = width,
            HeightMm = region.HeightMm
        };
        retained = new WorkRegion
        {
            XMm = region.XMm + width,
            YMm = region.YMm,
            WidthMm = region.WidthMm - width,
            HeightMm = region.HeightMm
        };
        return true;
    }

    private static void ApplyContinuingRegion(Queue<WorkRegion> queue, WorkRegion current, WorkRegion continuing)
    {
        if (continuing.WidthMm <= Epsilon || continuing.HeightMm <= Epsilon)
        {
            queue.Dequeue();
            return;
        }

        current.XMm = continuing.XMm;
        current.YMm = continuing.YMm;
        current.WidthMm = continuing.WidthMm;
        current.HeightMm = continuing.HeightMm;
    }

    private static WorkRegion ChoosePartSide(PtxPart? part, WorkRegion falling, WorkRegion retained)
    {
        if (part == null)
            return falling;

        var fallingFits = RegionCanContainPart(falling, part);
        var retainedFits = RegionCanContainPart(retained, part);
        if (fallingFits && !retainedFits)
            return falling;
        if (retainedFits && !fallingFits)
            return retained;

        var fallingScore = ScorePartRegion(part, falling);
        var retainedScore = ScorePartRegion(part, retained);
        return fallingScore <= retainedScore ? falling : retained;
    }

    private static bool RegionCanContainPart(WorkRegion region, PtxPart part)
    {
        var (layoutWidth, layoutHeight) = GetLayoutSize(part, region);
        return region.WidthMm + Epsilon >= layoutWidth && region.HeightMm + Epsilon >= layoutHeight;
    }

    private static double ScorePartRegion(PtxPart part, WorkRegion region)
    {
        var (layoutWidth, layoutHeight) = GetLayoutSize(part, region);
        var score = Math.Abs(region.WidthMm - layoutWidth) + Math.Abs(region.HeightMm - layoutHeight);
        if (region.WidthMm + Epsilon < layoutWidth || region.HeightMm + Epsilon < layoutHeight)
            score += 1_000_000;
        return score;
    }

    private static bool RemovesAlongX(int function, int patternType)
    {
        if (function == 0)
        {
            return patternType is 2 or 4;
        }

        if (function is 90 or 92)
            return true;

        if (function is 91 or 93)
            return false;

        var phase = GetPhaseKey(function);
        var oddPhaseAlongX = patternType is 1 or 3 or 4;
        return ((phase % 2) == 1) == oddPhaseAlongX;
    }

    private static int GetPhaseKey(int function)
    {
        if (function is >= 90 and <= 93)
            return function - 89;
        if (function <= 0)
            return 1;
        return function;
    }

    private static (double WidthMm, double HeightMm) GetLayoutSize(PtxPart? part, WorkRegion piece)
    {
        if (part == null)
            return (piece.WidthMm, piece.HeightMm);

        var width = part.WidthMm;
        var height = part.HeightMm;
        if (part.GrainDirection == 2)
            (width, height) = (height, width);

        return (width, height);
    }

    private static double Clamp(double value, double min, double max) =>
        Math.Min(max, Math.Max(min, value));
}
