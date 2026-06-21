using System.Windows.Media;

namespace WpfMarkdownViewer.Rendering;

/// <summary>SharpVectors implementation of <see cref="ISvgRenderer"/>: SVG bytes → a frozen, scalable WPF <see cref="DrawingImage"/>.</summary>
public sealed class SvgRenderer : ISvgRenderer
{
    public ImageSource? Render(byte[] svgBytes) => SvgImage.Parse(svgBytes);
}
