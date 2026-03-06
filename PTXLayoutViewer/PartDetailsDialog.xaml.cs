using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PTXLayoutViewer.Models;

namespace PTXLayoutViewer;

public partial class PartDetailsDialog : Window
{
    private static readonly Brush LabelBrush = new SolidColorBrush(Color.FromRgb(0x94, 0xa3, 0xb8));
    private static readonly Brush ValueBrush = new SolidColorBrush(Color.FromRgb(0xf1, 0xf5, 0xf9));

    public PartDetailsDialog()
    {
        InitializeComponent();
    }

    public void SetPart(PtxPart? part, LayoutRectangle? layout)
    {
        TitleText.Text = part != null && !string.IsNullOrEmpty(part.PartName)
            ? part.PartName
            : part != null ? $"Part {part.PartIndex}" : "Part";

        DetailsPanel.Children.Clear();
        if (part == null)
        {
            DetailsPanel.Children.Add(DetailRow("(No part data)", ""));
            return;
        }

        AddDetail("Part index", part.PartIndex.ToString());
        AddDetail("Part name", part.PartName);
        AddDetail("Job index", part.JobIndex.ToString());
        AddDetail("Width", UnitHelpers.FormatMmAndInch(part.WidthMm));
        AddDetail("Height", UnitHelpers.FormatMmAndInch(part.HeightMm));
        AddDetail("Quantity required", part.QtyReq.ToString());

        if (layout != null)
        {
            DetailsPanel.Children.Add(Spacer());
            AddDetail("Layout position X", UnitHelpers.FormatMmAndInch(layout.XMm));
            AddDetail("Layout position Y", UnitHelpers.FormatMmAndInch(layout.YMm));
            AddDetail("Layout width", UnitHelpers.FormatMmAndInch(layout.WidthMm));
            AddDetail("Layout height", UnitHelpers.FormatMmAndInch(layout.HeightMm));
        }
    }

    private void AddDetail(string label, string value)
    {
        DetailsPanel.Children.Add(DetailRow(label, value));
    }

    private static UIElement DetailRow(string label, string value)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var lbl = new TextBlock
        {
            Text = label + ":",
            Foreground = LabelBrush,
            Margin = new Thickness(0, 0, 12, 6)
        };
        var val = new TextBlock
        {
            Text = value,
            Foreground = ValueBrush,
            Margin = new Thickness(0, 0, 0, 6),
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(lbl, 0);
        Grid.SetColumn(val, 1);
        grid.Children.Add(lbl);
        grid.Children.Add(val);
        return grid;
    }

    private static UIElement Spacer()
    {
        return new Border { Height = 12, Margin = new Thickness(0, 8, 0, 0) };
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
