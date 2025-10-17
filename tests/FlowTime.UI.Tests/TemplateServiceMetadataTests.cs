using System.Globalization;
using System.Reflection;
using System.Linq;
using System.IO;
using FlowTime.UI.Services;
using YamlDotNet.RepresentationModel;

namespace FlowTime.UI.Tests;

public class TemplateServiceMetadataTests
{
	[Theory]
	[InlineData("it-system-microservices", "IT System with Microservices - Generated Model", "Modern web application handling user requests through microservices", new[] { "microservices", "web-scale", "modern", "it-systems" })]
	[InlineData("transportation-basic", "This simulates passenger demand and vehicle capacity in a transit system", "Transportation network with demand patterns and capacity constraints", new[] { "transportation", "transit", "capacity", "beginner" })]
	[InlineData("manufacturing-line", "Manufacturing Production Line - Generated Model", "Production line with quality control and maintenance downtime", new[] { "manufacturing", "production", "bottleneck", "operations" })]
	public void GenerateSimulationYaml_ShouldIncludeMetadataForTemplate(string templateId, string expectedTitle, string expectedDescription, string[] expectedTags)
	{
		// Arrange
		var request = new SimulationRunRequest
		{
			TemplateId = templateId,
			Parameters = new()
		};

		// Act
		var yaml = InvokeGenerateSimulationYaml(request);

		// Assert
		var metadata = ReadMetadataSection(yaml);
		Assert.Equal(expectedTitle, metadata.title);
		Assert.Equal(expectedDescription, metadata.description);
		Assert.Equal(templateId, metadata.templateId);
		Assert.Equal(expectedTags, metadata.tags);
	}

	[Fact]
	public void TranslateToSimulationSchema_UsesFirstConstNodeForArrivals()
	{
		// Arrange: shrink simulation hours for deterministic values
		var parameters = new Dictionary<string, object>
		{
			["demandRate"] = 10.0,
			["capacity"] = 15.0,
			["simulationHours"] = 4
		};

		var request = new SimulationRunRequest
		{
			TemplateId = "transportation-basic",
			Parameters = parameters
		};

		var nodesYaml = InvokeGenerateSimulationYaml(request);

		// Act
		var translated = InvokeTranslateToSimulationSchema(nodesYaml, request);
		var (bins, binMinutes, values, routeId) = ReadSimulationSchema(translated);

		// Assert
		Assert.Equal(4, bins);
		Assert.Equal(60, binMinutes);
		Assert.Equal("passenger_demand", routeId);
		Assert.Equal(new[] { 3, 3, 3, 3 }, values);
	}

	private static (string title, string description, string templateId, string[] tags) ReadMetadataSection(string yaml)
	{
		var yamlStream = new YamlStream();
		yamlStream.Load(new StringReader(yaml));
		var root = (YamlMappingNode)yamlStream.Documents[0].RootNode;
		var metadataNode = (YamlMappingNode)root.Children[new YamlScalarNode("metadata")];

		var title = ((YamlScalarNode)metadataNode.Children[new YamlScalarNode("title")]).Value ?? string.Empty;
		var description = ((YamlScalarNode)metadataNode.Children[new YamlScalarNode("description")]).Value ?? string.Empty;
		var templateId = ((YamlScalarNode)metadataNode.Children[new YamlScalarNode("templateId")]).Value ?? string.Empty;

		var tagsNode = metadataNode.Children[new YamlScalarNode("tags")];
		var tags = tagsNode switch
		{
			YamlSequenceNode seq => seq.Children.OfType<YamlScalarNode>().Select(n => n.Value ?? string.Empty).ToArray(),
			YamlScalarNode scalar => scalar.Value?.Trim('[', ']')?.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToArray() ?? Array.Empty<string>(),
			_ => Array.Empty<string>()
		};

		return (title, description, templateId, tags);
	}

	private static (int bins, int binMinutes, int[] values, string routeId) ReadSimulationSchema(string yaml)
	{
		var yamlStream = new YamlStream();
		yamlStream.Load(new StringReader(yaml));
		var root = (YamlMappingNode)yamlStream.Documents[0].RootNode;

		var gridNode = (YamlMappingNode)root.Children[new YamlScalarNode("grid")];
		var bins = int.Parse(((YamlScalarNode)gridNode.Children[new YamlScalarNode("bins")]).Value!, CultureInfo.InvariantCulture);
		var binSize = int.Parse(((YamlScalarNode)gridNode.Children[new YamlScalarNode("binSize")]).Value!, CultureInfo.InvariantCulture);
		var binUnit = ((YamlScalarNode)gridNode.Children[new YamlScalarNode("binUnit")]).Value!;
		var binMinutes = binUnit.ToLowerInvariant() switch
		{
			"minutes" => binSize,
			"hours" => binSize * 60,
			"days" => binSize * 1440,
			_ => throw new ArgumentException($"Unknown time unit: {binUnit}")
		};

		var arrivalsNode = (YamlMappingNode)root.Children[new YamlScalarNode("arrivals")];
		var valuesNode = (YamlSequenceNode)arrivalsNode.Children[new YamlScalarNode("values")];
		var values = valuesNode.Children.OfType<YamlScalarNode>().Select(n => int.Parse(n.Value!, CultureInfo.InvariantCulture)).ToArray();

		var routeNode = (YamlMappingNode)root.Children[new YamlScalarNode("route")];
		var routeId = ((YamlScalarNode)routeNode.Children[new YamlScalarNode("id")]).Value ?? string.Empty;

		return (bins, binMinutes, values, routeId);
	}

	private static string InvokeGenerateSimulationYaml(SimulationRunRequest request)
	{
		var method = typeof(FlowTimeSimService)
			.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
			.FirstOrDefault(m => m.Name == "GenerateSimulationYaml" && m.GetParameters().Length == 1);
		Assert.NotNull(method);
		return (string)method!.Invoke(null, new object[] { request })!;
	}

	private static string InvokeTranslateToSimulationSchema(string nodesYaml, SimulationRunRequest request)
	{
		var method = typeof(FlowTimeSimService)
			.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
			.FirstOrDefault(m => m.Name == "TranslateToSimulationSchema" && m.GetParameters().Length == 2);
		Assert.NotNull(method);
		return (string)method!.Invoke(null, new object[] { nodesYaml, request })!;
	}
}
