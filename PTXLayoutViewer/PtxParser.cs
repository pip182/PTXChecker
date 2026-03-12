using System.Globalization;
using Microsoft.VisualBasic.FileIO;
using PTXLayoutViewer.Models;

namespace PTXLayoutViewer;

/// <summary>Parses PTX CSV records into a <see cref="PtxDocument"/>.</summary>
public static class PtxParser
{
    public static PtxDocument Parse(string filePath)
    {
        PtxHeader? header = null;
        var jobs = new List<PtxJob>();
        var boards = new List<PtxBoard>();
        var materials = new List<PtxMaterial>();
        var parts = new List<PtxPart>();
        var partInfos = new List<PtxPartInfo>();
        var partUdis = new List<PtxPartUdi>();
        var partDestinations = new List<PtxPartDestination>();
        var notes = new List<PtxNote>();
        var offcuts = new List<PtxOffcut>();
        var patterns = new List<PtxPattern>();
        var patternUdis = new List<PtxPatternUdi>();
        var cuts = new List<PtxCut>();
        var vectors = new List<PtxVector>();

        foreach (var tokens in ReadCsvRecords(filePath))
        {
            if (tokens.Length == 0)
                continue;

            switch (GetText(tokens, 0).ToUpperInvariant())
            {
                case "HEADER":
                case "PROPERTIES":
                    if (TryParseHeader(tokens, out var parsedHeader))
                        header = parsedHeader;
                    break;
                case "BOARDS":
                    if (TryParseBoard(tokens, out var board))
                        boards.Add(board);
                    break;
                case "JOBS":
                    if (TryParseJob(tokens, out var job))
                        jobs.Add(job);
                    break;
                case "MATERIALS":
                    if (TryParseMaterial(tokens, out var material))
                        materials.Add(material);
                    break;
                case "PARTS_REQ":
                    if (TryParsePart(tokens, out var part))
                        parts.Add(part);
                    break;
                case "PARTS_INF":
                    if (TryParsePartInfo(tokens, out var partInfo))
                        partInfos.Add(partInfo);
                    break;
                case "PARTS_UDI":
                    if (TryParsePartUdi(tokens, out var udi))
                        partUdis.Add(udi);
                    break;
                case "PARTS_DST":
                    if (TryParsePartDestination(tokens, out var destination))
                        partDestinations.Add(destination);
                    break;
                case "NOTES":
                    if (TryParseNote(tokens, out var note))
                        notes.Add(note);
                    break;
                case "OFFCUTS":
                    if (TryParseOffcut(tokens, out var offcut))
                        offcuts.Add(offcut);
                    break;
                case "PATTERNS":
                    if (TryParsePattern(tokens, out var pattern))
                        patterns.Add(pattern);
                    break;
                case "PTN_UDI":
                    if (TryParsePatternUdi(tokens, out var patternUdi))
                        patternUdis.Add(patternUdi);
                    break;
                case "CUTS":
                    if (TryParseCut(tokens, out var cut))
                        cuts.Add(cut);
                    break;
                case "VECTORS":
                    if (TryParseVector(tokens, out var vector))
                        vectors.Add(vector);
                    break;
            }
        }

        var doc = new PtxDocument
        {
            Header = header,
            Jobs = jobs,
            Boards = boards,
            Materials = materials,
            Parts = parts,
            PartInfos = partInfos,
            PartUdis = partUdis,
            PartDestinations = partDestinations,
            Notes = notes,
            Offcuts = offcuts,
            Patterns = patterns,
            PatternUdis = patternUdis,
            Cuts = cuts,
            Vectors = vectors
        };

        return NormalizeDocument(ConvertDocumentUnits(doc));
    }

