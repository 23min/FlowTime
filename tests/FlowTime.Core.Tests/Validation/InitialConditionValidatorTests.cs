using System;
using FlowTime.Core.Models;
using FlowTime.Core.Validation;
using Xunit;

namespace FlowTime.Core.Tests.Validation;

public class InitialConditionValidatorTests
{
    [Fact]
    public void Validate_WithMatchingQueueDepth_DoesNotThrow()
    {
        var data = new NodeData
        {
            NodeId = "OrderQueue",
            Arrivals = new[] { 120d },
            Served = new[] { 100d },
            Errors = new[] { 5d },
            QueueDepth = new[] { 35d }
        };

        var initial = new InitialCondition { QueueDepth = 20d };
        var validator = new InitialConditionValidator();

        validator.Validate(data, initial);
    }

    [Fact]
    public void Validate_WithMissingQueueDepth_DoesNotThrow()
    {
        var data = new NodeData
        {
            NodeId = "OrderService",
            Arrivals = new[] { 50d },
            Served = new[] { 45d },
            Errors = new[] { 2d }
        };

        var validator = new InitialConditionValidator();

        validator.Validate(data, null);
    }

    [Fact]
    public void Validate_WithMismatchedQueueDepth_Throws()
    {
        var data = new NodeData
        {
            NodeId = "OrderQueue",
            Arrivals = new[] { 120d },
            Served = new[] { 110d },
            Errors = new[] { 5d },
            QueueDepth = new[] { 5d }
        };

        var initial = new InitialCondition { QueueDepth = 10d };
        var validator = new InitialConditionValidator();

        var ex = Assert.Throws<InvalidOperationException>(() => validator.Validate(data, initial));
        Assert.Contains("OrderQueue", ex.Message);
    }
}
