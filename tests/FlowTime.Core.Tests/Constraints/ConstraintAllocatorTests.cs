using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FlowTime.Core.Tests.Constraints;

public class ConstraintAllocatorTests
{
    [Fact]
    public void AllocateProportional_DistributesCapacityByDemand()
    {
        var allocatorType = Type.GetType("FlowTime.Core.Constraints.ConstraintAllocator, FlowTime.Core");
        Assert.NotNull(allocatorType);

        var method = allocatorType!.GetMethod("AllocateProportional");
        Assert.NotNull(method);

        var demands = new Dictionary<string, double>
        {
            ["service_a"] = 40d,
            ["service_b"] = 60d
        };

        var result = method!.Invoke(null, new object[] { demands, 50d });
        Assert.NotNull(result);

        var allocation = Assert.IsAssignableFrom<IReadOnlyDictionary<string, double>>(result);
        Assert.Equal(2, allocation.Count);

        Assert.Equal(20d, allocation["service_a"], 6);
        Assert.Equal(30d, allocation["service_b"], 6);
        Assert.Equal(50d, allocation.Values.Sum(), 6);
    }
}
