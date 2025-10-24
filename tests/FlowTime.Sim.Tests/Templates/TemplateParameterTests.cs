using FlowTime.Sim.Core.Templates;
using Xunit;

namespace FlowTime.Sim.Tests.Templates;

public class TemplateParameterTests
{
    [Fact]
    public void TemplateParameter_ShouldExposeArrayOfHint()
    {
        var parameter = new TemplateParameter();

        Assert.Null(parameter.ArrayOf);

        parameter.ArrayOf = "double";

        Assert.Equal("double", parameter.ArrayOf);
    }
}
