using TextMateSharp.Grammars;
using TextMateSharp.Registry;
using TextMateSharp.Themes;

namespace WpfMarkdownViewer.Highlighting;

/// <summary>A run of code with a resolved foreground color (hex, or null for the default).</summary>
public sealed record ColoredSpan(string Text, string? ColorHex);

/// <summary>
/// Per-line syntax highlighting via TextMate grammars (ADR-0003, TextMateSharp). Stateful across lines
/// (carries the rule stack) so multi-line constructs highlight correctly and incomplete code is tolerated.
/// Best-effort: any failure or unknown language falls back to a single uncolored span per line.
/// </summary>
public sealed class CodeHighlighter
{
    private static readonly RegistryOptions Options = new(ThemeName.LightPlus);
    private static readonly Registry Registry = new(Options);
    private static readonly Theme Theme = Registry.GetTheme();

    public IReadOnlyList<IReadOnlyList<ColoredSpan>> Highlight(string code, string? language)
    {
        string[] lines = code.Replace("\r\n", "\n").Split('\n');
        IGrammar? grammar = ResolveGrammar(language);
        if (grammar is null)
            return lines.Select(Plain).ToList();

        var result = new List<IReadOnlyList<ColoredSpan>>(lines.Length);
        IStateStack? state = null;
        foreach (string line in lines)
        {
            ITokenizeLineResult tokenized;
            try
            {
                tokenized = grammar.TokenizeLine(line, state, TimeSpan.MaxValue);
            }
            catch
            {
                result.Add(Plain(line));
                continue;
            }

            state = tokenized.RuleStack;
            var spans = new List<ColoredSpan>();
            foreach (IToken token in tokenized.Tokens)
            {
                int start = Math.Clamp(token.StartIndex, 0, line.Length);
                int end = Math.Clamp(token.EndIndex, 0, line.Length);
                if (end <= start)
                    continue;
                spans.Add(new ColoredSpan(line[start..end], ColorFor(token.Scopes)));
            }
            if (spans.Count == 0)
                spans.Add(new ColoredSpan(line, null));
            result.Add(spans);
        }
        return result;
    }

    private static IReadOnlyList<ColoredSpan> Plain(string line) => new[] { new ColoredSpan(line, null) };

    private static IGrammar? ResolveGrammar(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return null;
        try
        {
            string id = Alias(language.Trim().ToLowerInvariant());
            string scope = Options.GetScopeByLanguageId(id);
            return string.IsNullOrEmpty(scope) ? null : Registry.LoadGrammar(scope);
        }
        catch
        {
            return null;
        }
    }

    private static string? ColorFor(List<string> scopes)
    {
        foreach (ThemeTrieElementRule rule in Theme.Match(scopes))
            if (rule.foreground > 0)
                return Theme.GetColor(rule.foreground);
        return null;
    }

    private static string Alias(string lang) => lang switch
    {
        "cs" or "c#" => "csharp",
        "js" => "javascript",
        "ts" => "typescript",
        "py" => "python",
        "sh" or "bash" or "zsh" => "shellscript",
        "yml" => "yaml",
        "md" => "markdown",
        "ps1" => "powershell",
        _ => lang,
    };
}
