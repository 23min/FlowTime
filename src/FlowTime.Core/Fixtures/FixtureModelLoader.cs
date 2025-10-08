using System;
using System.Collections.Generic;
using System.IO;
using FlowTime.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FlowTime.Core.Fixtures;

public static class FixtureModelLoader
{
    public static ModelMetadata LoadMetadata(string fixtureName)
    {
        if (string.IsNullOrWhiteSpace(fixtureName))
            throw new ArgumentException("Fixture name must be provided", nameof(fixtureName));

        var fixtureDir = GetFixtureDirectory(fixtureName);
        var modelPath = Path.Combine(fixtureDir, "model.yaml");
        if (!File.Exists(modelPath))
            throw new FileNotFoundException($"Fixture model not found: {modelPath}", modelPath);

        var yaml = File.ReadAllText(modelPath);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var document = deserializer.Deserialize<FixtureDocument>(yaml)
            ?? throw new InvalidOperationException($"Failed to deserialize fixture model: {modelPath}");

        var definition = document.ToModelDefinition();

        return ModelParser.ParseMetadata(definition, fixtureDir);
    }

    private static string GetFixtureDirectory(string fixtureName)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        return Path.Combine(root, "fixtures", fixtureName);
    }
}

internal sealed class FixtureDocument
{
    public FixtureWindow? Window { get; init; }
    public FixtureTopology? Topology { get; init; }
    public List<FixtureVariable>? Variables { get; init; }
    public List<FixtureConstraint>? Constraints { get; init; }

    public ModelDefinition ToModelDefinition()
    {
        if (Window is null)
            throw new InvalidOperationException("Fixture model missing window section.");
        if (Topology is null)
            throw new InvalidOperationException("Fixture model missing topology section.");

        var definition = new ModelDefinition
        {
            SchemaVersion = 1,
            Grid = new GridDefinition
            {
                Bins = Window.Bins,
                BinSize = Window.BinSize,
                BinUnit = Window.BinUnit ?? "minutes",
                StartTimeUtc = Window.StartTimeUtc
            },
            Nodes = new List<NodeDefinition>(),
            Outputs = new List<OutputDefinition>(),
            Topology = new TopologyDefinition
            {
                Nodes = Topology.Nodes?.ConvertAll(ToTopologyNode) ?? new List<TopologyNodeDefinition>(),
                Edges = Topology.Edges?.ConvertAll(ToTopologyEdge) ?? new List<TopologyEdgeDefinition>()
            }
        };

        return definition;
    }

    private static TopologyNodeDefinition ToTopologyNode(FixtureNode node)
    {
        if (node.Semantics is null)
            throw new InvalidOperationException($"Fixture node '{node.Id}' missing semantics section.");

        return new TopologyNodeDefinition
        {
            Id = node.Id ?? throw new InvalidOperationException("Fixture node missing id."),
            Semantics = new TopologyNodeSemanticsDefinition
            {
                Arrivals = Require(node.Semantics.Arrivals, node.Id, "arrivals"),
                Served = Require(node.Semantics.Served, node.Id, "served"),
                Errors = Require(node.Semantics.Errors, node.Id, "errors"),
                ExternalDemand = node.Semantics.ExternalDemand,
                QueueDepth = node.Semantics.QueueDepth
            },
            InitialCondition = node.InitialCondition != null
                ? new InitialConditionDefinition { QueueDepth = node.InitialCondition.QueueDepth }
                : null
        };
    }

    private static TopologyEdgeDefinition ToTopologyEdge(FixtureEdge edge) => new()
    {
        Source = edge.Source ?? throw new InvalidOperationException("Fixture edge missing source."),
        Target = edge.Target ?? throw new InvalidOperationException("Fixture edge missing target."),
        Weight = edge.Weight == 0 ? 1.0 : edge.Weight
    };

    private static string Require(string? value, string nodeId, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Fixture node '{nodeId}' missing semantics.{name} value.");
        return value;
    }
}

internal sealed class FixtureWindow
{
    public int Bins { get; init; }
    public int BinSize { get; init; }
    public string? BinUnit { get; init; }
    public string? StartTimeUtc { get; init; }
}

internal sealed class FixtureTopology
{
    public List<FixtureNode>? Nodes { get; init; }
    public List<FixtureEdge>? Edges { get; init; }
}

internal sealed class FixtureNode
{
    public string? Id { get; init; }
    public FixtureSemantics? Semantics { get; init; }
    public FixtureInitialCondition? InitialCondition { get; init; }
}

internal sealed class FixtureSemantics
{
    public string? Arrivals { get; init; }
    public string? Served { get; init; }
    public string? Errors { get; init; }
    public string? ExternalDemand { get; init; }
    public string? QueueDepth { get; init; }
}

internal sealed class FixtureInitialCondition
{
    public double QueueDepth { get; init; }
}

internal sealed class FixtureEdge
{
    public string? Source { get; init; }
    public string? Target { get; init; }
    public double Weight { get; init; }
}

internal sealed class FixtureVariable { }
internal sealed class FixtureConstraint { }
