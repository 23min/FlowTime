using System;
using System.Collections.Generic;
using Bunit;
using FlowTime.UI.Components.TimeTravel;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FlowTime.UI.Tests.TimeTravel;

public sealed class ClassSelectorRenderTests : TestContext
{
    private readonly NavigationManager navigation;

    public ClassSelectorRenderTests()
    {
        navigation = Services.GetRequiredService<NavigationManager>();
    }

    [Fact]
    public void RendersSingleClassMessage()
    {
        navigation.NavigateTo("http://localhost/time-travel/dashboard");

        var cut = RenderComponent<ClassSelector>(parameters => parameters.Add(p => p.Classes, Array.Empty<string>()));

        var message = cut.Find("[data-testid='class-selector-empty']");
        Assert.Contains("Single-class model", message.TextContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SelectingClassUpdatesQuery()
    {
        var cut = RenderSelector(new[] { "Order", "Refund", "VIP" });
        var allChip = cut.Find("[data-testid='class-chip-all']");
        Assert.Equal("true", allChip.GetAttribute("aria-pressed"));

        var refundChip = cut.Find("[data-testid='class-chip-Refund']");
        refundChip.Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("Refund", ReadQuery()["classes"]);
            Assert.Equal("false", allChip.GetAttribute("aria-pressed"));
            Assert.Equal("true", refundChip.GetAttribute("aria-pressed"));
        });
    }

    [Fact]
    public void MultiSelectionCapsAtThree()
    {
        var cut = RenderSelector(new[] { "Order", "Refund", "VIP", "Wholesale" });

        cut.Find("[data-testid='class-chip-Order']").Click();
        cut.Find("[data-testid='class-chip-Refund']").Click();
        cut.Find("[data-testid='class-chip-VIP']").Click();
        cut.Find("[data-testid='class-chip-Wholesale']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("Order,Refund,VIP", ReadQuery()["classes"]);
        });
    }

    [Fact]
    public void ResetToAllClearsQuery()
    {
        var cut = RenderSelector(new[] { "Order", "Refund" });

        cut.Find("[data-testid='class-chip-Order']").Click();
        cut.WaitForAssertion(() => Assert.Equal("Order", ReadQuery()["classes"]));

        cut.Find("[data-testid='class-chip-all']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.False(ReadQuery().ContainsKey("classes"));
            Assert.Equal("true", cut.Find("[data-testid='class-chip-all']").GetAttribute("aria-pressed"));
        });
    }

    private IRenderedComponent<ClassSelector> RenderSelector(string[] classes)
    {
        navigation.NavigateTo("http://localhost/time-travel/dashboard");
        return RenderComponent<ClassSelector>(parameters => parameters.Add(p => p.Classes, classes));
    }

    private Dictionary<string, string> ReadQuery()
    {
        var uri = new Uri(navigation.Uri);
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(uri.Query))
        {
            return dict;
        }

        var trimmed = uri.Query.TrimStart('?');
        foreach (var pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kvp = pair.Split('=', 2);
            if (kvp.Length == 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(kvp[0]);
            var value = kvp.Length > 1 ? Uri.UnescapeDataString(kvp[1]) : string.Empty;
            dict[key] = value;
        }

        return dict;
    }
}
