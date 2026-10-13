using System.Windows;
using System.Windows.Controls;

namespace MuralInstitucional.Controls;

/// <summary>At most three columns, with independent row heights and no horizontal overflow.</summary>
public sealed class ResponsiveCardPanel : Panel
{
    public static int ColumnsFor(double width) => width >= 780 ? 3 : width >= 520 ? 2 : 1;
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 900 : Math.Max(0, availableSize.Width);
        var columns = ColumnsFor(width);
        var cellWidth = width / columns;
        double total = 0, rowHeight = 0;
        for (int i = 0; i < InternalChildren.Count; i++)
        {
            InternalChildren[i].Measure(new Size(cellWidth, double.PositiveInfinity));
            rowHeight = Math.Max(rowHeight, InternalChildren[i].DesiredSize.Height);
            if ((i + 1) % columns == 0 || i == InternalChildren.Count - 1) { total += rowHeight; rowHeight = 0; }
        }
        return new Size(width, total);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        int columns = ColumnsFor(finalSize.Width);
        double cellWidth = finalSize.Width / columns, y = 0;
        for (int start = 0; start < InternalChildren.Count; start += columns)
        {
            var count = Math.Min(columns, InternalChildren.Count - start);
            var height = Enumerable.Range(start, count).Max(i => InternalChildren[i].DesiredSize.Height);
            for (int j = 0; j < count; j++)
                InternalChildren[start + j].Arrange(new Rect(j * cellWidth, y, cellWidth, InternalChildren[start + j].DesiredSize.Height));
            y += height;
        }
        return finalSize;
    }
}
