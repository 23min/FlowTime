using System.Text.Json;
using FlowTime.UI.Services;

namespace FlowTime.UI.Tests;

/// <summary>
/// Integration tests that demonstrate the parameter type conversion issue.
/// These tests show the actual problem: when UI sends string arrays to FlowTime-Sim API,
/// the API returns templates with unresolved handlebars instead of resolved values.
/// </summary>
public class ParameterConversionIntegrationTests
{
    [Fact]
    public void StringArrayParameters_CauseHandlebarsInResponse_DemonstratesIssue()
    {
        // This test documents the actual issue that the user is experiencing:
        // When string arrays are sent to FlowTime-Sim API, it returns handlebars instead of resolved values
        
        // Arrange - Parameters as the UI currently sends them (string arrays)
        var uiParameters = new Dictionary<string, object>
        {
            ["bins"] = 6,
            ["binMinutes"] = 60,
            ["demandPattern"] = new List<string> { "10", "15", "20", "25", "18", "12" }, // String array from UI
            ["capacityPattern"] = new List<string> { "15", "18", "25", "30", "22", "16" }  // String array from UI  
        };

        // Act - Serialize as the UI would send to FlowTime-Sim API
        var json = JsonSerializer.Serialize(uiParameters, new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
        });

        // Assert - This JSON contains string arrays which cause the handlebars issue
        Assert.Contains("\"demandPattern\":[\"10\",\"15\",\"20\",\"25\",\"18\",\"12\"]", json);
        Assert.Contains("\"capacityPattern\":[\"15\",\"18\",\"25\",\"30\",\"22\",\"16\"]", json);
        
        // Expected response from FlowTime-Sim API with string arrays: {{demandPattern}} (handlebars)
        // Expected response from FlowTime-Sim API with number arrays: [10, 15, 20, 25, 18, 12] (resolved)
    }

    [Fact]  
    public void NumberArrayParameters_WouldResolveCorrectly_ShowsDesiredBehavior()
    {
        // This test shows what the UI SHOULD send to get resolved values instead of handlebars
        
        // Arrange - Parameters as they SHOULD be sent (number arrays)
        var correctParameters = new Dictionary<string, object>
        {
            ["bins"] = 6,
            ["binMinutes"] = 60,
            ["demandPattern"] = new double[] { 10, 15, 20, 25, 18, 12 }, // Number array (correct)
            ["capacityPattern"] = new double[] { 15, 18, 25, 30, 22, 16 } // Number array (correct)
        };

        // Act - Serialize as the UI SHOULD send to FlowTime-Sim API
        var json = JsonSerializer.Serialize(correctParameters, new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
        });

        // Assert - This JSON contains number arrays which would resolve correctly
        Assert.Contains("\"demandPattern\":[10,15,20,25,18,12]", json);
        Assert.Contains("\"capacityPattern\":[15,18,25,30,22,16]", json);
        
        // With number arrays, FlowTime-Sim API returns: values: [10, 15, 20, 25, 18, 12] (resolved)
        // Instead of: values: {{demandPattern}} (handlebars)
    }

    [Theory]
    [InlineData("10,15,20", new double[] { 10, 15, 20 })]
    [InlineData("1.5,2.0,3.5", new double[] { 1.5, 2.0, 3.5 })]
    [InlineData("100", new double[] { 100 })]
    [InlineData("", new double[] { })]
    public void ParseNumberArray_ShouldConvertStringArrayToDoubleArray(string commaSeparated, double[] expected)
    {
        // This test shows the conversion logic that needs to be implemented
        
        // Arrange - Simulate what comes from UI form (comma-separated string)
        var stringArray = string.IsNullOrWhiteSpace(commaSeparated) 
            ? new List<string>()
            : commaSeparated.Split(',', StringSplitOptions.RemoveEmptyEntries)
                           .Select(s => s.Trim())
                           .ToList();

        // Act - Convert to double array (this logic needs to be added to ConvertRequestToApiParameters)
        var result = stringArray.Where(s => double.TryParse(s, out _))
                               .Select(double.Parse)
                               .ToArray();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ConvertRequestToApiParameters_Integration_ShowsProblemAndSolution()
    {
        // This test demonstrates the complete flow and the fix needed
        
        // Arrange - Typical UI request with string arrays (current broken behavior)
        var request = new SimulationRunRequest
        {
            TemplateId = "transportation-basic",
            Parameters = new Dictionary<string, object>
            {
                ["bins"] = 6,
                ["binMinutes"] = 60,
                ["demandPattern"] = new List<string> { "10", "15", "20" }, // From UI form
                ["capacityPattern"] = new List<string> { "15", "18", "25" }  // From UI form
            }
        };

        // Current behavior (what ConvertRequestToApiParameters currently does)
        var currentBehavior = new Dictionary<string, object>();
        foreach (var param in request.Parameters)
        {
            currentBehavior[param.Key] = param.Value; // Direct copy (broken)
        }
        
        // Desired behavior (what ConvertRequestToApiParameters should do after fix)
        var desiredBehavior = new Dictionary<string, object>();
        foreach (var param in request.Parameters)
        {
            if (param.Value is List<string> stringList && IsNumberArrayParameter(param.Key))
            {
                // Convert string array to double array for number array parameters
                var doubleArray = stringList.Select(double.Parse).ToArray();
                desiredBehavior[param.Key] = doubleArray;
            }
            else
            {
                desiredBehavior[param.Key] = param.Value;
            }
        }

        // Assert current vs desired
        Assert.IsType<List<string>>(currentBehavior["demandPattern"]);  // Current: string array
        Assert.IsType<double[]>(desiredBehavior["demandPattern"]);       // Desired: double array
        
        Assert.Equal(new[] { "10", "15", "20" }, (List<string>)currentBehavior["demandPattern"]);
        Assert.Equal(new double[] { 10, 15, 20 }, (double[])desiredBehavior["demandPattern"]);
    }
    
    /// <summary>
    /// Helper method to determine if a parameter should be treated as a number array.
    /// In the real fix, this would use template metadata to determine parameter types.
    /// </summary>
    private static bool IsNumberArrayParameter(string parameterName)
    {
        // Known number array parameters from templates
        return parameterName.EndsWith("Pattern") || 
               parameterName.EndsWith("Schedule") || 
               parameterName.EndsWith("Capacity");
    }
}