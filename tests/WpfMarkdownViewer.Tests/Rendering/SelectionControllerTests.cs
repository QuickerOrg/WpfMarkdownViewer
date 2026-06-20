using System.Windows;
using System.Windows.Controls;
using WpfMarkdownViewer.Rendering;

namespace WpfMarkdownViewer.Tests.Rendering;

public class SelectionControllerTests
{
    private static SelectionController WithViewport(double top, double height)
    {
        var controller = new SelectionController(new Grid());
        controller.EnableAutoScroll(() => (top, height), _ => { });
        return controller;
    }

    [WpfFact]
    public void EdgeVelocity_InsideBand_IsZero()
    {
        var c = WithViewport(100, 200); // band [100, 300]
        Assert.Equal(0, c.EdgeVelocityForTest(new Point(0, 200)));
    }

    [WpfFact]
    public void EdgeVelocity_PastTop_IsNegative_PastBottom_IsPositive()
    {
        var c = WithViewport(100, 200);
        Assert.True(c.EdgeVelocityForTest(new Point(0, 110)) < 0); // near/above top edge
        Assert.True(c.EdgeVelocityForTest(new Point(0, 295)) > 0); // near/below bottom edge
    }

    [WpfFact]
    public void EdgeVelocity_IsClampedToMaxStep()
    {
        var c = WithViewport(100, 200);
        double far = c.EdgeVelocityForTest(new Point(0, 1000)); // way past the bottom
        Assert.Equal(18, far, 3); // MaxStep
    }

    [WpfFact]
    public void EdgeVelocity_WithoutAutoScroll_IsZero()
    {
        var c = new SelectionController(new Grid()); // not configured
        Assert.Equal(0, c.EdgeVelocityForTest(new Point(0, 9999)));
    }
}
