using System;
using System.IO;
using System.Linq;
using FlowTime.Core.DataSources;
using FlowTime.Core.Models;

namespace FlowTime.Core.Fixtures;

public sealed class FixtureSemanticLoader
{
    private readonly ModelMetadata metadata;
    private readonly SemanticLoader loader;

    public FixtureSemanticLoader(ModelMetadata metadata, string fixtureName)
    {
        this.metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        if (string.IsNullOrWhiteSpace(fixtureName))
            throw new ArgumentException("Fixture name must be provided", nameof(fixtureName));

        var fixtureDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "fixtures", fixtureName));
        loader = new SemanticLoader(fixtureDir);
    }

    public NodeData LoadNode(string nodeId)
    {
        if (metadata.Topology == null)
            throw new InvalidOperationException("Fixture metadata does not contain topology information.");

        var node = metadata.Topology.Nodes.FirstOrDefault(n => string.Equals(n.Id, nodeId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Node '{nodeId}' not found in fixture topology.");

        return loader.LoadNodeData(node, metadata.Window.Bins);
    }
}
