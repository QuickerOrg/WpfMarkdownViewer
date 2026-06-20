using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;
using WpfMarkdownViewer.Model;

namespace WpfMarkdownViewer.Rendering;

/// <summary>A <see cref="TextSource"/> that feeds a paragraph's flat <see cref="InlineRun"/> list to WPF's TextFormatter.</summary>
internal sealed class InlineTextSource : TextSource
{
    private readonly InlineProjection _projection;
    private readonly MarkdownStyle _theme;
    private readonly double _emSize;
    private readonly FontWeight _baseWeight;
    private readonly bool _monospace;

    public InlineTextSource(InlineProjection projection, MarkdownStyle theme, double emSize, FontWeight baseWeight, bool monospace = false)
    {
        _projection = projection;
        _theme = theme;
        _emSize = emSize;
        _baseWeight = baseWeight;
        _monospace = monospace;
    }

    public override TextRun GetTextRun(int index)
    {
        string text = _projection.VisibleText;
        if (index >= text.Length)
            return new TextEndOfParagraph(1);

        InlineRun run = RunAt(index);
        int length = run.VisibleEnd - index;
        return new TextCharacters(text, index, length, PropsFor(run));
    }

    public override TextSpan<CultureSpecificCharacterBufferRange> GetPrecedingText(int indexLimit) =>
        new(0, new CultureSpecificCharacterBufferRange(CultureInfo.CurrentCulture, CharacterBufferRange.Empty));

    public override int GetTextEffectCharacterIndexFromTextSourceCharacterIndex(int index) => index;

    private InlineRun RunAt(int index)
    {
        foreach (var run in _projection.Runs)
            if (index >= run.VisibleStart && index < run.VisibleEnd)
                return run;
        // Fallback (should not happen): an unstyled run covering the rest.
        return new InlineRun(index, _projection.VisibleText[index..], InlineStyle.None);
    }

    private InlineRunProperties PropsFor(InlineRun run)
    {
        if (_monospace)
        {
            var mono = new Typeface(_theme.MonoTypeface.FontFamily, FontStyles.Normal, _baseWeight, FontStretches.Normal);
            return new InlineRunProperties(mono, _emSize, _theme.Foreground, null, null);
        }

        bool code = run.Style.HasFlag(InlineStyle.Code);
        var family = code ? _theme.MonoTypeface : _theme.BaseTypeface;
        var weight = run.Style.HasFlag(InlineStyle.Bold) ? FontWeights.Bold : _baseWeight;
        var style = run.Style.HasFlag(InlineStyle.Italic) ? FontStyles.Italic : FontStyles.Normal;
        var typeface = new Typeface(family.FontFamily, style, weight, FontStretches.Normal);

        Brush fg = run.LinkTarget is not null ? _theme.LinkBrush
            : code ? _theme.CodeForeground
            : _theme.Foreground;
        Brush? bg = run.Style.HasFlag(InlineStyle.Highlight) ? _theme.HighlightBackground
            : code ? _theme.InlineCodeBackground
            : null;

        return new InlineRunProperties(typeface, _emSize, fg, bg, BuildDecorations(run));
    }

    private static TextDecorationCollection? BuildDecorations(InlineRun run)
    {
        bool underline = run.LinkTarget is not null || run.Style.HasFlag(InlineStyle.Underline);
        bool strike = run.Style.HasFlag(InlineStyle.Strikethrough);
        if (!underline && !strike)
            return null;

        var decorations = new TextDecorationCollection();
        if (underline)
            decorations.Add(TextDecorations.Underline[0]);
        if (strike)
            decorations.Add(TextDecorations.Strikethrough[0]);
        decorations.Freeze();
        return decorations;
    }
}

internal sealed class InlineRunProperties : TextRunProperties
{
    public InlineRunProperties(Typeface typeface, double emSize, Brush fg, Brush? bg, TextDecorationCollection? decorations)
    {
        Typeface = typeface;
        FontRenderingEmSize = emSize;
        ForegroundBrush = fg;
        BackgroundBrush = bg;
        TextDecorations = decorations;
    }

    public override Typeface Typeface { get; }
    public override double FontRenderingEmSize { get; }
    public override double FontHintingEmSize => FontRenderingEmSize;
    public override TextDecorationCollection? TextDecorations { get; }
    public override Brush ForegroundBrush { get; }
    public override Brush? BackgroundBrush { get; }
    public override CultureInfo CultureInfo => CultureInfo.CurrentCulture;
    public override TextEffectCollection? TextEffects => null;
}

internal sealed class InlineParagraphProperties : TextParagraphProperties
{
    public InlineParagraphProperties(TextRunProperties defaultProps, double lineHeight)
    {
        DefaultTextRunProperties = defaultProps;
        LineHeight = lineHeight;
    }

    public override FlowDirection FlowDirection => FlowDirection.LeftToRight;
    public override TextAlignment TextAlignment => TextAlignment.Left;
    public override bool FirstLineInParagraph => false;
    public override double LineHeight { get; }
    public override bool AlwaysCollapsible => true;
    public override TextRunProperties DefaultTextRunProperties { get; }
    public override TextWrapping TextWrapping => TextWrapping.Wrap;
    public override TextMarkerProperties? TextMarkerProperties => null;
    public override double Indent => 0;
}
