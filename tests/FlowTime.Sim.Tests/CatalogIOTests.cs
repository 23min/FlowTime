using FlowTime.Sim.Core;
using Xunit;

namespace FlowTime.Sim.Tests;

public class CatalogIOTests
{
    [Fact]
    public void ReadCatalogFromYaml_ValidYaml_ReturnsCorrectCatalog()
    {
        // Arrange
        var yaml = """
            version: 1
            metadata:
              id: demo-system
              title: Demo Processing System
              description: Simple two-component system for testing
            components:
              - id: COMP_A
                label: Component A
                description: Entry point component
              - id: COMP_B
                label: Component B
                description: Processing component
            connections:
              - from: COMP_A
                to: COMP_B
                label: Primary flow
            classes:
              - DEFAULT
            layoutHints:
              rankDir: LR
              spacing: 100
            """;

        // Act
        var catalog = CatalogIO.ReadCatalogFromYaml(yaml);

        // Assert
        Assert.Equal(1, catalog.Version);
        Assert.Equal("demo-system", catalog.Metadata.Id);
        Assert.Equal("Demo Processing System", catalog.Metadata.Title);
        Assert.Equal("Simple two-component system for testing", catalog.Metadata.Description);
        
        Assert.Equal(2, catalog.Components.Count);
        Assert.Equal("COMP_A", catalog.Components[0].Id);
        Assert.Equal("Component A", catalog.Components[0].Label);
        Assert.Equal("Entry point component", catalog.Components[0].Description);
        
        Assert.Single(catalog.Connections);
        Assert.Equal("COMP_A", catalog.Connections[0].From);
        Assert.Equal("COMP_B", catalog.Connections[0].To);
        Assert.Equal("Primary flow", catalog.Connections[0].Label);
        
        Assert.Single(catalog.Classes);
        Assert.Equal("DEFAULT", catalog.Classes[0]);
        
        Assert.NotNull(catalog.LayoutHints);
        Assert.Equal("LR", catalog.LayoutHints.RankDir);
        Assert.Equal(100, catalog.LayoutHints.Spacing);
    }

    [Fact]
    public void WriteCatalogToYaml_ValidCatalog_GeneratesCorrectYaml()
    {
        // Arrange
        var catalog = new Catalog
        {
            Version = 1,
            Metadata = new CatalogMetadata
            {
                Id = "test-catalog",
                Title = "Test Catalog"
            },
            Components = new List<CatalogComponent>
            {
                new CatalogComponent { Id = "nodeA", Label = "Node A" }
            },
            Classes = new List<string> { "DEFAULT" },
            LayoutHints = new CatalogLayoutHints { RankDir = "LR" }
        };

        // Act
        var yaml = CatalogIO.WriteCatalogToYaml(catalog);

        // Assert
        Assert.Contains("version: 1", yaml);
        Assert.Contains("id: test-catalog", yaml);
        Assert.Contains("title: Test Catalog", yaml);
        Assert.Contains("id: nodeA", yaml);
        Assert.Contains("label: Node A", yaml);
        Assert.Contains("rankDir: LR", yaml);
    }

    [Fact]
    public void ReadCatalogFromYaml_InvalidYaml_ThrowsException()
    {
        // Arrange
        var invalidYaml = """
            version: 1
            metadata:
              id: test
              title: Test
            components:
              - id: COMP_A
                label: A
              - id: COMP_A  # Duplicate ID
                label: A2
            classes:
              - DEFAULT
            """;

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => 
            CatalogIO.ReadCatalogFromYaml(invalidYaml));
        Assert.Contains("Duplicate component ID: COMP_A", exception.Message);
    }

    [Fact]
    public void ComputeCatalogHash_SameCatalog_GeneratesSameHash()
    {
        // Arrange
        var catalog1 = new Catalog
        {
            Version = 1,
            Metadata = new CatalogMetadata { Id = "test", Title = "Test" },
            Components = new List<CatalogComponent> { new CatalogComponent { Id = "A", Label = "Component A" } },
            Classes = new List<string> { "DEFAULT" }
        };

        var catalog2 = new Catalog
        {
            Version = 1,
            Metadata = new CatalogMetadata { Id = "test", Title = "Test" },
            Components = new List<CatalogComponent> { new CatalogComponent { Id = "A", Label = "Component A" } },
            Classes = new List<string> { "DEFAULT" }
        };

        // Act
        var hash1 = CatalogIO.ComputeCatalogHash(catalog1);
        var hash2 = CatalogIO.ComputeCatalogHash(catalog2);

        // Assert
        Assert.Equal(hash1, hash2);
        Assert.StartsWith("sha256:", hash1);
    }

    [Fact]
    public void ComputeCatalogHash_DifferentCatalogs_GeneratesDifferentHashes()
    {
        // Arrange
        var catalog1 = new Catalog
        {
            Version = 1,
            Metadata = new CatalogMetadata { Id = "test1", Title = "Test 1" },
            Components = new List<CatalogComponent> { new CatalogComponent { Id = "A", Label = "Component A" } },
            Classes = new List<string> { "DEFAULT" }
        };

        var catalog2 = new Catalog
        {
            Version = 1,
            Metadata = new CatalogMetadata { Id = "test2", Title = "Test 2" },
            Components = new List<CatalogComponent> { new CatalogComponent { Id = "A", Label = "Component A" } },
            Classes = new List<string> { "DEFAULT" }
        };

        // Act
        var hash1 = CatalogIO.ComputeCatalogHash(catalog1);
        var hash2 = CatalogIO.ComputeCatalogHash(catalog2);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void RoundTrip_CatalogToYamlAndBack_PreservesData()
    {
        // Arrange
        var originalCatalog = new Catalog
        {
            Version = 1,
            Metadata = new CatalogMetadata
            {
                Id = "round-trip-test",
                Title = "Round Trip Test",
                Description = "Testing YAML round-trip serialization"
            },
            Components = new List<CatalogComponent>
            {
                new CatalogComponent { Id = "COMP_A", Label = "Component A", Description = "First component" },
                new CatalogComponent { Id = "COMP_B", Label = "Component B" }
            },
            Connections = new List<CatalogConnection>
            {
                new CatalogConnection { From = "COMP_A", To = "COMP_B" }
            },
            Classes = new List<string> { "DEFAULT", "PRIORITY" },
            LayoutHints = new CatalogLayoutHints { RankDir = "TB", Spacing = 50 }
        };

        // Act
        var yaml = CatalogIO.WriteCatalogToYaml(originalCatalog);
        var roundTripCatalog = CatalogIO.ReadCatalogFromYaml(yaml);

        // Assert
        Assert.Equal(originalCatalog.Version, roundTripCatalog.Version);
        Assert.Equal(originalCatalog.Metadata.Id, roundTripCatalog.Metadata.Id);
        Assert.Equal(originalCatalog.Metadata.Title, roundTripCatalog.Metadata.Title);
        Assert.Equal(originalCatalog.Metadata.Description, roundTripCatalog.Metadata.Description);
        Assert.Equal(originalCatalog.Components.Count, roundTripCatalog.Components.Count);
        Assert.Equal(originalCatalog.Connections.Count, roundTripCatalog.Connections.Count);
        Assert.Equal(originalCatalog.Classes.Count, roundTripCatalog.Classes.Count);
        Assert.Equal(originalCatalog.LayoutHints?.RankDir, roundTripCatalog.LayoutHints?.RankDir);
        Assert.Equal(originalCatalog.LayoutHints?.Spacing, roundTripCatalog.LayoutHints?.Spacing);
    }
}
