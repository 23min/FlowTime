using System;
using System.Collections.Generic;
using FlowTime.Core.Models;
using FlowTime.Core.Validation;

namespace FlowTime.Core.Fixtures;

public static class FixtureRunBuilder
{
    public static FixtureRun Build(string fixtureName)
    {
        var metadata = FixtureModelLoader.LoadMetadata(fixtureName);
        if (metadata.Topology is null)
            throw new InvalidOperationException($"Fixture '{fixtureName}' does not define a topology.");

        var loader = new FixtureSemanticLoader(metadata, fixtureName);
        var validator = new InitialConditionValidator();
        var nodes = new Dictionary<string, NodeData>();

        foreach (var topologyNode in metadata.Topology.Nodes)
        {
            var data = loader.LoadNode(topologyNode.Id);
            validator.Validate(data, topologyNode.InitialCondition);
            nodes[topologyNode.Id] = data;
        }

        return new FixtureRun
        {
            Metadata = metadata,
            Nodes = nodes
        };
    }
}

public sealed record FixtureRun
{
    public required ModelMetadata Metadata { get; init; }
    public required IReadOnlyDictionary<string, NodeData> Nodes { get; init; }
}
