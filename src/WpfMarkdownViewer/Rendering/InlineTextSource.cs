using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;
using WpfMarkdownViewer.Model;

namespace WpfMarkdownViewer.Rendering;

/// <summary>Visual parameters for self-drawn text. A minimal light theme for M1 (ADR-0005); real theming arrives in phase E.</summary>
public sealed class TextRenderTheme
{
    public Typeface BaseTypeface { get; init; } = new("Segoe UI");
    public Typeface MonoTypeface { get; init; } = new("Consolas");
    public double EmSize { get; init; } = 15;
    public Brush Foreground { get; init; } = new SolidColorBrush(Color.FromRgb(0x1f, 0x23, 0x28));
    public Brush LinkBrush { get; init; } = new SolidColorBrush(Color.FromRgb(0x0b, 0x66, 0xc2));
    public Brush CodeForeground { get; init; } = new SolidColorBrush(Color.FromRgb(0xc7, 0x25, 0x4e));
    public Brush InlineCodeBackground { get; init; } = new SolidColorBrush(Color.FromRgb(0xf0, 0xf0, 0xf2));
    public Brush CodeBlockBackground { get; init; } = new SolidColorBrush(Color.FromRgb(0xf6, 0xf6, 0xf8));
    public Brush QuoteBar { get; init; } = new SolidColorBrush(Color.FromRgb(0xd0, 0xd7, 0xde));

    public TextRenderTheme() { Freeze(Foreground, LinkBrush, CodeForeground, InlineCodeBackground, CodeBlockBackground, QuoteBar); }

    private static void Freeze(params Brush[] brushes)
    {
        foreach (var b in brushes)
            if (b.CanFreeze) b.Freeze();
    }
}

/// <summary>A <see cref="TextSource"/> that feeds a paragraph's flat <see cref="InlineRun"/> list to WPF's TextFormatter.</summary>
internal sealed class InlineTextSource : TextSource
{
    private readonly InlineProjection _projection;
    private readonly TextRenderTheme _theme;
    private readonly double _emSize;
    private readonly FontWeight _baseWeight;
    private readonly bool _monospace;

    public InlineTextSource(InlineProjection projection, TextRenderTheme theme, double emSize, FontWeight baseWeight, bool monospace = false)
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
        Brush? bg = code ? _theme.InlineCodeBackground : null;
        var decorations = run.LinkTarget is not null ? TextDecorations.Underline : null;

        return new InlineRunProperties(typeface, _emSize, fg, bg, decorations);
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
    public InlineParagraphProperties(TextRunProperties defaultProps) => DefaultTextRunProperties = defaultProps;

    public override FlowDirection FlowDirection => FlowDirection.LeftToRight;
    public override TextAlignment TextAlignment => TextAlignment.Left;
    public override bool FirstLineInParagraph => false;
    public override double LineHeight => 0;
    public override bool AlwaysCollapsible => false;
    public override TextRunProperties DefaultTextRunProperties { get; }
    public override TextWrapping TextWrapping => TextWrapping.Wrap;
    public override TextMarkerProperties? TextMarkerProperties => null;
    public override double Indent => 0;
}
