using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfMarkdownViewer.Rendering;

namespace WpfMarkdownViewer.Demo;

public partial class MainWindow : Window
{
    private const string Sample =
        "# WpfMarkdownViewer\n\n" +
        "这是一个**自绘**的、支持*增量流式*渲染的 Markdown 组件，目标是接近 `ChatGPT` 的网页效果，并且在长回复下保持流畅。\n\n" +
        "## 核心特性\n\n" +
        "- 块级增量渲染，只重绘 Active Block\n" +
        "- 流式预览必须 **Converge** 到 Markdig\n" +
        "- 自适应节流，平滑流动\n\n" +
        "下面是一段示例代码：\n\n" +
        "```csharp\n" +
        "public sealed class Demo\n" +
        "{\n" +
        "    public IReadOnlyList<MdBlock> Render(string markdown)\n" +
        "        => parser.ToBlocks(markdown);\n" +
        "}\n" +
        "```\n\n" +
        "> 设计要点：finalize 时不应有可见跳变 —— 这一点由 Converge 测试守护。\n\n" +
        "## 三种实现路线\n\n" +
        "| 路线 | 优点 | 建议 |\n" +
        "| --- | --- | --- |\n" +
        "| WebView2 | 接近网页、生态成熟 | 快速验证 |\n" +
        "| FlowDocument | 实现快、纯 WPF | 仅 MVP |\n" +
        "| 自研 Renderer | 性能与体验最好 | 长期推荐 |\n\n" +
        "更多内容见 [项目文档](https://example.com) 与各 ADR。\n";

    private DispatcherTimer? _feeder;
    private int _pos;
    private bool _dark;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => Replay();
    }

    private void OnReplay(object sender, RoutedEventArgs e) => Replay();

    private void OnToggleTheme(object sender, RoutedEventArgs e)
    {
        _dark = !_dark;
        ApplyTheme(_dark);
    }

    private void ApplyTheme(bool dark)
    {
        var theme = dark ? TextRenderTheme.Dark : TextRenderTheme.Light;
        Viewer.ApplyTheme(theme);
        var bg = theme.Background;
        Root.Background = bg;
        Host.Background = bg;
        ThemeButton.Content = dark ? "切换浅色" : "切换深色";
    }

    private void Replay()
    {
        _feeder?.Stop();
        Viewer.Reset();
        _pos = 0;
        StatusText.Text = "流式输出中…";

        _feeder = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) };
        _feeder.Tick += (_, _) =>
        {
            if (_pos >= Sample.Length)
            {
                _feeder!.Stop();
                Viewer.Complete();
                StatusText.Text = "完成（Markdig 权威 finalize）";
                Dispatcher.BeginInvoke(new Action(SaveSnapshots), DispatcherPriority.ApplicationIdle);
                return;
            }

            int n = Math.Min(Random.Shared.Next(1, 5), Sample.Length - _pos);
            Viewer.AppendDelta(Sample.Substring(_pos, n));
            _pos += n;
        };
        _feeder.Start();
    }

    private void SaveSnapshots()
    {
        ApplyTheme(dark: false);
        Save("demo.png");
        ApplyTheme(dark: true);
        Save("demo-dark.png");
        ApplyTheme(_dark); // restore whatever the user had
        StatusText.Text = "完成；已保存浅色/深色快照";
    }

    private void Save(string fileName)
    {
        try
        {
            Viewer.UpdateLayout();
            int w = Math.Max(1, (int)Math.Ceiling(Viewer.ActualWidth));
            int h = Math.Max(1, (int)Math.Ceiling(Viewer.ActualHeight));
            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(Viewer);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));

            string dir = @"D:\Work_Try\WpfMarkdownViewer\artifacts";
            Directory.CreateDirectory(dir);
            using var fs = File.Create(Path.Combine(dir, fileName));
            encoder.Save(fs);
        }
        catch (Exception ex)
        {
            StatusText.Text = "快照失败：" + ex.Message;
        }
    }
}
