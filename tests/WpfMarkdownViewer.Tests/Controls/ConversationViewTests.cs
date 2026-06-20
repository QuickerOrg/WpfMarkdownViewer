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
