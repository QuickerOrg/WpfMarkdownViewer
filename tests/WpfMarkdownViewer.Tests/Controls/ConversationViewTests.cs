using System.Windows;
using WpfMarkdownViewer.Controls;

namespace WpfMarkdownViewer.Tests.Controls;

/// <summary>Conversation Shell (ADR-0004 / M3): composing many Documents, streaming to the active message, chrome, theming.</summary>
public class ConversationViewTests
{
    private static void Stream(ConversationView view, string markdown)
    {
        for (int i = 0; i < markdown.Length; i += 3)
        {
            view.AppendDelta(markdown.Substring(i, Math.Min(3, markdown.Length - i)));
            view.FlushActiveForTest();
        }
        view.CompleteMessage();
    }

    [WpfFact]
    public void StreamedTurns_BuildSeparateMessages()
    {
        var view = new ConversationView();

        view.StartMessage(ChatRole.User);
        Stream(view, "帮我解释一下");
        view.StartMessage(ChatRole.Assistant);
        Stream(view, "# 解释\n\n这是 **答案**。");

        Assert.Equal(2, view.MessageCount);
        Assert.Contains("解释", view.MessageTextForTest(1));
        Assert.Contains("这是 答案", view.MessageTextForTest(1)); // inline markup stripped → converged
    }

    [WpfFact]
    public void AppendDelta_BeforeStartMessage_OpensAnAssistantTurn()
    {
        var view = new ConversationView();

        view.AppendDelta("hi");
        view.FlushActiveForTest();

        Assert.Equal(1, view.MessageCount);
        Assert.Contains("hi", view.MessageTextForTest(0));
    }

    [WpfFact]
    public void StartMessage_FinalizesThePreviousActiveTurn()
    {
        var view = new ConversationView();

        view.StartMessage(ChatRole.Assistant);
        view.AppendDelta("first");
        view.FlushActiveForTest();
        view.StartMessage(ChatRole.User); // should finalize the assistant turn without an explicit CompleteMessage

        Assert.Equal(2, view.MessageCount);
        Assert.Contains("first", view.MessageTextForTest(0));
    }

    [WpfFact]
    public void AddMessage_RendersStaticMarkdown()
    {
        var view = new ConversationView();

        view.AddMessage(ChatRole.User, "你好");
        view.AddMessage(ChatRole.Assistant, "# Hi\n\nthere");

        Assert.Equal(2, view.MessageCount);
        Assert.Contains("Hi", view.MessageTextForTest(1));
        Assert.Contains("there", view.MessageTextForTest(1));
    }

    [WpfFact]
    public void AccessibleText_AggregatesRolesAndContent()
    {
        var view = new ConversationView();
        view.AddMessage(ChatRole.User, "问题");
        view.AddMessage(ChatRole.Assistant, "回答");

        string text = view.AccessibleTextForTest();

        Assert.Contains("User: 问题", text);
        Assert.Contains("Assistant: 回答", text);
    }

    [WpfFact]
    public void Clear_RemovesEveryMessage()
    {
        var view = new ConversationView();
        view.AddMessage(ChatRole.User, "a");
        view.AddMessage(ChatRole.Assistant, "b");

        view.Clear();

        Assert.Equal(0, view.MessageCount);
        Assert.Equal(0, view.RealizedCountForTest);
    }

    // --- M3-B: message-level virtualization ---

    private static ConversationView ManyMessages(int count)
    {
        var view = new ConversationView();
        for (int i = 0; i < count; i++)
            view.AddMessage(i % 2 == 0 ? ChatRole.User : ChatRole.Assistant, $"message number {i}\n\nwith a second paragraph");
        Layout(view, 200);
        return view;
    }

    private static void Layout(ConversationView view, double height)
    {
        view.Measure(new Size(400, double.PositiveInfinity));
        view.Arrange(new Rect(0, 0, 400, height));
        view.UpdateLayout();
    }

    [WpfFact]
    public void NoViewport_RealizesEveryMessage()
    {
        var view = ManyMessages(40);

        Assert.Equal(40, view.MessageCount);
        Assert.Equal(40, view.RealizedCountForTest); // no Scroll Host ⇒ realize all
    }

