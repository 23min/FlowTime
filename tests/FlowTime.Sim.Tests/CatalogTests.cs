using FlowTime.Sim.Core;
using Xunit;

namespace FlowTime.Sim.Tests;

public class CatalogTests
{
    [Fact]
    public void Catalog_ValidCatalog_PassesValidation()
    {
        // Arrange
        var catalog = new Catalog
        {
            Version = 1,
            Metadata = new CatalogMetadata
            {
                Id = "test-catalog",
                Title = "Test Catalog",
                Description = "A test catalog"
            },
            Components = new[]
            {
                new CatalogComponent { Id = "COMP_A", Label = "Component A" },
                new CatalogComponent { Id = "COMP_B", Label = "Component B" }
            },
            Connections = new[]
            {
                new CatalogConnection { From = "COMP_A", To = "COMP_B", Label = "Primary flow" }
            },
            Classes = new[] { "DEFAULT" },
            LayoutHints = new CatalogLayoutHints { RankDir = "LR", Spacing = 100 }
        };

        // Act
        var result = catalog.Validate();

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Catalog_DuplicateComponentIds_FailsValidation()
    {
        // Arrange
        var catalog = new Catalog
        {
            Version = 1,
            Metadata = new CatalogMetadata { Id = "test", Title = "Test" },
            Components = new[]
            {
                new CatalogComponent { Id = "COMP_A", Label = "Component A1" },
                new CatalogComponent { Id = "COMP_A", Label = "Component A2" }
            },
            Classes = new[] { "DEFAULT" }
        };

        // Act
        var result = catalog.Validate();

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Duplicate component ID: COMP_A", result.Errors);
    }

    [Fact]
    public void Catalog_InvalidConnection_FailsValidation()
    {
        // Arrange
        var catalog = new Catalog
        {
            Version = 1,
            Metadata = new CatalogMetadata { Id = "test", Title = "Test" },
            Components = new[]
            {
                new CatalogComponent { Id = "COMP_A", Label = "Component A" }
            },
            Connections = new[]
            {
                new CatalogConnection { From = "COMP_A", To = "UNKNOWN", Label = "Bad connection" }
            },
            Classes = new[] { "DEFAULT" }
        };

        // Act
        var result = catalog.Validate();

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Connection references unknown component: UNKNOWN", result.Errors);
    }

    [Fact]
    public void Catalog_InvalidComponentIdFormat_FailsValidation()
    {
        // Arrange
        var catalog = new Catalog
        {
            Version = 1,
            Metadata = new CatalogMetadata { Id = "test", Title = "Test" },
            Components = new[]
            {
                new CatalogComponent { Id = "COMP@INVALID", Label = "Invalid Component" }
            },
            Classes = new[] { "DEFAULT" }
        };

        // Act
        var result = catalog.Validate();

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Invalid component ID format: COMP@INVALID"));
    }

    [Theory]
    [InlineData("COMP_A")]
    [InlineData("Component-1")]
    [InlineData("node.service")]
    [InlineData("API123")]
    public void Catalog_ValidComponentIds_PassValidation(string componentId)
    {
        // Arrange
        var catalog = new Catalog
        {
            Version = 1,
            Metadata = new CatalogMetadata { Id = "test", Title = "Test" },
            Components = new[]
            {
                new CatalogComponent { Id = componentId, Label = "Test Component" }
            },
            Classes = new[] { "DEFAULT" }
        };

        // Act
        var result = catalog.Validate();

        // Assert
        Assert.True(result.IsValid, $"Component ID '{componentId}' should be valid");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("COMP@A")]
    [InlineData("COMP#A")]
    [InlineData("COMP/A")]
    public void Catalog_InvalidComponentIds_FailValidation(string componentId)
    {
        // Arrange
        var catalog = new Catalog
        {
            Version = 1,
            Metadata = new CatalogMetadata { Id = "test", Title = "Test" },
            Components = new[]
            {
                new CatalogComponent { Id = componentId, Label = "Test Component" }
            },
            Classes = new[] { "DEFAULT" }
        };

        // Act
        var result = catalog.Validate();

        // Assert
        Assert.False(result.IsValid, $"Component ID '{componentId}' should be invalid");
    }
}
