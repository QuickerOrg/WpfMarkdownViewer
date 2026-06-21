using System.Runtime.CompilerServices;
using WpfMarkdownViewer.Highlighting;
using WpfMarkdownViewer.Rendering;

namespace WpfMarkdownViewer.Tests;

/// <summary>Registers the built-in rendering capabilities once before any test runs (mirrors what a host app does at startup).</summary>
internal static class TestBootstrap
{
    [ModuleInitializer]
    internal static void RegisterCapabilities()
    {
        Capabilities.Highlighting = new TextMateHighlighter();
        Capabilities.Math = new WpfMathRenderer();
        Capabilities.Svg = new SvgRenderer();
        Capabilities.Mermaid = new BuiltInMermaidRenderer();
    }
}
