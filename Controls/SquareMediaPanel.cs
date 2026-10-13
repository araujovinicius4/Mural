using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MuralInstitucional.Controls;

/// <summary>Media content cannot influence the allocated 1:1 surface.</summary>
public sealed class SquareMediaPanel : Panel
{
    protected override Size MeasureOverride(Size constraint)
    {
        double side = double.IsInfinity(constraint.Width) ? 240 : Math.Max(0, constraint.Width);
        foreach (UIElement child in InternalChildren) child.Measure(new Size(side, side));
        return new Size(side, side);
    }
    protected override Size ArrangeOverride(Size arrangeSize)
    {
        double side = Math.Min(arrangeSize.Width, arrangeSize.Height);
        // Grid auto tracks can expand to an image's UniformToFill desired size.
        // Allocate the same square slot to every layer so overflow remains centered.
        foreach (UIElement child in InternalChildren)
        {
            if (child is MediaElement { NaturalVideoWidth: > 0, NaturalVideoHeight: > 0 } video)
            {
                double scale = Math.Max(side / video.NaturalVideoWidth, side / video.NaturalVideoHeight);
                double width = video.NaturalVideoWidth * scale, height = video.NaturalVideoHeight * scale;
                child.Arrange(new Rect((side - width) / 2, (side - height) / 2, width, height));
            }
            else child.Arrange(new Rect(0, 0, side, side));
        }
        Clip = new RectangleGeometry(new Rect(0, 0, side, side), 12, 12);
        return new Size(side, side);
    }
}
