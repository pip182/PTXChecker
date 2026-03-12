using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using PTXLayoutViewer.Models;

namespace PTXLayoutViewer;

public partial class MainWindow : Window
{
    private PtxDocument? _document;
    private string? _filePath;
    private IReadOnlyList<(int PtnIndex, IReadOnlyList<LayoutRectangle> Rectangles)> _layouts = Array.Empty<(int, IReadOnlyList<LayoutRectangle>)>();
    private IReadOnlyList<LayoutRectangle>? _currentRectangles;
    private IReadOnlyList<DebugLayoutRow> _currentDebugRows = Array.Empty<DebugLayoutRow>();
    private IReadOnlyList<MetadataViewRow> _currentMetadataRows = Array.Empty<MetadataViewRow>();
    private IReadOnlyList<(string Label, string Value)> _currentPartDetailsPairs = Array.Empty<(string, string)>();
    private Border? _highlightOverlay;

    private const double ScalePxPerMm = 0.35;
    private const double HighlightInset = 0;
    private const double HighlightStrokeThickness = 4;
    private const double HighlightCornerRadius = 0;
    private const int ProductItemNumberUdiIndex = 22; // PARTS_UDI "Product Item Number"
    private const byte HighlightStrokeOpacity = 120;
    private static readonly Color HighlightStrokeColor = Color.FromArgb(HighlightStrokeOpacity, 255, 255, 255);
    private static readonly Color[] PartColors =
    {
        Color.FromRgb(59, 130, 246),
        Color.FromRgb(34, 197, 94),
        Color.FromRgb(234, 179, 8),
        Color.FromRgb(239, 68, 68),
        Color.FromRgb(168, 85, 247),
        Color.FromRgb(236, 72, 153),
    };

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "PTX files (*.ptx)|*.ptx|CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            Title = "Select PTX file"
        };
        if (dlg.ShowDialog() != true)
            return;

        try
        {
            _filePath = dlg.FileName;
            _document = PtxParser.Parse(dlg.FileName);
            FilePathText.Text = dlg.FileName;
            _layouts = LayoutBuilder.BuildAllLayouts(_document);

            PatternCombo.Items.Clear();
            if (_layouts.Count > 0)
            {
                foreach (var (ptnIndex, rects) in _layouts)
                {
                    var pattern = _document.GetPattern(ptnIndex);
                    var board = pattern != null ? _document.GetBoard(pattern.BrdIndex) : null;
                    var patternName = pattern?.PatternName ?? "";
                    if (board != null && !string.IsNullOrEmpty(board.MaterialCode))
                    {
                        patternName = string.IsNullOrEmpty(patternName)
                            ? board.MaterialCode
                            : $"{patternName} - {board.MaterialCode}";
                    }

                    if (string.IsNullOrEmpty(patternName))
                        patternName = "Pattern";

                    PatternCombo.Items.Add(new ComboBoxItem
                    {
                        Tag = ptnIndex,
                        Content = $"Pattern {ptnIndex} ({patternName}) - {rects.Count} part(s)"
                    });
                }

                PatternCombo.SelectedIndex = 0;
            }
            else
            {
                PatternCombo.Items.Add(new ComboBoxItem { Content = "No parts in file" });
                PatternCombo.SelectedIndex = 0;
                _currentRectangles = null;
                LayoutCanvas.Children.Clear();
                PartsList.Items.Clear();
                UpdatePartDetailsPanel(null);
                PopulateDebugGrid(Array.Empty<DebugLayoutRow>());
                PopulateMetadataGrid(BuildMetadataRows(null, Array.Empty<LayoutRectangle>()));
                StatusText.Text = _document.Parts.Count == 0 ? "No parts in file." : "No layout data. Metadata still available in the Metadata tab.";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Error loading PTX", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PatternCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_document == null || PatternCombo.SelectedItem is not ComboBoxItem item || item.Tag is not int ptnIndex)
            return;

        var entry = _layouts.FirstOrDefault(l => l.PtnIndex == ptnIndex);
        var rects = entry.Rectangles;
        var pattern = _document.GetPattern(ptnIndex);
        var board = pattern != null
            ? _document.GetBoard(pattern.BrdIndex)
            : _document.Boards.FirstOrDefault();

        BoardSizeText.Text = board != null
            ? $"Board: {board.LengthMm:F0} x {board.WidthMm:F0} mm ({UnitHelpers.MmToInch(board.LengthMm):F1} x {UnitHelpers.MmToInch(board.WidthMm):F1} in)"
            : string.Empty;

        _currentRectangles = rects;
        PopulateDebugGrid(BuildDebugRows(ptnIndex, rects, board));
        RenderLayout(rects, board);
        PopulatePartsList(rects);
        UpdatePartDetailsPanel(null);
        PopulateMetadataGrid(BuildMetadataRows(ptnIndex, rects));

        StatusText.Text = $"{rects.Count} part(s), {_currentDebugRows.Count} debug row(s), {_currentMetadataRows.Count} metadata row(s)";
    }

    private void PopulatePartsList(IReadOnlyList<LayoutRectangle> rectangles)
    {
        PartsList.SelectionChanged -= PartsList_SelectionChanged;
        PartsList.Items.Clear();
        for (var i = 0; i < rectangles.Count; i++)
        {
            var rectangle = rectangles[i];
            var label = string.IsNullOrEmpty(rectangle.PartName) ? $"P{rectangle.PartIndex}" : rectangle.PartName;
            PartsList.Items.Add(new ListBoxItem { Tag = i, Content = label });
        }
        PartsList.SelectionChanged += PartsList_SelectionChanged;
    }

    private void PartsList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (PartsList.SelectedItem is not ListBoxItem item || item.Tag is not int index)
        {
            ClearPartHighlight();
            UpdatePartDetailsPanel(null);
            SyncGridSelectionToPart(-1);
            return;
        }

        HighlightPartAtIndex(index);
        UpdatePartDetailsPanel(index);
        SyncGridSelectionToPart(index);
    }

    /// <summary>Updates Debug and Metadata grid selection so the row for the selected part is highlighted.</summary>
    private void SyncGridSelectionToPart(int layoutIndex)
    {
        DebugGrid.SelectionChanged -= DebugGrid_SelectionChanged;
        MetadataGrid.SelectionChanged -= MetadataGrid_SelectionChanged;
        try
        {
            if (layoutIndex >= 0 && _currentRectangles != null && layoutIndex < _currentRectangles.Count)
            {
                DebugGrid.SelectedIndex = layoutIndex;
                var partIndex = _currentRectangles[layoutIndex].PartIndex;
                var metadataRow = _currentMetadataRows.FirstOrDefault(r => r.PartIndex == partIndex);
                MetadataGrid.SelectedItem = metadataRow;
            }
            else
            {
                DebugGrid.SelectedIndex = -1;
                MetadataGrid.SelectedIndex = -1;
            }
        }
        finally
        {
            DebugGrid.SelectionChanged += DebugGrid_SelectionChanged;
            MetadataGrid.SelectionChanged += MetadataGrid_SelectionChanged;
        }
    }

    private void HighlightPartAtIndex(int index)
    {
        RemoveHighlightOverlay();
        if (_currentRectangles == null || index < 0 || index >= _currentRectangles.Count)
            return;

        var rectangle = _currentRectangles[index];
        var left = rectangle.XMm * ScalePxPerMm;
        var top = rectangle.YMm * ScalePxPerMm;
        var width = Math.Max(1, rectangle.WidthMm * ScalePxPerMm);
        var height = Math.Max(1, rectangle.HeightMm * ScalePxPerMm);

        if (width <= HighlightInset * 2 || height <= HighlightInset * 2)
            return;

        _highlightOverlay = new Border
        {
            Width = width - HighlightInset * 2,
            Height = height - HighlightInset * 2,
            Background = Brushes.Transparent,
            BorderBrush = new SolidColorBrush(HighlightStrokeColor),
            BorderThickness = new Thickness(HighlightStrokeThickness),
            CornerRadius = new CornerRadius(HighlightCornerRadius)
        };

        Canvas.SetLeft(_highlightOverlay, left + HighlightInset);
        Canvas.SetTop(_highlightOverlay, top + HighlightInset);
        LayoutCanvas.Children.Add(_highlightOverlay);
    }

    private void RemoveHighlightOverlay()
    {
        if (_highlightOverlay != null && LayoutCanvas.Children.Contains(_highlightOverlay))
            LayoutCanvas.Children.Remove(_highlightOverlay);
        _highlightOverlay = null;
    }

    private void ClearPartHighlight() => RemoveHighlightOverlay();

    private void UpdatePartDetailsPanel(int? index)
    {
        PartDetailsPanel.Children.Clear();
        PartDetailsPanel.RowDefinitions.Clear();

        if (index == null || _currentRectangles == null || _document == null || index.Value >= _currentRectangles.Count)
        {
            _currentPartDetailsPairs = Array.Empty<(string, string)>();
            PartDetailsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var message = new TextBlock
            {
                Text = "Select a part from the list or canvas.",
                Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xa3, 0xb8)),
                FontSize = 12
            };
            Grid.SetColumnSpan(message, 3);
            Grid.SetRow(message, 0);
            PartDetailsPanel.Children.Add(message);
            return;
        }

        var layout = _currentRectangles[index.Value];
        var part = _document.GetPart(layout.PartIndex);
        var partInfo = _document.GetPartInfo(layout.PartIndex);
        var partUdi = _document.GetUdiForPart(layout.PartIndex);
        var destination = _document.GetDestinationForPart(layout.PartIndex);
        var job = part != null ? _document.GetJob(part.JobIndex) : null;

        var labelBrush = new SolidColorBrush(Color.FromRgb(0x94, 0xa3, 0xb8));
        var valueBrush = new SolidColorBrush(Color.FromRgb(0xf1, 0xf5, 0xf9));
        var pairs = new List<(string Label, string Value)>();

        if (part != null)
        {
            pairs.Add(("Part index", part.PartIndex.ToString()));
            pairs.Add(("Code", part.PartName));
            pairs.Add(("Job index", part.JobIndex.ToString()));
            pairs.Add(("Material index", part.MatIndex.ToString()));
            pairs.Add(("Requested size", $"{UnitHelpers.FormatMmAndInch(part.WidthMm)} x {UnitHelpers.FormatMmAndInch(part.HeightMm)}"));
            pairs.Add(("Quantity required", part.QtyReq.ToString()));
            pairs.Add(("Quantity produced", part.QtyProduced.ToString()));
            pairs.Add(("Grain", part.GrainDirection switch { 1 => "Along board length", 2 => "Along board width", _ => "None" }));
        }

        pairs.Add(("Layout X", UnitHelpers.FormatMmAndInch(layout.XMm)));
        pairs.Add(("Layout Y", UnitHelpers.FormatMmAndInch(layout.YMm)));
        pairs.Add(("Layout width", UnitHelpers.FormatMmAndInch(layout.WidthMm)));
        pairs.Add(("Layout height", UnitHelpers.FormatMmAndInch(layout.HeightMm)));
        pairs.Add(("Region width", UnitHelpers.FormatMmAndInch(layout.RegionWidthMm)));
        pairs.Add(("Region height", UnitHelpers.FormatMmAndInch(layout.RegionHeightMm)));

        if (job != null)
        {
            pairs.Add(("Job name", job.Name));
            pairs.Add(("Customer", job.Customer));
            pairs.Add(("Job status", FormatJobStatus(job.Status)));
        }

        if (partInfo != null)
        {
            pairs.Add(("Description", partInfo.Description));
            pairs.Add(("Finished size", $"{UnitHelpers.FormatMmAndInch(partInfo.FinishedLengthMm)} x {UnitHelpers.FormatMmAndInch(partInfo.FinishedWidthMm)}"));
            pairs.Add(("Drawing", partInfo.Drawing));
            pairs.Add(("Product", partInfo.Product));
            pairs.Add(("Room", partInfo.Room));
            pairs.Add(("Colour", partInfo.Colour));
            pairs.Add(("Barcode 1", partInfo.Barcode1));
            pairs.Add(("Barcode 2", partInfo.Barcode2));
        }

        if (destination != null)
        {
            pairs.Add(("Station", destination.Station));
            pairs.Add(("Station 2", destination.Station2));
            pairs.Add(("Stacks", destination.QuantityStacks.ToString()));
            pairs.Add(("Stack layout", $"{destination.PartsPerStackLength} x {destination.PartsPerStackWidth}"));
            pairs.Add(("Part orientation", destination.PartLayoutOrientation == 1 ? "Lengthways" : "Widthways"));
        }

        if (partUdi != null)
        {
            for (var i = 0; i < PtxPartUdi.FieldLabels.Count; i++)
            {
                var value = partUdi.GetValue(i);
                if (!string.IsNullOrWhiteSpace(value))
                    pairs.Add((PtxPartUdi.FieldLabels[i], value));
            }
        }

        const int columns = 3;
        const double labelMaxWidth = 160;
        var seenLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visiblePairs = new List<(string Label, string Value)>();
        foreach (var p in pairs)
        {
            if (string.IsNullOrWhiteSpace(p.Value)) continue;
            if (!seenLabels.Add(p.Label)) continue;
            visiblePairs.Add(p);
        }
        _currentPartDetailsPairs = visiblePairs;
        var rowCount = (visiblePairs.Count + columns - 1) / columns;
        for (var row = 0; row < rowCount; row++)
            PartDetailsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (var i = 0; i < visiblePairs.Count; i++)
        {
            var (label, value) = visiblePairs[i];
            var cell = new Grid { Margin = new Thickness(0, 0, 20, 6) };
            cell.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MaxWidth = labelMaxWidth });
            cell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var labelText = new TextBlock
            {
                Text = label + ":",
                Foreground = labelBrush,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = labelMaxWidth
            };

            var valueText = new TextBlock
            {
                Text = value,
                Foreground = valueBrush,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };

            Grid.SetColumn(labelText, 0);
            Grid.SetColumn(valueText, 1);
            Grid.SetColumn(cell, i % columns);
            Grid.SetRow(cell, i / columns);
            cell.Children.Add(labelText);
            cell.Children.Add(valueText);
            PartDetailsPanel.Children.Add(cell);
        }
    }

    private void RenderLayout(IReadOnlyList<LayoutRectangle> rectangles, PtxBoard? board)
    {
        RemoveHighlightOverlay();
        LayoutCanvas.Children.Clear();

        var boardWidth = board?.LengthMm ?? 0;
        var boardHeight = board?.WidthMm ?? 0;
        if (board == null)
        {
            foreach (var rectangle in rectangles)
            {
                boardWidth = Math.Max(boardWidth, rectangle.XMm + rectangle.WidthMm);
                boardHeight = Math.Max(boardHeight, rectangle.YMm + rectangle.HeightMm);
            }
        }

        if (boardWidth <= 0)
            boardWidth = 1000;
        if (boardHeight <= 0)
            boardHeight = 800;

        LayoutCanvas.Width = boardWidth * ScalePxPerMm;
        LayoutCanvas.Height = boardHeight * ScalePxPerMm;
        LayoutCanvas.ClipToBounds = true;

        var boardRect = new Rectangle
        {
            Width = LayoutCanvas.Width,
            Height = LayoutCanvas.Height,
            Fill = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            Stroke = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
            StrokeThickness = 2
        };
        LayoutCanvas.Children.Add(boardRect);

        for (var i = 0; i < rectangles.Count; i++)
        {
            var rectangle = rectangles[i];
            var color = PartColors[i % PartColors.Length];
            var debugRow = _currentDebugRows.FirstOrDefault(r =>
                r.PartIndex == rectangle.PartIndex &&
                Math.Abs(r.XMm - rectangle.XMm) < 0.01 &&
                Math.Abs(r.YMm - rectangle.YMm) < 0.01);
            var hasWarning = debugRow != null && !string.IsNullOrWhiteSpace(debugRow.Warning);

            var partRect = new Rectangle
            {
                Width = Math.Max(1, rectangle.WidthMm * ScalePxPerMm),
                Height = Math.Max(1, rectangle.HeightMm * ScalePxPerMm),
                Fill = new SolidColorBrush(Color.FromArgb(180, color.R, color.G, color.B)),
                Stroke = new SolidColorBrush(hasWarning ? Color.FromRgb(248, 113, 113) : color),
                StrokeThickness = hasWarning ? 2.5 : 1.5,
                Tag = rectangle,
                Cursor = Cursors.Hand
            };

            partRect.MouseDown += PartRect_MouseDown;
            Canvas.SetLeft(partRect, rectangle.XMm * ScalePxPerMm);
            Canvas.SetTop(partRect, rectangle.YMm * ScalePxPerMm);
            LayoutCanvas.Children.Add(partRect);

            var partNameText = string.IsNullOrEmpty(rectangle.PartName) ? $"P{rectangle.PartIndex}" : rectangle.PartName;
            var productItemNumber = _document?.GetUdiForPart(rectangle.PartIndex)?.GetValue(ProductItemNumberUdiIndex) ?? "";

            var label = new TextBlock
            {
                Text = partNameText,
                Foreground = Brushes.White,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                IsHitTestVisible = false
            };
            var labelStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                IsHitTestVisible = false
            };
            labelStack.Children.Add(label);
            if (!string.IsNullOrEmpty(productItemNumber))
            {
                var itemLine = new TextBlock
                {
                    Text = $"Item #{productItemNumber}",
                    Foreground = Brushes.White,
                    FontSize = 9,
                    Opacity = 0.9,
                    IsHitTestVisible = false
                };
                labelStack.Children.Add(itemLine);
            }

            Canvas.SetLeft(labelStack, rectangle.XMm * ScalePxPerMm + 8);
            Canvas.SetTop(labelStack, rectangle.YMm * ScalePxPerMm + 4);
            LayoutCanvas.Children.Add(labelStack);
        }
    }

    private void PartRect_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Rectangle rect || rect.Tag is not LayoutRectangle layout || _currentRectangles == null)
            return;

        for (var i = 0; i < _currentRectangles.Count; i++)
        {
            if (_currentRectangles[i].PartIndex == layout.PartIndex &&
                Math.Abs(_currentRectangles[i].XMm - layout.XMm) < 0.01 &&
                Math.Abs(_currentRectangles[i].YMm - layout.YMm) < 0.01)
            {
                PartsList.SelectedIndex = i;
                break;
            }
        }
    }

    private IReadOnlyList<DebugLayoutRow> BuildDebugRows(int ptnIndex, IReadOnlyList<LayoutRectangle> rectangles, PtxBoard? board)
    {
        if (_document == null)
            return Array.Empty<DebugLayoutRow>();

        return rectangles.Select(rectangle =>
        {
            var part = _document.GetPart(rectangle.PartIndex);
            var fitsRegion = rectangle.WidthMm <= rectangle.RegionWidthMm + 0.01 &&
                             rectangle.HeightMm <= rectangle.RegionHeightMm + 0.01;
            var fitsBoard = board == null ||
                            (rectangle.XMm + rectangle.WidthMm <= board.LengthMm + 0.01 &&
                             rectangle.YMm + rectangle.HeightMm <= board.WidthMm + 0.01);
            var warnings = new List<string>();
            if (!fitsRegion)
                warnings.Add("layout exceeds reconstructed region");
            if (!fitsBoard)
                warnings.Add("layout exceeds board");

            return new DebugLayoutRow
            {
                PatternIndex = ptnIndex,
                PartIndex = rectangle.PartIndex,
                PartName = rectangle.PartName,
                GrainDirection = part?.GrainDirection ?? 0,
                PartWidthMm = part?.WidthMm ?? rectangle.WidthMm,
                PartHeightMm = part?.HeightMm ?? rectangle.HeightMm,
                LayoutWidthMm = rectangle.WidthMm,
                LayoutHeightMm = rectangle.HeightMm,
                RegionWidthMm = rectangle.RegionWidthMm,
                RegionHeightMm = rectangle.RegionHeightMm,
                XMm = rectangle.XMm,
                YMm = rectangle.YMm,
                FitsRegion = fitsRegion,
                FitsBoard = fitsBoard,
                Warning = string.Join("; ", warnings)
            };
        }).ToList();
    }

    private IReadOnlyList<MetadataViewRow> BuildMetadataRows(int? ptnIndex, IReadOnlyList<LayoutRectangle> rectangles)
    {
        if (_document == null)
            return Array.Empty<MetadataViewRow>();

        var rows = new List<MetadataViewRow>();
        var pattern = ptnIndex.HasValue ? _document.GetPattern(ptnIndex.Value) : null;
        var partIndices = rectangles.Select(r => r.PartIndex).Distinct().ToHashSet();
        var jobIndices = new HashSet<int>();

        if (pattern != null)
            jobIndices.Add(pattern.JobIndex);
        foreach (var partIndex in partIndices)
        {
            var part = _document.GetPart(partIndex);
            if (part != null)
                jobIndices.Add(part.JobIndex);
        }
        if (jobIndices.Count == 0)
        {
            foreach (var job in _document.Jobs)
                jobIndices.Add(job.JobIndex);
        }

        if (_document.Header != null)
        {
            rows.Add(new MetadataViewRow
            {
                TableName = "HEADER",
                Scope = "Document",
                RecordKey = _document.Header.Title,
                Summary = $"Version {_document.Header.Version}, units {_document.Header.Units}, origin {_document.Header.Origin}",
                Details = $"TrimType={_document.Header.TrimType}"
            });
        }

        foreach (var jobIndex in jobIndices.OrderBy(x => x))
        {
            var job = _document.GetJob(jobIndex);
            if (job != null)
            {
                rows.Add(new MetadataViewRow
                {
                    TableName = "JOBS",
                    Scope = $"Job {jobIndex}",
                    RecordKey = job.Name,
                    Summary = JoinFields(job.Description, job.Customer, FormatJobStatus(job.Status)),
                    Details = JoinFields($"Order={job.OrderDate}", $"Cut={job.CutDate}", $"Waste={job.WastePercent:F1}%", $"CutTime={job.CutTimeSeconds:F0}s")
                });
            }

            foreach (var note in _document.GetNotesForJob(jobIndex))
            {
                rows.Add(new MetadataViewRow
                {
                    TableName = "NOTES",
                    Scope = $"Job {jobIndex}",
                    RecordKey = $"Note {note.NoteIndex}",
                    Summary = note.Text,
                    Details = string.Empty
                });
            }
        }

        foreach (var partIndex in partIndices.OrderBy(x => x))
        {
            var part = _document.GetPart(partIndex);
            if (part == null)
                continue;

            var scope = $"Part {partIndex}";
            var partInfo = _document.GetPartInfo(partIndex);
            if (partInfo != null)
            {
                rows.Add(new MetadataViewRow
                {
                    TableName = "PARTS_INF",
                    Scope = scope,
                    RecordKey = part.PartName,
                    Summary = JoinFields(partInfo.Description, partInfo.Product, partInfo.Room),
                    Details = JoinFields(
                        $"Finished={partInfo.FinishedLengthMm:F1}x{partInfo.FinishedWidthMm:F1}",
                        $"Drawing={partInfo.Drawing}",
                        $"Colour={partInfo.Colour}",
                        $"Barcodes={JoinFields(partInfo.Barcode1, partInfo.Barcode2)}"),
                    PartIndex = partIndex
                });
            }

            var partUdi = _document.GetUdiForPart(partIndex);
            if (partUdi != null)
            {
                var nonEmptyValues = partUdi.Values.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
                rows.Add(new MetadataViewRow
                {
                    TableName = "PARTS_UDI",
                    Scope = scope,
                    RecordKey = part.PartName,
                    Summary = nonEmptyValues.Count == 0 ? "(empty)" : string.Join(" | ", nonEmptyValues.Take(3)),
                    Details = nonEmptyValues.Count <= 3 ? string.Empty : $"{nonEmptyValues.Count} non-empty value(s)",
                    PartIndex = partIndex
                });
            }

            var destination = _document.GetDestinationForPart(partIndex);
            if (destination != null)
            {
                rows.Add(new MetadataViewRow
                {
                    TableName = "PARTS_DST",
                    Scope = scope,
                    RecordKey = part.PartName,
                    Summary = JoinFields(destination.Station, destination.Station2),
                    Details = JoinFields(
                        $"Stacks={destination.QuantityStacks}",
                        $"Layout={destination.PartsPerStackLength}x{destination.PartsPerStackWidth}",
                        $"Orientation={(destination.PartLayoutOrientation == 1 ? "Lengthways" : "Widthways")}"),
                    PartIndex = partIndex
                });
            }
        }

        if (ptnIndex.HasValue)
        {
            foreach (var patternUdi in _document.GetPatternUdis(ptnIndex.Value))
            {
                var nonEmptyValues = patternUdi.Values.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
                rows.Add(new MetadataViewRow
                {
                    TableName = "PTN_UDI",
                    Scope = $"Pattern {ptnIndex.Value}",
                    RecordKey = $"Strip {patternUdi.StripIndex}",
                    Summary = nonEmptyValues.Count == 0 ? "(empty)" : string.Join(" | ", nonEmptyValues.Take(3)),
                    Details = nonEmptyValues.Count <= 3 ? string.Empty : $"{nonEmptyValues.Count} non-empty value(s)"
                });
            }
        }

        return rows;
    }

    private void PopulateDebugGrid(IReadOnlyList<DebugLayoutRow> rows)
    {
        _currentDebugRows = rows;
        DebugGrid.ItemsSource = rows;
    }

    private void PopulateMetadataGrid(IReadOnlyList<MetadataViewRow> rows)
    {
        _currentMetadataRows = rows;
        MetadataGrid.ItemsSource = rows;
    }

    private void SyncSelectionToPartHighlight(int layoutIndex)
    {
        PartsList.SelectionChanged -= PartsList_SelectionChanged;
        try
        {
            PartsList.SelectedIndex = layoutIndex;
            HighlightPartAtIndex(layoutIndex);
            UpdatePartDetailsPanel(layoutIndex);
        }
        finally
        {
            PartsList.SelectionChanged += PartsList_SelectionChanged;
        }
    }

    private void ClearSelectionAndHighlight()
    {
        PartsList.SelectionChanged -= PartsList_SelectionChanged;
        try
        {
            PartsList.SelectedIndex = -1;
            ClearPartHighlight();
            UpdatePartDetailsPanel(null);
        }
        finally
        {
            PartsList.SelectionChanged += PartsList_SelectionChanged;
        }
    }

    private void DebugGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var idx = DebugGrid.SelectedIndex;
        if (idx >= 0 && _currentRectangles != null && idx < _currentRectangles.Count)
        {
            if (PartsList.SelectedIndex != idx)
                SyncSelectionToPartHighlight(idx);
        }
        else if (PartsList.SelectedIndex != -1)
        {
            ClearSelectionAndHighlight();
        }
    }

    private void MetadataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MetadataGrid.SelectedItem is not MetadataViewRow row || !row.PartIndex.HasValue || _currentRectangles == null)
        {
            if (PartsList.SelectedIndex != -1)
                ClearSelectionAndHighlight();
            return;
        }
        var layoutIndex = -1;
        for (var i = 0; i < _currentRectangles.Count; i++)
        {
            if (_currentRectangles[i].PartIndex == row.PartIndex.Value)
            {
                layoutIndex = i;
                break;
            }
        }
        if (layoutIndex >= 0 && PartsList.SelectedIndex != layoutIndex)
            SyncSelectionToPartHighlight(layoutIndex);
        else if (layoutIndex < 0 && PartsList.SelectedIndex != -1)
            ClearSelectionAndHighlight();
    }

    private void CopyDebugButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentDebugRows.Count == 0)
            return;

        Clipboard.SetText(BuildDebugCsv(_currentDebugRows));
        StatusText.Text = $"Copied {_currentDebugRows.Count} debug row(s) to the clipboard.";
    }

    private void ExportDebugButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentDebugRows.Count == 0)
            return;

        ExportCsv(BuildDebugCsv(_currentDebugRows), "debug");
    }

    private void CopyMetadataButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMetadataRows.Count == 0)
            return;

        Clipboard.SetText(BuildMetadataCsv(_currentMetadataRows));
        StatusText.Text = $"Copied {_currentMetadataRows.Count} metadata row(s) to the clipboard.";
    }

    private void ExportMetadataButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMetadataRows.Count == 0)
            return;

        ExportCsv(BuildMetadataCsv(_currentMetadataRows), "metadata");
    }

    private void CopyPartDetailsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPartDetailsPairs.Count == 0)
            return;

        Clipboard.SetText(BuildPartDetailsCsv(_currentPartDetailsPairs));
        StatusText.Text = $"Copied {_currentPartDetailsPairs.Count} part detail(s) to the clipboard.";
    }

    private void ExportPartDetailsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPartDetailsPairs.Count == 0)
            return;

        ExportCsv(BuildPartDetailsCsv(_currentPartDetailsPairs), "part_details");
    }

    private void ExportCsv(string content, string suffix)
    {
        var fileStem = System.IO.Path.GetFileNameWithoutExtension(_filePath ?? "ptx");
        var saveDialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName = $"{fileStem}_{suffix}.csv",
            Title = $"Export {suffix} CSV"
        };

        if (saveDialog.ShowDialog() != true)
            return;

        File.WriteAllText(saveDialog.FileName, content, Encoding.UTF8);
        StatusText.Text = $"Exported {suffix} CSV to {saveDialog.FileName}.";
    }

    private static string BuildDebugCsv(IEnumerable<DebugLayoutRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("PatternIndex,PartIndex,PartName,GrainDirection,PartWidthMm,PartHeightMm,LayoutWidthMm,LayoutHeightMm,RegionWidthMm,RegionHeightMm,XMm,YMm,FitsRegion,FitsBoard,Warning");
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(",",
                row.PatternIndex,
                row.PartIndex,
                Csv(row.PartName),
                row.GrainDirection,
                row.PartWidthMm.ToString("F3"),
                row.PartHeightMm.ToString("F3"),
                row.LayoutWidthMm.ToString("F3"),
                row.LayoutHeightMm.ToString("F3"),
                row.RegionWidthMm.ToString("F3"),
                row.RegionHeightMm.ToString("F3"),
                row.XMm.ToString("F3"),
                row.YMm.ToString("F3"),
                row.FitsRegion,
                row.FitsBoard,
                Csv(row.Warning)));
        }
        return builder.ToString();
    }

    private static string BuildMetadataCsv(IEnumerable<MetadataViewRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("TableName,Scope,RecordKey,Summary,Details");
        foreach (var row in rows)
            builder.AppendLine($"{Csv(row.TableName)},{Csv(row.Scope)},{Csv(row.RecordKey)},{Csv(row.Summary)},{Csv(row.Details)}");
        return builder.ToString();
    }

    private static string BuildPartDetailsCsv(IEnumerable<(string Label, string Value)> pairs)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Label,Value");
        foreach (var (label, value) in pairs)
            builder.AppendLine($"{Csv(label)},{Csv(value)}");
        return builder.ToString();
    }

    private static string Csv(string value)
    {
        var text = value ?? string.Empty;
        return $"\"{text.Replace("\"", "\"\"")}\"";
    }

    private static string JoinFields(params string[] values) =>
        string.Join(" | ", values.Where(v => !string.IsNullOrWhiteSpace(v)));

    private static string FormatJobStatus(int status) => status switch
    {
        0 => "Not optimized",
        1 => "Optimized",
        2 => "Optimize failed",
        _ => $"Status {status}"
    };
}
