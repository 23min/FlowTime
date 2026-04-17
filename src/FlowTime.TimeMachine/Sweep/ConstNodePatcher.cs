using System.Globalization;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace FlowTime.TimeMachine.Sweep;

/// <summary>
/// Patches a const node's values array in a model YAML document.
/// Used by <see cref="SweepRunner"/> to substitute parameter values before each evaluation.
/// </summary>
public static class ConstNodePatcher
{
    /// <summary>
    /// Returns a copy of <paramref name="modelYaml"/> where the named const node's
    /// <c>values</c> array is replaced with an array filled with <paramref name="value"/>
    /// (same bin count as the original).
    /// <para>
    /// Returns the original YAML unchanged if the node is not found, is not a
    /// <c>const</c> node, or has no <c>values</c> key.
    /// </para>
    /// </summary>
    public static string Patch(string modelYaml, string nodeId, double value)
    {
        var stream = new YamlStream();
        using var reader = new StringReader(modelYaml);
        stream.Load(reader);

        if (stream.Documents.Count == 0)
            return modelYaml;

        var root = stream.Documents[0].RootNode as YamlMappingNode;
        if (root is null)
            return modelYaml;

        var nodesValue = GetValue(root, "nodes");
        if (nodesValue is not YamlSequenceNode nodesSeq)
            return modelYaml;

        foreach (var entry in nodesSeq)
        {
            if (entry is not YamlMappingNode nodeMap)
                continue;

            var idNode = GetValue(nodeMap, "id") as YamlScalarNode;
            var kindNode = GetValue(nodeMap, "kind") as YamlScalarNode;

            if (idNode?.Value != nodeId || kindNode?.Value != "const")
                continue;

            var valuesNode = GetValue(nodeMap, "values");
            if (valuesNode is not YamlSequenceNode valuesSeq)
                continue;

            int binCount = valuesSeq.Children.Count;
            var replacement = new YamlSequenceNode();
            replacement.Style = YamlDotNet.Core.Events.SequenceStyle.Flow;
            for (int i = 0; i < binCount; i++)
                replacement.Add(new YamlScalarNode(value.ToString(CultureInfo.InvariantCulture)));

            nodeMap.Children[new YamlScalarNode("values")] = replacement;
            break;
        }

        var writer = new StringWriter();
        stream.Save(writer, assignAnchors: false);
        return writer.ToString();
    }

    private static YamlNode? GetValue(YamlMappingNode map, string key)
    {
        foreach (var (k, v) in map.Children)
        {
            if (k is YamlScalarNode scalar && scalar.Value == key)
                return v;
        }
        return null;
    }
}
