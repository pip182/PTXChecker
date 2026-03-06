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
    private const double ScalePxPerMm = 0.35;
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
                LayoutCanvas.Children.Clear();
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

        RenderLayout(rects, board);
        StatusText.Text = $"{rects.Count} part(s) — scale {ScalePxPerMm} px/mm";
    }

    private void RenderLayout(IReadOnlyList<LayoutRectangle> rectangles, PtxBoard? board)
    {
        LayoutCanvas.Children.Clear();

        double boardW = board?.LengthMm ?? 0;
        double boardH = board?.WidthMm ?? 0;
        foreach (var r in rectangles)
        {
            boardW = Math.Max(boardW, r.XMm + r.WidthMm);
            boardH = Math.Max(boardH, r.YMm + r.HeightMm);
        }
        if (boardW <= 0) boardW = 1000;
        if (boardH <= 0) boardH = 800;

        double scale = ScalePxPerMm;
        double canvasW = boardW * scale;
        double canvasH = boardH * scale;

        LayoutCanvas.Width = canvasW;
        LayoutCanvas.Height = canvasH;

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
        var part = _document.GetPart(layout.PartIndex);
        var dlg = new PartDetailsDialog
        {
            Owner = this
        };
        dlg.SetPart(part, layout);
        dlg.ShowDialog();
    }
}
