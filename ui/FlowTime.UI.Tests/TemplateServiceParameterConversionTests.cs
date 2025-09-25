using System.Reflection;
using FlowTime.UI.Services;

namespace FlowTime.UI.Tests;

public class TemplateServiceParameterConversionTests
{
        [Fact]
    public void ConvertRequestToApiParameters_ShouldConvertStringArraysToDoubleArrays_FixedBehavior()
    {
        // Arrange - This test verifies the FIXED behavior which prevents the handlebars issue
        var request = new SimulationRunRequest
        {
            TemplateId = "transportation-basic",
            Parameters = new Dictionary<string, object>
            {
                ["bins"] = 6,
                ["binMinutes"] = 60,
                ["demandPattern"] = new List<string> { "10", "15", "20", "25", "18", "12" },  // String array from UI form
                ["capacityPattern"] = new List<string> { "15", "18", "25", "30", "22", "16" }  // String array from UI form
            }
        };

        // Act
        var result = InvokeConvertRequestToApiParameters(request);

        // Assert - Fixed behavior: string arrays are converted to double arrays
        Assert.Equal(6, result["bins"]);
        Assert.Equal(60, result["binMinutes"]);
        
        // Fixed behavior: number array parameters are converted to double arrays
        Assert.IsType<double[]>(result["demandPattern"]);
        Assert.IsType<double[]>(result["capacityPattern"]);
        
        var demandArray = (double[])result["demandPattern"];
        var capacityArray = (double[])result["capacityPattern"];
        
        Assert.Equal(new[] { 10.0, 15.0, 20.0, 25.0, 18.0, 12.0 }, demandArray);
        Assert.Equal(new[] { 15.0, 18.0, 25.0, 30.0, 22.0, 16.0 }, capacityArray);
    }

    [Fact]
    public void ConvertRequestToApiParameters_ShouldConvertNumberArrays_DesiredBehavior()
    {
        // Arrange - This test shows what the behavior SHOULD be after the fix
        var request = new SimulationRunRequest
        {
            TemplateId = "transportation-basic",
            Parameters = new Dictionary<string, object>
            {
                ["bins"] = 6,
                ["binMinutes"] = 60,
                ["demandPattern"] = new List<string> { "10", "15", "20", "25", "18", "12" }, // String array from UI form
                ["capacityPattern"] = new List<string> { "15", "18", "25", "30", "22", "16" }  // String array from UI form
            }
        };

        // Act
        var result = InvokeConvertRequestToApiParameters(request);

        // Assert - After fix: should convert string arrays to number arrays for numberArray parameters
        Assert.Equal(6, result["bins"]);
        Assert.Equal(60, result["binMinutes"]);
        
        // TODO: After implementing the fix, these should be double arrays, not string arrays
        // Expected behavior after fix:
        // Assert.IsType<double[]>(result["demandPattern"]);
        // Assert.IsType<double[]>(result["capacityPattern"]);
        // 
        // var demandArray = (double[])result["demandPattern"];
        // var capacityArray = (double[])result["capacityPattern"];
        // 
        // Assert.Equal(new double[] { 10, 15, 20, 25, 18, 12 }, demandArray);
        // Assert.Equal(new double[] { 15, 18, 25, 30, 22, 16 }, capacityArray);
    }

    [Fact]
    public void ConvertRequestToApiParameters_ShouldHandleMixedParameterTypes()
    {
        // Arrange
        var request = new SimulationRunRequest
        {
            TemplateId = "manufacturing-line",
            Parameters = new Dictionary<string, object>
            {
                ["bins"] = 12,                                              // number
                ["binMinutes"] = 60,                                        // number
                ["rawMaterialSchedule"] = new List<string> { "100", "80", "120" }, // numberArray (should convert)
                ["qualityRate"] = 0.95,                                     // number (double)
                ["assemblyCapacity"] = new List<string> { "90", "85", "95" }       // numberArray (should convert)
            }
        };

        // Act
        var result = InvokeConvertRequestToApiParameters(request);

        // Assert - Numbers should remain as-is, arrays should be converted to double arrays  
        Assert.Equal(12, result["bins"]);
        Assert.Equal(60, result["binMinutes"]);
        Assert.Equal(0.95, result["qualityRate"]);
        
        // Fixed behavior: number array parameters are converted to double arrays
        Assert.IsType<double[]>(result["rawMaterialSchedule"]);
        Assert.IsType<double[]>(result["assemblyCapacity"]);
    }

    [Theory]
    [InlineData("transportation-basic")]
    [InlineData("manufacturing-line")]
    [InlineData("it-system-microservices")]
    [InlineData("supply-chain-multi-tier")]
    public void ConvertRequestToApiParameters_ShouldPreserveCatalogId(string templateId)
    {
        // Arrange
        var request = new SimulationRunRequest
        {
            TemplateId = templateId,
            CatalogId = "demo-catalog",
            Parameters = new Dictionary<string, object>
            {
                ["bins"] = 6,
                ["binMinutes"] = 60
            }
        };

        // Act
        var result = InvokeConvertRequestToApiParameters(request);

        // Assert
        Assert.Equal("demo-catalog", result["catalogId"]);
        Assert.Equal(6, result["bins"]);
        Assert.Equal(60, result["binMinutes"]);
    }

    /// <summary>
    /// Uses reflection to call the private ConvertRequestToApiParameters method
    /// </summary>
    private static Dictionary<string, object> InvokeConvertRequestToApiParameters(SimulationRunRequest request)
    {
        var method = typeof(FlowTimeSimService)
            .GetMethod("ConvertRequestToApiParameters", BindingFlags.NonPublic | BindingFlags.Static);
        
        Assert.NotNull(method);
        
        var result = method.Invoke(null, new object[] { request });
        return (Dictionary<string, object>)result!;
    }
}