    [WpfFact]
    public void WithSmallViewport_OffscreenMessages_AreVirtualized()
    {
        var view = ManyMessages(40);
        Assert.Equal(40, view.RealizedCountForTest); // heights measured first

        ((IVirtualizingContent)view).SetViewport(0, 200);
        Layout(view, 200);

        int realized = view.RealizedCountForTest;
        Assert.True(realized < 40, $"expected virtualization, but {realized}/40 realized");
        Assert.True(realized >= 1);
    }

    [WpfFact]
    public void ScrollingDown_RealizesLaterMessages()
    {
        var view = ManyMessages(40);
        ((IVirtualizingContent)view).SetViewport(0, 200);
        Layout(view, 200);

        ((IVirtualizingContent)view).SetViewport(2000, 200);
        Layout(view, 200);

        Assert.True(view.RealizedCountForTest >= 1);
        Assert.True(view.RealizedCountForTest < 40);
    }

    [WpfFact]
    public void VirtualizationDisabled_KeepsEveryMessageRealized()
    {
        var view = ManyMessages(40);
        view.VirtualizationEnabled = false;

        ((IVirtualizingContent)view).SetViewport(0, 200);
        Layout(view, 200);

        Assert.Equal(40, view.RealizedCountForTest);
    }

    [WpfFact]
    public void RevirtualizedMessage_RebuildsContent_WhenScrolledBack()
    {
        var view = ManyMessages(40);
        ((IVirtualizingContent)view).SetViewport(2000, 200); // drop the early messages
        Layout(view, 200);
        ((IVirtualizingContent)view).SetViewport(0, 200);    // scroll back to the top
        Layout(view, 200);

        Assert.Contains("message number 0", view.MessageTextForTest(0)); // rebuilt from cached Markdown
    }

    [WpfFact]
    public void ActiveMessage_IsNeverVirtualized()
    {
        var view = ManyMessages(40);
        view.StartMessage(ChatRole.Assistant);
        view.AppendDelta("streaming tail");
        view.FlushActiveForTest();

        ((IVirtualizingContent)view).SetViewport(0, 200); // viewport at the top, active message far below
        Layout(view, 200);

        Assert.Contains("streaming tail", view.MessageTextForTest(view.MessageCount - 1));
    }

    // --- Cross-message selection ---

    private static void Lay(ConversationView view) // realize + arrange so selectable leaves and transforms exist
    {
        view.Measure(new Size(600, double.PositiveInfinity));
        view.Arrange(new Rect(0, 0, 600, 4000));
        view.UpdateLayout();
    }

    [WpfFact]
    public void Selectables_SpanAllMessages_InTopToBottomOrder()
    {
        var view = new ConversationView();
        view.AddMessage(ChatRole.User, "alpha");
        view.AddMessage(ChatRole.Assistant, "beta\n\ngamma");
        Lay(view);

        Assert.Equal(new[] { "alpha", "beta", "gamma" }, view.SelectableTextsForTest());
    }

    [WpfFact]
    public void Selection_AcrossMessageBoundary_CopiesBothMessages()
    {
        var view = new ConversationView();
        view.AddMessage(ChatRole.User, "question");
        view.AddMessage(ChatRole.Assistant, "answer");
        Lay(view);

        // Drag from inside the user message into the assistant message.
        string md = view.SelectAcrossAndGetMarkdownForTest(0, 3, 1, 6);

        Assert.Equal("stion\nanswer", md);
    }

    [WpfFact]
    public void ApplyTheme_KeepsMessagesAndContent()
    {
        var view = new ConversationView();
        view.AddMessage(ChatRole.User, "你好");
        view.AddMessage(ChatRole.Assistant, "world");

        view.ApplyTheme(WpfMarkdownViewer.Rendering.MarkdownStyle.Dark);

        Assert.Equal(2, view.MessageCount);
        Assert.Contains("world", view.MessageTextForTest(1));
    }
}
