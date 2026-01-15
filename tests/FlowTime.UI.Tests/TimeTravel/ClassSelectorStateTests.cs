using System.Collections.Generic;
using FlowTime.UI.TimeTravel;
using Xunit;

namespace FlowTime.UI.Tests.TimeTravel;

public sealed class ClassSelectorStateTests
{
    private static readonly IReadOnlyList<string> Classes = new[] { "Order", "Refund", "VIP", "Wholesale" };

    [Fact]
    public void DefaultsToAllWhenNoSelection()
    {
        var state = new ClassSelectionState(Classes);

        Assert.Equal(ClassSelectionMode.All, state.Mode);
        Assert.Empty(state.SelectedClasses);
        Assert.Null(state.GetQueryValue());
    }

    [Fact]
    public void AppliesQueryValueForSingleClass()
    {
        var state = new ClassSelectionState(Classes);

        state.ApplyQueryValue("Refund");

        Assert.Equal(ClassSelectionMode.Single, state.Mode);
        Assert.Equal(new[] { "Refund" }, state.SelectedClasses);
        Assert.Equal("Refund", state.GetQueryValue());
    }

    [Fact]
    public void ToggleSupportsMultiSelectUpToThree()
    {
        var state = new ClassSelectionState(Classes);

        state.Toggle("Order");
        state.Toggle("Refund");
        state.Toggle("VIP");
        state.Toggle("Wholesale"); // should be ignored due to limit

        Assert.Equal(ClassSelectionMode.Multi, state.Mode);
        Assert.Equal(3, state.SelectedClasses.Count);
        Assert.DoesNotContain("Wholesale", state.SelectedClasses);
        Assert.Equal(new[] { "Order", "Refund", "VIP" }, state.SelectedClasses);
        Assert.Equal("Order,Refund,VIP", state.GetQueryValue());
    }
}
