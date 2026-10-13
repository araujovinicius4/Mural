using System.Windows;
using System.Windows.Media;

namespace MuralInstitucional.Controls;

/// <summary>Draws at uniform cover scale; source aspect ratio never changes.</summary>
public sealed class CoverImage : FrameworkElement
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source), typeof(ImageSource), typeof(CoverImage),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public ImageSource? Source { get => (ImageSource?)GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (Source is not { Width: > 0, Height: > 0 } image) return;
        double scale = Math.Max(ActualWidth / image.Width, ActualHeight / image.Height);
        double width = image.Width * scale, height = image.Height * scale;
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)));
        dc.DrawImage(image, new Rect((ActualWidth - width) / 2, (ActualHeight - height) / 2, width, height));
        dc.Pop();
    }
}
