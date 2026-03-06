using System.IO;
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
    private IReadOnlyList<(int PtnIndex, IReadOnlyList<LayoutRectangle> Rectangles)> _layouts = Array.Empty<(int, IReadOnlyList<LayoutRectangle>)>();
    private IReadOnlyList<LayoutRectangle>? _currentRectangles;
    private Border? _highlightOverlay; // Inset border so stroke isn't clipped at board edge
    private const double ScalePxPerMm = 0.35;
    private const double HighlightInset = 4;
    private const double HighlightStrokeThickness = 2;
    private static readonly Color HighlightStrokeColor = Color.FromRgb(255, 255, 255);
    private static readonly Color[] PartColors =
    {
        Color.FromRgb(59, 130, 246),   // blue
        Color.FromRgb(34, 197, 94),    // green
        Color.FromRgb(234, 179, 8),    // amber
        Color.FromRgb(239, 68, 68),    // red
        Color.FromRgb(168, 85, 247),   // violet
        Color.FromRgb(236, 72, 153),   // pink
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
        if (dlg.ShowDialog() != true) return;

        try
        {
            _document = PtxParser.Parse(dlg.FileName);
            FilePathText.Text = dlg.FileName;
            _layouts = LayoutBuilder.BuildAllLayouts(_document);

            PatternCombo.Items.Clear();
            if (_layouts.Count > 0)
            {
                foreach (var (ptnIndex, rects) in _layouts)
                {
                    var ptn = _document.Patterns.FirstOrDefault(p => p.PtnIndex == ptnIndex);
                    var board = ptn != null ? _document.GetBoard(ptn.BrdIndex) : null;
                    var namePart = ptn?.PatternName ?? "";
                    if (board != null && !string.IsNullOrEmpty(board.MaterialCode))
                    {
                        if (namePart == "(generic)" && board.MaterialCode == "(generic)")
                            namePart = "generic";
                        else if (string.IsNullOrEmpty(namePart))
                            namePart = board.MaterialCode;
                        else
                            namePart = $"{namePart} — {board.MaterialCode}";
                    }
                    if (string.IsNullOrEmpty(namePart)) namePart = "—";
                    var label = $"Pattern {ptnIndex} ({namePart})";
                    PatternCombo.Items.Add(new ComboBoxItem { Tag = ptnIndex, Content = $"{label} — {rects.Count} part(s)" });
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
                StatusText.Text = _document.Parts.Count == 0 ? "No parts in file." : "No layout data.";
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
        if (entry.Rectangles is not { } rects) return;

        var board = _document.GetBoard(
            _document.Patterns.FirstOrDefault(p => p.PtnIndex == ptnIndex)?.BrdIndex ?? 1
        ) ?? _document.Boards.FirstOrDefault();

        if (board != null)
            BoardSizeText.Text = $"Board: {board.LengthMm:F0} × {board.WidthMm:F0} mm ({UnitHelpers.MmToInch(board.LengthMm):F1} × {UnitHelpers.MmToInch(board.WidthMm):F1} in)";
        else
            BoardSizeText.Text = "";

        _currentRectangles = rects;
        RenderLayout(rects, board);
        PopulatePartsList(rects);
        UpdatePartDetailsPanel(null);
        StatusText.Text = $"{rects.Count} part(s) — scale {ScalePxPerMm} px/mm";
    }

    private void PopulatePartsList(IReadOnlyList<LayoutRectangle> rectangles)
    {
        PartsList.SelectionChanged -= PartsList_SelectionChanged;
        PartsList.Items.Clear();
        for (var i = 0; i < rectangles.Count; i++)
        {
            var r = rectangles[i];
            var label = string.IsNullOrEmpty(r.PartName) ? $"P{r.PartIndex}" : r.PartName;
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
            return;
        }
        HighlightPartAtIndex(index);
        UpdatePartDetailsPanel(index);
    }

    private void HighlightPartAtIndex(int index)
    {
        RemoveHighlightOverlay();
        if (_currentRectangles == null || index < 0 || index >= _currentRectangles.Count)
            return;
        var r = _currentRectangles[index];
        double scale = ScalePxPerMm;
        double left = r.XMm * scale;
        double top = r.YMm * scale;
        double w = Math.Max(1, r.WidthMm * scale);
        double h = Math.Max(1, r.HeightMm * scale);
        double inset = HighlightInset;
        if (w > inset * 2 && h > inset * 2)
        {
            // Border draws its stroke on the edge of the element; inset position/size keeps stroke fully inside part so it never clips
            _highlightOverlay = new Border
            {
                Width = w - inset * 2,
                Height = h - inset * 2,
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(HighlightStrokeColor),
                BorderThickness = new Thickness(HighlightStrokeThickness)
            };
            Canvas.SetLeft(_highlightOverlay, left + inset);
            Canvas.SetTop(_highlightOverlay, top + inset);
            LayoutCanvas.Children.Add(_highlightOverlay);
        }
    }

    private void RemoveHighlightOverlay()
    {
        if (_highlightOverlay != null && LayoutCanvas.Children.Contains(_highlightOverlay))
        {
            LayoutCanvas.Children.Remove(_highlightOverlay);
            _highlightOverlay = null;
        }
    }

    private void ClearPartHighlight()
    {
        RemoveHighlightOverlay();
    }

    private void UpdatePartDetailsPanel(int? index)
    {
        PartDetailsPanel.Children.Clear();
        if (index == null || _currentRectangles == null || _document == null || index.Value >= _currentRectangles.Count)
        {
            var msg = new TextBlock { Text = "Select a part from the list or canvas.", Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xa3, 0xb8)), FontSize = 12 };
            Grid.SetColumnSpan(msg, 3);
            PartDetailsPanel.Children.Add(msg);
            return;
        }
        var layout = _currentRectangles[index.Value];
        var part = _document.GetPart(layout.PartIndex);
        var labelBrush = new SolidColorBrush(Color.FromRgb(0x94, 0xa3, 0xb8));
        var valueBrush = new SolidColorBrush(Color.FromRgb(0xf1, 0xf5, 0xf9));

        var pairs = new List<(string Label, string Value)>();
        if (part != null)
        {
            pairs.Add(("Part index", part.PartIndex.ToString()));
            pairs.Add(("Part name", part.PartName));
            pairs.Add(("Job index", part.JobIndex.ToString()));
            pairs.Add(("Width", UnitHelpers.FormatMmAndInch(part.WidthMm)));
            pairs.Add(("Height", UnitHelpers.FormatMmAndInch(part.HeightMm)));
            pairs.Add(("Quantity required", part.QtyReq.ToString()));
            pairs.Add(("Grain direction", part.GrainDirection switch { 1 => "Along board length", 2 => "Along board width", _ => "None" }));
        }
        pairs.Add(("Layout X", UnitHelpers.FormatMmAndInch(layout.XMm)));
        pairs.Add(("Layout Y", UnitHelpers.FormatMmAndInch(layout.YMm)));
        pairs.Add(("Layout width", UnitHelpers.FormatMmAndInch(layout.WidthMm)));
        pairs.Add(("Layout height", UnitHelpers.FormatMmAndInch(layout.HeightMm)));

        const int columns = 3;
        for (var i = 0; i < pairs.Count; i++)
        {
            var (label, value) = pairs[i];
            var col = i % columns;
            var row = i / columns;
            var cell = new Grid { Margin = new Thickness(0, 0, 20, 6) };
            cell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            cell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var lbl = new TextBlock { Text = label + ":", Foreground = labelBrush, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            var val = new TextBlock { Text = value, Foreground = valueBrush, FontSize = 12, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
            cell.Children.Add(lbl);
            cell.Children.Add(val);
            Grid.SetColumn(lbl, 0);
            Grid.SetColumn(val, 1);
            Grid.SetColumn(cell, col);
            Grid.SetRow(cell, row);
            PartDetailsPanel.Children.Add(cell);
        }
    }

    private void RenderLayout(IReadOnlyList<LayoutRectangle> rectangles, PtxBoard? board)
    {
        RemoveHighlightOverlay();
        LayoutCanvas.Children.Clear();

        // Use actual board dimensions so parts never render beyond the board; expand only when no board
        double boardW = board?.LengthMm ?? 0;
        double boardH = board?.WidthMm ?? 0;
        if (board == null)
        {
            foreach (var r in rectangles)
            {
                boardW = Math.Max(boardW, r.XMm + r.WidthMm);
                boardH = Math.Max(boardH, r.YMm + r.HeightMm);
            }
        }
        if (boardW <= 0) boardW = 1000;
        if (boardH <= 0) boardH = 800;

        double scale = ScalePxPerMm;
        double canvasW = boardW * scale;
        double canvasH = boardH * scale;

        LayoutCanvas.Width = canvasW;
        LayoutCanvas.Height = canvasH;
        LayoutCanvas.ClipToBounds = true;

        // Board outline
        var boardRect = new Rectangle
        {
            Width = canvasW,
            Height = canvasH,
            Fill = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            Stroke = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
            StrokeThickness = 2
        };
        Canvas.SetLeft(boardRect, 0);
        Canvas.SetTop(boardRect, 0);
        LayoutCanvas.Children.Add(boardRect);

        for (var i = 0; i < rectangles.Count; i++)
        {
            var r = rectangles[i];
            var color = PartColors[i % PartColors.Length];
            var rect = new Rectangle
            {
                Width = Math.Max(1, r.WidthMm * scale),
                Height = Math.Max(1, r.HeightMm * scale),
                Fill = new SolidColorBrush(Color.FromArgb(180, color.R, color.G, color.B)),
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 1.5,
                Tag = r
            };
            rect.Cursor = Cursors.Hand;
            rect.MouseDown += PartRect_MouseDown;
            Canvas.SetLeft(rect, r.XMm * scale);
            Canvas.SetTop(rect, r.YMm * scale);
            LayoutCanvas.Children.Add(rect);

            var label = new TextBlock
            {
                Text = string.IsNullOrEmpty(r.PartName) ? $"P{r.PartIndex}" : r.PartName,
                Foreground = Brushes.White,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(label, r.XMm * scale + 4);
            Canvas.SetTop(label, r.YMm * scale + 4);
            LayoutCanvas.Children.Add(label);
        }
    }

    private void PartRect_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Rectangle rect || rect.Tag is not LayoutRectangle layout || _document == null)
            return;
        // Sync list selection so highlight and bottom panel update (no dialog)
        if (_currentRectangles != null)
        {
            for (var i = 0; i < _currentRectangles.Count; i++)
            {
                if (_currentRectangles[i].PartIndex == layout.PartIndex
                    && Math.Abs(_currentRectangles[i].XMm - layout.XMm) < 0.01
                    && Math.Abs(_currentRectangles[i].YMm - layout.YMm) < 0.01)
                {
                    PartsList.SelectedIndex = i;
                    break;
                }
            }
        }
    }
}
