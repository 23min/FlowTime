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

	private static string InvokeGenerateSimulationYaml(SimulationRunRequest request)
	{
		var method = typeof(FlowTimeSimService)
			.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
			.FirstOrDefault(m => m.Name == "GenerateSimulationYaml" && m.GetParameters().Length == 1);
		Assert.NotNull(method);
		return (string)method!.Invoke(null, new object[] { request })!;
	}
}
