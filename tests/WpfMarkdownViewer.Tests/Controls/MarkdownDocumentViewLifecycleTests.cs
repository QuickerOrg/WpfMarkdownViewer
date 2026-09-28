using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WpfMarkdownViewer.Controls;

namespace WpfMarkdownViewer.Tests.Controls;

/// <summary>
/// Timer lifecycle: the flush pump and the caret blink run only while they have work, stop on Unloaded,
/// resume on Loaded, and never keep the control (or its window) alive through the Dispatcher.
/// </summary>
public class MarkdownDocumentViewLifecycleTests
{
    [WpfFact]
    public void NewView_RunsNoTimers()
    {
        var view = new MarkdownDocumentView();

        Assert.False(view.IsFlushTimerRunning);
        Assert.False(view.IsCaretTimerRunning);
    }

    [WpfFact]
    public void StaticMarkdown_RunsNoTimers()
    {
        var view = new MarkdownDocumentView();
        view.SetMarkdown("# Title\n\nbody");

        Assert.False(view.IsFlushTimerRunning);
        Assert.False(view.IsCaretTimerRunning);
    }

    [WpfFact]
    public async Task AppendDelta_StartsThePump_WhichFlushesAndThenStops()
    {
        var view = new MarkdownDocumentView();

        view.AppendDelta("hello");
        Assert.True(view.IsFlushTimerRunning);

        Assert.True(await WaitUntil(() => !view.IsFlushTimerRunning));
        Assert.Single(view.Document.Blocks); // flushed by the pump itself, no FlushForTest
        Assert.Equal("hello", view.GetAccessibleText().Trim());

        view.AppendDelta(" world");
        Assert.True(view.IsFlushTimerRunning);
        Assert.True(await WaitUntil(() => !view.IsFlushTimerRunning));
        Assert.Equal("hello world", view.GetAccessibleText().Trim());
    }

    [WpfFact]
    public async Task AppendDelta_FromBackgroundThread_IsFlushedAndThePumpStops()
    {
        var view = new MarkdownDocumentView();

        await Task.Run(() =>
        {
            view.AppendDelta("from ");
            view.AppendDelta("worker");
        });

        Assert.True(await WaitUntil(() => view.GetAccessibleText().Trim() == "from worker" && !view.IsFlushTimerRunning));
    }

    [WpfFact]
    public async Task CaretBlinks_OnlyWhileStreamingAndVisible()
    {
        var view = new MarkdownDocumentView();
        var window = OffscreenWindow(view);
        try
        {
            window.Show();
            await Settle();
            Assert.False(view.IsCaretTimerRunning);

            view.AppendDelta("streaming text");
            view.FlushForTest();
            Assert.True(view.IsCaretTimerRunning);

            view.Visibility = Visibility.Collapsed;
            await Settle();
            Assert.False(view.IsCaretTimerRunning);

            view.Visibility = Visibility.Visible;
            await Settle();
            Assert.True(view.IsCaretTimerRunning);

            view.Complete();
            Assert.False(view.IsCaretTimerRunning);
            Assert.False(view.IsFlushTimerRunning);
        }
        finally
        {
            window.Close();
        }
    }

    [WpfFact]
    public async Task Unloaded_StopsTimers_AndLoaded_ResumesThem_Repeatedly()
    {
        var view = new MarkdownDocumentView();
        var holder = new ContentControl { Content = view };
        var window = OffscreenWindow(holder);
        try
        {
            window.Show();
            await Settle();
            view.AppendDelta("first");
            Assert.True(await WaitUntil(() => !view.IsFlushTimerRunning));
            Assert.True(view.IsCaretTimerRunning);

            for (int round = 0; round < 3; round++)
            {
                holder.Content = null;
                await Settle();
                Assert.False(view.IsLoaded);
                Assert.False(view.IsCaretTimerRunning);
                Assert.False(view.IsFlushTimerRunning);

                // Deltas arriving while unloaded are queued, not pumped.
                string before = view.GetAccessibleText();
                view.AppendDelta($" r{round}");
                Assert.False(view.IsFlushTimerRunning);
                await Settle();
                Assert.Equal(before, view.GetAccessibleText());

                holder.Content = view;
                await Settle();
                Assert.True(view.IsLoaded);
                Assert.True(await WaitUntil(() => view.GetAccessibleText().Contains($"r{round}") && !view.IsFlushTimerRunning));
                Assert.True(view.IsCaretTimerRunning);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [WpfFact]
    public async Task Unloaded_EndsAnInProgressDrag_AndItsAutoScrollTimer()
    {
        var view = new MarkdownDocumentView();
        view.SetMarkdown("some selectable text\n\nmore text");
        var host = new MarkdownScrollHost { Content = view };
        var window = OffscreenWindow(host);
        try
        {
            window.Show();
            await Settle();
            Assert.True(view.BeginDragForTest(new Point(20, 20)));
            Assert.True(view.IsAutoScrollTimerRunningForTest);

            window.Content = null;
            await Settle();
            Assert.False(view.IsAutoScrollTimerRunningForTest);
        }
        finally
        {
            window.Close();
        }
    }

    [WpfFact]
    public async Task ClosedWindow_WithAStreamingView_IsCollectable()
    {
        var (viewRef, windowRef) = await ShowStreamAndClose();

        for (int i = 0; i < 10 && (viewRef.IsAlive || windowRef.IsAlive); i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        }

        Assert.False(viewRef.IsAlive, "MarkdownDocumentView must not be kept alive after its window closed.");
        Assert.False(windowRef.IsAlive, "The host window must not be kept alive by the view's timers.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<(WeakReference View, WeakReference Window)> ShowStreamAndClose()
    {
        var view = new MarkdownDocumentView();
        var window = OffscreenWindow(new MarkdownScrollHost { Content = view });
        window.Show();
        await Settle();

        // Mid-stream (never completed): caret blinking and deltas still queued when the window closes.
        view.AppendDelta("# Heading\n\nstreaming paragraph");
        view.FlushForTest();
        view.AppendDelta(" more");
        Assert.True(view.IsCaretTimerRunning);

        window.Close();
        await Settle();
        Assert.False(view.IsCaretTimerRunning);
        Assert.False(view.IsFlushTimerRunning);

        return (new WeakReference(view), new WeakReference(window));
    }

    private static Window OffscreenWindow(object content) => new()
    {
        Width = 400,
        Height = 300,
        Left = -20000,
        Top = -20000,
        ShowActivated = false,
        ShowInTaskbar = false,
        WindowStyle = WindowStyle.None,
        Content = content,
    };

    private static async Task Settle()
    {
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    }

    private static async Task<bool> WaitUntil(Func<bool> condition, int timeoutMs = 3000)
    {
        var sw = Stopwatch.StartNew();
        while (!condition())
        {
            if (sw.ElapsedMilliseconds > timeoutMs)
                return false;
            await Task.Delay(10);
        }
        return true;
    }
}