    private static IEnumerable<string[]> ReadCsvRecords(string filePath)
    {
        using var parser = new TextFieldParser(filePath);
        parser.SetDelimiters(",");
        parser.HasFieldsEnclosedInQuotes = true;
        parser.TrimWhiteSpace = false;

        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields();
            if (fields is { Length: > 0 })
                yield return fields;
        }
    }

    /// <summary>Fills in generic BOARDS, PATTERNS, and synthetic CUTS when missing so layout can still be shown.</summary>
    private static PtxDocument NormalizeDocument(PtxDocument doc)
    {
        var boards = doc.Boards.ToList();
        var patterns = doc.Patterns.ToList();
        var cuts = doc.Cuts.ToList();
        var jobIndex = doc.Parts.Count > 0 ? doc.Parts[0].JobIndex : 1;

        if (doc.Parts.Count == 0)
            return doc;

        if (boards.Count == 0)
        {
            var length = Math.Max(2440, doc.Parts.Sum(p => p.WidthMm));
            var width = Math.Max(1220, doc.Parts.Max(p => p.HeightMm));
            boards.Add(new PtxBoard
            {
                JobIndex = jobIndex,
                BrdIndex = 1,
                Code = "(generic)",
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
                BrdIndex = boards[0].BrdIndex,
                Type = 0,
                QtyRun = 1,
                QtyCycles = 1,
                MaxBook = 1,
                Picture = "(generic)"
            });
        }

        if (cuts.Count == 0 && patterns.Count > 0)
        {
            var stripHeight = doc.Parts.Max(p => p.GrainDirection == 2 ? p.WidthMm : p.HeightMm);
            cuts.Add(new PtxCut
            {
                JobIndex = jobIndex,
                PtnIndex = patterns[0].PtnIndex,
                CutIndex = 1,
                Sequence = 1,
                Function = 1,
                DimensionMm = stripHeight,
                QtyRpt = 1,
                PartIndexToken = "0",
                Comment = "generic rip"
            });

            var cutIndex = 2;
            var sequence = 2;
            foreach (var part in doc.Parts.OrderBy(p => p.PartIndex))
            {
                var crossMm = part.GrainDirection == 2 ? part.HeightMm : part.WidthMm;
                cuts.Add(new PtxCut
                {
                    JobIndex = jobIndex,
                    PtnIndex = patterns[0].PtnIndex,
                    CutIndex = cutIndex++,
                    Sequence = sequence++,
                    Function = 2,
                    DimensionMm = crossMm,
                    QtyRpt = 1,
                    PartIndex = part.PartIndex,
                    PartIndexToken = part.PartIndex.ToString(CultureInfo.InvariantCulture),
                    QtyParts = 1
                });
            }
        }

        if (boards.Count == doc.Boards.Count &&
            patterns.Count == doc.Patterns.Count &&
            cuts.Count == doc.Cuts.Count)
        {
            return doc;
        }

        return new PtxDocument
        {
            Header = doc.Header,
            Jobs = doc.Jobs,
            Boards = boards,
            Materials = doc.Materials,
            Parts = doc.Parts,
            PartInfos = doc.PartInfos,
            PartUdis = doc.PartUdis,
            PartDestinations = doc.PartDestinations,
            Notes = doc.Notes,
            Offcuts = doc.Offcuts,
            Patterns = patterns,
            PatternUdis = doc.PatternUdis,
            Cuts = cuts,
            Vectors = doc.Vectors
        };
    }

    private static PtxDocument ConvertDocumentUnits(PtxDocument doc)
    {
        if (doc.Header?.Units != 1)
            return doc;

        return new PtxDocument
        {
            Header = doc.Header,
            Jobs = doc.Jobs,
            Boards = doc.Boards.Select(board => board.withDimensions(
                lengthMm: UnitHelpers.InchToMm(board.LengthMm),
                widthMm: UnitHelpers.InchToMm(board.WidthMm))).ToList(),
            Materials = doc.Materials.Select(material => material.withDimensions(
                thicknessMm: UnitHelpers.InchToMm(material.ThicknessMm),
                kerfRipMm: UnitHelpers.InchToMm(material.KerfRipMm),
                kerfCrossMm: UnitHelpers.InchToMm(material.KerfCrossMm),
                trimFixedRipMm: UnitHelpers.InchToMm(material.TrimFixedRipMm),
                trimVariableRipMm: UnitHelpers.InchToMm(material.TrimVariableRipMm),
                trimFixedCrossMm: UnitHelpers.InchToMm(material.TrimFixedCrossMm),
                trimVariableCrossMm: UnitHelpers.InchToMm(material.TrimVariableCrossMm),
                trimHeadMm: UnitHelpers.InchToMm(material.TrimHeadMm),
                trimFixedRecutMm: UnitHelpers.InchToMm(material.TrimFixedRecutMm),
                trimVariableRecutMm: UnitHelpers.InchToMm(material.TrimVariableRecutMm))).ToList(),
            Parts = doc.Parts.Select(part => part.withDimensions(
                widthMm: UnitHelpers.InchToMm(part.WidthMm),
                heightMm: UnitHelpers.InchToMm(part.HeightMm))).ToList(),
            PartInfos = doc.PartInfos.Select(info => info.withDimensions(
                finishedLengthMm: UnitHelpers.InchToMm(info.FinishedLengthMm),
                finishedWidthMm: UnitHelpers.InchToMm(info.FinishedWidthMm),
                productWidthMm: UnitHelpers.InchToMm(info.ProductWidthMm),
                productHeightMm: UnitHelpers.InchToMm(info.ProductHeightMm),
                productDepthMm: UnitHelpers.InchToMm(info.ProductDepthMm),
                secondCutLengthMm: UnitHelpers.InchToMm(info.SecondCutLengthMm),
                secondCutWidthMm: UnitHelpers.InchToMm(info.SecondCutWidthMm))).ToList(),
            PartUdis = doc.PartUdis,
            PartDestinations = doc.PartDestinations.Select(destination => destination.withDimensions(
                stackHeightDimensionMm: UnitHelpers.InchToMm(destination.StackHeightDimensionMm),
                bottomLengthMm: UnitHelpers.InchToMm(destination.BottomLengthMm),
                bottomWidthMm: UnitHelpers.InchToMm(destination.BottomWidthMm),
                bottomThicknessMm: UnitHelpers.InchToMm(destination.BottomThicknessMm),
                overhangLengthMm: UnitHelpers.InchToMm(destination.OverhangLengthMm),
                overhangWidthMm: UnitHelpers.InchToMm(destination.OverhangWidthMm))).ToList(),
            Notes = doc.Notes,
            Offcuts = doc.Offcuts.Select(offcut => offcut.withDimensions(
                lengthMm: UnitHelpers.InchToMm(offcut.LengthMm),
                widthMm: UnitHelpers.InchToMm(offcut.WidthMm))).ToList(),
            Patterns = doc.Patterns,
            PatternUdis = doc.PatternUdis,
            Cuts = doc.Cuts.Select(cut => cut.withDimension(UnitHelpers.InchToMm(cut.DimensionMm))).ToList(),
            Vectors = doc.Vectors.Select(vector => vector.withCoordinates(
                xStartMm: UnitHelpers.InchToMm(vector.XStartMm),
                yStartMm: UnitHelpers.InchToMm(vector.YStartMm),
                xEndMm: UnitHelpers.InchToMm(vector.XEndMm),
                yEndMm: UnitHelpers.InchToMm(vector.YEndMm))).ToList()
        };
    }

    private static bool TryParseHeader(string[] t, out PtxHeader header)
    {
        header = new PtxHeader
        {
            Version = GetText(t, 1),
            Title = GetText(t, 2),
            Units = GetInt(t, 3),
            Origin = GetInt(t, 4),
            TrimType = GetInt(t, 5)
        };
        return !string.IsNullOrWhiteSpace(header.Version);
    }

    private static bool TryParseJob(string[] t, out PtxJob job)
    {
        job = null!;
        if (!TryGetInt(t, 1, out var jobIndex))
            return false;

        job = new PtxJob
        {
            JobIndex = jobIndex,
            Name = GetText(t, 2),
            Description = GetText(t, 3),
            OrderDate = GetText(t, 4),
            CutDate = GetText(t, 5),
            Customer = GetText(t, 6),
            Status = GetInt(t, 7),
            OptimizerParameter = GetText(t, 8),
            SawParameter = GetText(t, 9),
            CutTimeSeconds = GetDouble(t, 10),
            WastePercent = GetDouble(t, 11)
        };
        return true;
    }

    private static bool TryParsePartUdi(string[] t, out PtxPartUdi udi)
    {
        udi = null!;
        if (!TryGetInt(t, 1, out var job) || !TryGetInt(t, 2, out var partId))
            return false;

        var values = new List<string>();
        for (var i = 3; i < t.Length && values.Count < PtxPartUdi.FieldLabels.Count; i++)
            values.Add(GetText(t, i));

        udi = new PtxPartUdi
        {
            JobIndex = job,
            PartIndex = partId,
            Values = values
        };
        return true;
    }

    private static bool TryParseBoard(string[] t, out PtxBoard board)
    {
        board = null!;
        if (!TryGetInt(t, 1, out var job) ||
            !TryGetInt(t, 2, out var brd) ||
            !TryGetDouble(t, 5, out var length) ||
            !TryGetDouble(t, 6, out var width))
        {
            return false;
        }

        board = new PtxBoard
        {
            JobIndex = job,
            BrdIndex = brd,
            Code = GetText(t, 3),
            MatIndex = GetInt(t, 4),
            LengthMm = length,
            WidthMm = width,
            QtyStock = GetInt(t, 7),
            QtyUsed = GetInt(t, 8),
            Cost = GetDouble(t, 9),
            Information = GetText(t, 11),
            Grain = GetInt(t, 13),
            Type = GetInt(t, 14),
            CostMethod = GetInt(t, 18)
        };
        return true;
    }

    private static bool TryParseMaterial(string[] t, out PtxMaterial material)
    {
        material = null!;
        if (!TryGetInt(t, 1, out var job) || !TryGetInt(t, 2, out var matIndex))
            return false;

        material = new PtxMaterial
        {
            JobIndex = job,
            MatIndex = matIndex,
            Code = GetText(t, 3),
            Description = GetText(t, 4),
            ThicknessMm = GetDouble(t, 5),
            Book = GetInt(t, 6),
            KerfRipMm = GetDouble(t, 7),
            KerfCrossMm = GetDouble(t, 8),
            TrimFixedRipMm = GetDouble(t, 9),
            TrimVariableRipMm = GetDouble(t, 10),
            TrimFixedCrossMm = GetDouble(t, 11),
            TrimVariableCrossMm = GetDouble(t, 12),
            TrimHeadMm = GetDouble(t, 13),
            TrimFixedRecutMm = GetDouble(t, 14),
            TrimVariableRecutMm = GetDouble(t, 15),
            Rule1 = GetInt(t, 16),
            Rule2 = GetInt(t, 17),
            Rule3 = GetInt(t, 18),
            Rule4 = GetInt(t, 19),
            MaterialParameter = GetText(t, 20),
            Grain = GetInt(t, 21),
            Picture = GetText(t, 22),
            Density = GetDouble(t, 23)
        };
        return true;
    }

    private static bool TryParsePart(string[] t, out PtxPart part)
    {
        part = null!;
        if (!TryGetInt(t, 1, out var job) ||
            !TryGetInt(t, 2, out var partId) ||
            !TryGetDouble(t, 5, out var length) ||
            !TryGetDouble(t, 6, out var width))
        {
            return false;
        }

        part = new PtxPart
        {
            JobIndex = job,
            PartIndex = partId,
            Code = GetText(t, 3),
            MatIndex = GetInt(t, 4),
            WidthMm = length,
            HeightMm = width,
            QtyReq = GetInt(t, 7),
            QtyOver = GetInt(t, 8),
            QtyUnder = GetInt(t, 9),
            GrainDirection = GetInt(t, 10),
            QtyProduced = GetInt(t, 11),
            UnderProducedError = GetInt(t, 12),
            UnderProducedAllowed = GetInt(t, 13),
            UnderProducedPlusPart = GetInt(t, 14)
        };
        return true;
    }

    private static bool TryParsePartInfo(string[] t, out PtxPartInfo partInfo)
    {
        partInfo = null!;
        if (!TryGetInt(t, 1, out var job) || !TryGetInt(t, 2, out var partIndex))
            return false;

        partInfo = new PtxPartInfo
        {
            JobIndex = job,
            PartIndex = partIndex,
            Description = GetText(t, 3),
            LabelQuantity = GetInt(t, 4),
            FinishedLengthMm = GetDouble(t, 5),
            FinishedWidthMm = GetDouble(t, 6),
            Order = GetText(t, 7),
            Edge1 = GetText(t, 8),
            Edge2 = GetText(t, 9),
            Edge3 = GetText(t, 10),
            Edge4 = GetText(t, 11),
            FaceLaminate = GetText(t, 16),
            BackLaminate = GetText(t, 17),
            Core = GetText(t, 18),
            Drawing = GetText(t, 19),
            Product = GetText(t, 20),
            ProductInfo = GetText(t, 21),
            ProductWidthMm = GetDouble(t, 22),
            ProductHeightMm = GetDouble(t, 23),
            ProductDepthMm = GetDouble(t, 24),
            ProductNumber = GetText(t, 25),
            Room = GetText(t, 26),
            Barcode1 = GetText(t, 27),
            Barcode2 = GetText(t, 28),
            Colour = GetText(t, 29),
            SecondCutLengthMm = GetDouble(t, 30),
            SecondCutWidthMm = GetDouble(t, 31)
        };
        return true;
    }

    private static bool TryParseOffcut(string[] t, out PtxOffcut offcut)
    {
        offcut = null!;
        if (!TryGetInt(t, 1, out var job) ||
            !TryGetInt(t, 2, out var offcutIndex) ||
            !TryGetDouble(t, 5, out var length) ||
            !TryGetDouble(t, 6, out var width))
        {
            return false;
        }

        offcut = new PtxOffcut
        {
            JobIndex = job,
            OffcutIndex = offcutIndex,
            Code = GetText(t, 3),
            MatIndex = GetInt(t, 4),
            LengthMm = length,
            WidthMm = width,
            Quantity = GetInt(t, 7),
            Grain = GetInt(t, 8),
            Cost = GetDouble(t, 9),
            Type = GetInt(t, 10),
            ExtraInformation = GetText(t, 11),
            CostMethod = GetInt(t, 12)
        };
        return true;
    }

    private static bool TryParsePartDestination(string[] t, out PtxPartDestination destination)
    {
        destination = null!;
        if (!TryGetInt(t, 1, out var job) || !TryGetInt(t, 2, out var partIndex))
            return false;

        destination = new PtxPartDestination
        {
            JobIndex = job,
            PartIndex = partIndex,
            PartsPerStackLength = GetInt(t, 3),
            PartsPerStackWidth = GetInt(t, 4),
            PartLayoutOrientation = GetInt(t, 5),
            StackHeightQuantity = GetInt(t, 6),
            StackHeightDimensionMm = GetDouble(t, 7),
            Station = GetText(t, 8),
            QuantityStacks = GetInt(t, 9),
            BottomType = GetText(t, 10),
            BottomDescription = GetText(t, 11),
            BottomMaterial = GetText(t, 12),
            BottomLengthMm = GetDouble(t, 13),
            BottomWidthMm = GetDouble(t, 14),
            BottomThicknessMm = GetDouble(t, 15),
            OverhangLengthMm = GetDouble(t, 16),
            OverhangWidthMm = GetDouble(t, 17),
            TopType = GetText(t, 20),
            TopDescription = GetText(t, 21),
            TopMaterial = GetText(t, 22),
            SupportType = GetText(t, 28),
            SupportDescription = GetText(t, 29),
            SupportMaterial = GetText(t, 30),
            Station2 = GetText(t, 36)
        };
        return true;
    }

    private static bool TryParsePattern(string[] t, out PtxPattern pattern)
    {
        pattern = null!;
        if (!TryGetInt(t, 1, out var job) ||
            !TryGetInt(t, 2, out var ptn) ||
            !TryGetInt(t, 3, out var brd))
        {
            return false;
        }

        pattern = new PtxPattern
        {
            JobIndex = job,
            PtnIndex = ptn,
            BrdIndex = brd,
            Type = GetInt(t, 4),
            QtyRun = GetInt(t, 5),
            QtyCycles = GetInt(t, 6),
            MaxBook = GetInt(t, 7),
            Picture = GetText(t, 8),
            CycleTimeSeconds = GetDouble(t, 9),
            TotalTimeSeconds = GetDouble(t, 10),
            PatternProcessing = GetText(t, 11)
        };
        return true;
    }

    private static bool TryParsePatternUdi(string[] t, out PtxPatternUdi patternUdi)
    {
        patternUdi = null!;
        if (!TryGetInt(t, 1, out var job) ||
            !TryGetInt(t, 2, out var patternIndex) ||
            !TryGetInt(t, 3, out var boardIndex) ||
            !TryGetInt(t, 4, out var stripIndex))
        {
            return false;
        }

        var values = new List<string>();
        for (var i = 5; i < t.Length; i++)
            values.Add(GetText(t, i));

        patternUdi = new PtxPatternUdi
        {
            JobIndex = job,
            PtnIndex = patternIndex,
            BrdIndex = boardIndex,
            StripIndex = stripIndex,
            Values = values
        };
        return true;
    }

    private static bool TryParseCut(string[] t, out PtxCut cut)
    {
        cut = null!;
        if (!TryGetInt(t, 1, out var job) ||
            !TryGetInt(t, 2, out var ptn) ||
            !TryGetInt(t, 3, out var cutIndex) ||
            !TryGetInt(t, 5, out var function) ||
            !TryGetDouble(t, 6, out var dimension) ||
            !TryGetInt(t, 7, out var qtyRpt))
        {
            return false;
        }

        var sequence = GetInt(t, 4);
        if (sequence <= 0)
            sequence = cutIndex;

        var token = GetText(t, 8);
        var offcutIndex = 0;
        var isOffcut = token.StartsWith("X", StringComparison.OrdinalIgnoreCase) &&
                       int.TryParse(token.AsSpan(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out offcutIndex);

        var partIndex = 0;
        if (!isOffcut)
            int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out partIndex);

        cut = new PtxCut
        {
            JobIndex = job,
            PtnIndex = ptn,
            CutIndex = cutIndex,
            Sequence = sequence,
            Function = function,
            DimensionMm = dimension,
            QtyRpt = qtyRpt,
            PartIndex = partIndex,
            PartIndexToken = token,
            IsOffcut = isOffcut,
            OffcutIndex = isOffcut ? offcutIndex : null,
            QtyParts = GetInt(t, 9),
            Comment = GetText(t, 10)
        };
        return true;
    }

    private static PtxBoard withDimensions(this PtxBoard board, double lengthMm, double widthMm) =>
        new()
        {
            JobIndex = board.JobIndex,
            BrdIndex = board.BrdIndex,
            Code = board.Code,
            MatIndex = board.MatIndex,
            LengthMm = lengthMm,
            WidthMm = widthMm,
            QtyStock = board.QtyStock,
            QtyUsed = board.QtyUsed,
            Cost = board.Cost,
            Information = board.Information,
            Grain = board.Grain,
            Type = board.Type,
            CostMethod = board.CostMethod
        };

    private static PtxMaterial withDimensions(
        this PtxMaterial material,
        double thicknessMm,
        double kerfRipMm,
        double kerfCrossMm,
        double trimFixedRipMm,
        double trimVariableRipMm,
        double trimFixedCrossMm,
        double trimVariableCrossMm,
        double trimHeadMm,
        double trimFixedRecutMm,
        double trimVariableRecutMm) =>
        new()
        {
            JobIndex = material.JobIndex,
            MatIndex = material.MatIndex,
            Code = material.Code,
            Description = material.Description,
            ThicknessMm = thicknessMm,
            Book = material.Book,
            KerfRipMm = kerfRipMm,
            KerfCrossMm = kerfCrossMm,
            TrimFixedRipMm = trimFixedRipMm,
            TrimVariableRipMm = trimVariableRipMm,
            TrimFixedCrossMm = trimFixedCrossMm,
            TrimVariableCrossMm = trimVariableCrossMm,
            TrimHeadMm = trimHeadMm,
            TrimFixedRecutMm = trimFixedRecutMm,
            TrimVariableRecutMm = trimVariableRecutMm,
            Rule1 = material.Rule1,
            Rule2 = material.Rule2,
            Rule3 = material.Rule3,
            Rule4 = material.Rule4,
            MaterialParameter = material.MaterialParameter,
            Grain = material.Grain,
            Picture = material.Picture,
            Density = material.Density
        };

    private static PtxPart withDimensions(this PtxPart part, double widthMm, double heightMm) =>
        new()
        {
            JobIndex = part.JobIndex,
            PartIndex = part.PartIndex,
            Code = part.Code,
            MatIndex = part.MatIndex,
            WidthMm = widthMm,
            HeightMm = heightMm,
            QtyReq = part.QtyReq,
            QtyOver = part.QtyOver,
            QtyUnder = part.QtyUnder,
            GrainDirection = part.GrainDirection,
            QtyProduced = part.QtyProduced,
            UnderProducedError = part.UnderProducedError,
            UnderProducedAllowed = part.UnderProducedAllowed,
            UnderProducedPlusPart = part.UnderProducedPlusPart
        };

    private static PtxPartInfo withDimensions(
        this PtxPartInfo info,
        double finishedLengthMm,
        double finishedWidthMm,
        double productWidthMm,
        double productHeightMm,
        double productDepthMm,
        double secondCutLengthMm,
        double secondCutWidthMm) =>
        new()
        {
            JobIndex = info.JobIndex,
            PartIndex = info.PartIndex,
            Description = info.Description,
            LabelQuantity = info.LabelQuantity,
            FinishedLengthMm = finishedLengthMm,
            FinishedWidthMm = finishedWidthMm,
            Order = info.Order,
            Edge1 = info.Edge1,
            Edge2 = info.Edge2,
            Edge3 = info.Edge3,
            Edge4 = info.Edge4,
            FaceLaminate = info.FaceLaminate,
            BackLaminate = info.BackLaminate,
            Core = info.Core,
            Drawing = info.Drawing,
            Product = info.Product,
            ProductInfo = info.ProductInfo,
            ProductWidthMm = productWidthMm,
            ProductHeightMm = productHeightMm,
            ProductDepthMm = productDepthMm,
            ProductNumber = info.ProductNumber,
            Room = info.Room,
            Barcode1 = info.Barcode1,
            Barcode2 = info.Barcode2,
            Colour = info.Colour,
            SecondCutLengthMm = secondCutLengthMm,
            SecondCutWidthMm = secondCutWidthMm
        };

    private static PtxPartDestination withDimensions(
        this PtxPartDestination destination,
        double stackHeightDimensionMm,
        double bottomLengthMm,
        double bottomWidthMm,
        double bottomThicknessMm,
        double overhangLengthMm,
        double overhangWidthMm) =>
        new()
        {
            JobIndex = destination.JobIndex,
            PartIndex = destination.PartIndex,
            PartsPerStackLength = destination.PartsPerStackLength,
            PartsPerStackWidth = destination.PartsPerStackWidth,
            PartLayoutOrientation = destination.PartLayoutOrientation,
            StackHeightQuantity = destination.StackHeightQuantity,
            StackHeightDimensionMm = stackHeightDimensionMm,
            Station = destination.Station,
            QuantityStacks = destination.QuantityStacks,
            BottomType = destination.BottomType,
            BottomDescription = destination.BottomDescription,
            BottomMaterial = destination.BottomMaterial,
            BottomLengthMm = bottomLengthMm,
            BottomWidthMm = bottomWidthMm,
            BottomThicknessMm = bottomThicknessMm,
            OverhangLengthMm = overhangLengthMm,
            OverhangWidthMm = overhangWidthMm,
            TopType = destination.TopType,
            TopDescription = destination.TopDescription,
            TopMaterial = destination.TopMaterial,
            SupportType = destination.SupportType,
            SupportDescription = destination.SupportDescription,
            SupportMaterial = destination.SupportMaterial,
            Station2 = destination.Station2
        };

    private static PtxOffcut withDimensions(this PtxOffcut offcut, double lengthMm, double widthMm) =>
        new()
        {
            JobIndex = offcut.JobIndex,
            OffcutIndex = offcut.OffcutIndex,
            Code = offcut.Code,
            MatIndex = offcut.MatIndex,
            LengthMm = lengthMm,
            WidthMm = widthMm,
            Quantity = offcut.Quantity,
            Grain = offcut.Grain,
            Cost = offcut.Cost,
            Type = offcut.Type,
            ExtraInformation = offcut.ExtraInformation,
            CostMethod = offcut.CostMethod
        };

    private static PtxCut withDimension(this PtxCut cut, double dimensionMm) =>
        new()
        {
            JobIndex = cut.JobIndex,
            PtnIndex = cut.PtnIndex,
            CutIndex = cut.CutIndex,
            Sequence = cut.Sequence,
            Function = cut.Function,
            DimensionMm = dimensionMm,
            QtyRpt = cut.QtyRpt,
            PartIndex = cut.PartIndex,
            PartIndexToken = cut.PartIndexToken,
            IsOffcut = cut.IsOffcut,
            OffcutIndex = cut.OffcutIndex,
            QtyParts = cut.QtyParts,
            Comment = cut.Comment
        };

    private static PtxVector withCoordinates(
        this PtxVector vector,
        double xStartMm,
        double yStartMm,
        double xEndMm,
        double yEndMm) =>
        new()
        {
            JobIndex = vector.JobIndex,
            PtnIndex = vector.PtnIndex,
            CutIndex = vector.CutIndex,
            XStartMm = xStartMm,
            YStartMm = yStartMm,
            XEndMm = xEndMm,
            YEndMm = yEndMm
        };

    private static bool TryParseVector(string[] t, out PtxVector vector)
    {
        vector = null!;
        if (!TryGetInt(t, 1, out var job) ||
            !TryGetInt(t, 2, out var ptn) ||
            !TryGetInt(t, 3, out var cutIndex) ||
            !TryGetDouble(t, 4, out var xStart) ||
            !TryGetDouble(t, 5, out var yStart) ||
            !TryGetDouble(t, 6, out var xEnd) ||
            !TryGetDouble(t, 7, out var yEnd))
        {
            return false;
        }

        vector = new PtxVector
        {
            JobIndex = job,
            PtnIndex = ptn,
            CutIndex = cutIndex,
            XStartMm = xStart,
            YStartMm = yStart,
            XEndMm = xEnd,
            YEndMm = yEnd
        };
        return true;
    }

    private static bool TryParseNote(string[] t, out PtxNote note)
    {
        note = null!;
        if (!TryGetInt(t, 1, out var jobIndex) || !TryGetInt(t, 2, out var noteIndex))
            return false;

        note = new PtxNote
        {
            JobIndex = jobIndex,
            NoteIndex = noteIndex,
            Text = GetText(t, 3)
        };
        return true;
    }

    private static string GetText(string[] tokens, int index) =>
        index < tokens.Length ? tokens[index].Trim().Trim('"') : "";

    private static int GetInt(string[] tokens, int index)
    {
        return TryGetInt(tokens, index, out var value) ? value : 0;
    }

    private static bool TryGetInt(string[] tokens, int index, out int value)
    {
        value = 0;
        return index < tokens.Length &&
               int.TryParse(tokens[index].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static double GetDouble(string[] tokens, int index)
    {
        return TryGetDouble(tokens, index, out var value) ? value : 0;
    }

    private static bool TryGetDouble(string[] tokens, int index, out double value)
    {
        value = 0;
        return index < tokens.Length &&
               double.TryParse(tokens[index].Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}
