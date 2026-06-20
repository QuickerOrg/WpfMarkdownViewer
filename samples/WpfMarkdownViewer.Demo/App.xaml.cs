using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace WpfMarkdownViewer.Demo;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Log((args.ExceptionObject as Exception)?.ToString() ?? "unknown");
        base.OnStartup(e);
    }

    private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log(e.Exception.ToString());
        // Keep the process alive long enough that the log is flushed; then exit non-zero.
        e.Handled = true;
        Shutdown(1);
    }

    private static void Log(string text)
    {
        try
        {
            string dir = @"D:\Work_Try\WpfMarkdownViewer\artifacts";
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "error.log"), text);
        }
        catch { /* best effort */ }
    }
}
