using System.Globalization;
using YamlDotNet.RepresentationModel;

namespace FlowTime.TimeMachine.Sweep;

/// <summary>
/// Reads the current scalar value of a named const node from a model YAML document.
/// Companion to <see cref="ConstNodePatcher"/> — reads before patching.
/// </summary>
public static class ConstNodeReader
{
    /// <summary>
    /// Returns the first-bin value of the named const node, or <c>null</c> if the
    /// node is not found, is not a <c>const</c> node, or has no <c>values</c> entry.
    /// </summary>
    public static double? ReadValue(string modelYaml, string nodeId)
    {
        var stream = new YamlStream();
        using var reader = new StringReader(modelYaml);
        stream.Load(reader);

        if (stream.Documents.Count == 0)
            return null;

        var root = stream.Documents[0].RootNode as YamlMappingNode;
        if (root is null)
            return null;

        var nodesValue = GetValue(root, "nodes");
        if (nodesValue is not YamlSequenceNode nodesSeq)
            return null;

        foreach (var entry in nodesSeq)
        {
            if (entry is not YamlMappingNode nodeMap)
                continue;

            var idNode = GetValue(nodeMap, "id") as YamlScalarNode;
            var kindNode = GetValue(nodeMap, "kind") as YamlScalarNode;

            if (idNode?.Value != nodeId || kindNode?.Value != "const")
                continue;

            var valuesNode = GetValue(nodeMap, "values");
            if (valuesNode is not YamlSequenceNode valuesSeq || valuesSeq.Children.Count == 0)
                return null;

            var firstBin = valuesSeq.Children[0] as YamlScalarNode;
            if (firstBin?.Value is null)
                return null;

            if (double.TryParse(firstBin.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                return value;

            return null;
        }

        return null;
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
