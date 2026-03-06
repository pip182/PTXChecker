using System.Globalization;
using System.IO;
using PTXLayoutViewer.Models;

namespace PTXLayoutViewer;

/// <summary>Parses PTX CSV records into a <see cref="PtxDocument"/>.</summary>
public static class PtxParser
{
    private static readonly char[] Comma = { ',' };

    public static PtxDocument Parse(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        var boards = new List<PtxBoard>();
        var parts = new List<PtxPart>();
        var patterns = new List<PtxPattern>();
        var cuts = new List<PtxCut>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var tokens = line.Split(Comma, StringSplitOptions.None);
            if (tokens.Length == 0) continue;

            var recordType = tokens[0].Trim().ToUpperInvariant();
            if (recordType == "BOARDS" && TryParseBoard(tokens, out var b))
                boards.Add(b);
            else if (recordType == "PARTS_REQ" && TryParsePart(tokens, out var p))
                parts.Add(p);
            else if (recordType == "PATTERNS" && TryParsePattern(tokens, out var pat))
                patterns.Add(pat);
            else if (recordType == "CUTS" && TryParseCut(tokens, out var c))
                cuts.Add(c);
        }

        var doc = new PtxDocument
        {
            Boards = boards,
            Parts = parts,
            Patterns = patterns,
            Cuts = cuts
        };
        return NormalizeDocument(doc);
    }

    /// <summary>Fills in generic BOARDS, PATTERNS, and synthetic CUTS when missing so layout can still be shown.</summary>
    private static PtxDocument NormalizeDocument(PtxDocument doc)
    {
        var boards = doc.Boards.ToList();
        var patterns = doc.Patterns.ToList();
        var cuts = doc.Cuts.ToList();
        int jobIndex = doc.Parts.Count > 0 ? doc.Parts[0].JobIndex : 1;

        if (doc.Parts.Count == 0)
            return doc;

        if (boards.Count == 0)
        {
            double length = 2440;
            double width = 1220;
            if (doc.Parts.Count > 0)
            {
                length = Math.Max(length, doc.Parts.Sum(p => p.WidthMm));
                width = Math.Max(width, doc.Parts.Max(p => p.HeightMm));
            }
            boards.Add(new PtxBoard
            {
                JobIndex = jobIndex,
                BrdIndex = 1,
                MaterialCode = "(generic)",
                LengthMm = length,
                WidthMm = width
            });
        }

        if (patterns.Count == 0 && boards.Count > 0)
        {
            patterns.Add(new PtxPattern
            {
                JobIndex = jobIndex,
                PtnIndex = 1,
                BrdIndex = 1,
                PatternName = "(generic)",
                Margin1Mm = 0,
                Margin2Mm = 0
            });
        }

        if (cuts.Count == 0 && patterns.Count > 0)
        {
            double stripHeight = doc.Parts.Max(p => p.HeightMm);
            cuts.Add(new PtxCut
            {
                JobIndex = jobIndex,
                PtnIndex = 1,
                CutIndex = 1,
                Sequence = 1,
                Function = 1,
                DimensionMm = stripHeight,
                QtyRpt = 1,
                PartIndex = 0,
                QtyParts = 0,
                Comment = "generic rip"
            });
            int cutIndex = 2;
            int sequence = 2;
            foreach (var part in doc.Parts.OrderBy(p => p.PartIndex))
            {
                cuts.Add(new PtxCut
                {
                    JobIndex = jobIndex,
                    PtnIndex = 1,
                    CutIndex = cutIndex++,
                    Sequence = sequence++,
                    Function = 2,
                    DimensionMm = part.WidthMm,
                    QtyRpt = 1,
                    PartIndex = part.PartIndex,
                    QtyParts = 1,
                    Comment = ""
                });
            }
        }

        if (boards.Count == doc.Boards.Count && patterns.Count == doc.Patterns.Count && cuts.Count == doc.Cuts.Count)
            return doc;

        return new PtxDocument
        {
            Boards = boards,
            Parts = doc.Parts,
            Patterns = patterns,
            Cuts = cuts
        };
    }

    // BOARDS: Job_ID, BRD_INDEX, Material_Code, ?, Length, Width, ...
    private static bool TryParseBoard(string[] t, out PtxBoard board)
    {
        board = null!;
        if (t.Length < 7) return false;
        if (!int.TryParse(t[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var job)) return false;
        if (!int.TryParse(t[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var brd)) return false;
        if (!double.TryParse(t[5].Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var length)) return false;
        if (!double.TryParse(t[6].Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var width)) return false;
        board = new PtxBoard { JobIndex = job, BrdIndex = brd, MaterialCode = t[3].Trim(), LengthMm = length, WidthMm = width };
        return true;
    }

    // PARTS_REQ: Job_ID, Part_ID, Part_Seq/Name?, Material_ID?, Width, Height, ...
    private static bool TryParsePart(string[] t, out PtxPart part)
    {
        part = null!;
        if (t.Length < 8) return false;
        if (!int.TryParse(t[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var job)) return false;
        if (!int.TryParse(t[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var partId)) return false;
        if (!double.TryParse(t[5].Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var w)) return false;
        if (!double.TryParse(t[6].Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var h)) return false;
        int qty = 0;
        if (t.Length > 7) int.TryParse(t[7].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out qty);
        part = new PtxPart
        {
            JobIndex = job,
            PartIndex = partId,
            PartName = t.Length > 3 ? t[3].Trim().Trim('"') : "",
            WidthMm = w,
            HeightMm = h,
            QtyReq = qty
        };
        return true;
    }

    // PATTERNS: Job, PTN_INDEX, BRD_INDEX, ..., "Name", Margin1, Margin2, ...
    private static bool TryParsePattern(string[] t, out PtxPattern pattern)
    {
        pattern = null!;
        if (t.Length < 10) return false;
        if (!int.TryParse(t[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var job)) return false;
        if (!int.TryParse(t[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var ptn)) return false;
        if (!int.TryParse(t[3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var brd)) return false;
        var name = t.Length > 8 ? t[8].Trim().Trim('"') : "";
        if (!double.TryParse(t[9].Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var m1)) return false;
        if (!double.TryParse(t[10].Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var m2)) return false;
        pattern = new PtxPattern { JobIndex = job, PtnIndex = ptn, BrdIndex = brd, PatternName = name, Margin1Mm = m1, Margin2Mm = m2 };
        return true;
    }

    // CUTS: ..., JOB, PTN, CUT_INDEX, SEQUENCE, FUNCTION, DIMENSION, QTY_RPT, PART_INDEX, QTY_PARTS, COMMENT
    private static bool TryParseCut(string[] t, out PtxCut cut)
    {
        cut = null!;
        if (t.Length < 10) return false;
        if (!int.TryParse(t[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var job)) return false;
        if (!int.TryParse(t[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var ptn)) return false;
        if (!int.TryParse(t[3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var cutIx)) return false;
        if (!int.TryParse(t[4].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seq)) return false;
        if (!int.TryParse(t[5].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var fn)) return false;
        if (!double.TryParse(t[6].Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var dim)) return false;
        if (!int.TryParse(t[7].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var qtyRpt)) return false;
        var partIndexStr = t[8].Trim();
        var partIndex = 0;
        if (partIndexStr.StartsWith("X", StringComparison.OrdinalIgnoreCase))
            int.TryParse(partIndexStr.AsSpan(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out partIndex);
        else
            int.TryParse(partIndexStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out partIndex);
        if (!int.TryParse(t[9].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var qtyParts)) return false;
        cut = new PtxCut
        {
            JobIndex = job,
            PtnIndex = ptn,
            CutIndex = cutIx,
            Sequence = seq,
            Function = fn,
            DimensionMm = dim,
            QtyRpt = qtyRpt,
            PartIndex = partIndex,
            QtyParts = qtyParts,
            Comment = t.Length > 10 ? t[10].Trim().Trim('"') : ""
        };
        return true;
    }
}
