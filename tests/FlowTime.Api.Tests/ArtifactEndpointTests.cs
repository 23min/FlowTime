using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using System.Net;
using FlowTime.Contracts.Services;

namespace FlowTime.Api.Tests;

public class ArtifactEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory factory;
    private readonly HttpClient client;

    public ArtifactEndpointTests(TestWebApplicationFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task GET_Runs_NonExistentRun_Returns_NotFound()
    {
        // Act
        var response = await client.GetAsync("/v1/runs/nonexistent/index");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GET_Runs_ExistingRun_Returns_Index()
    {
        // Arrange: We need to generate some artifacts first
        // Run the CLI to generate an output
        var tempDir = Path.GetTempPath();
        var runDir = Path.Combine(tempDir, "test_artifacts");
        Directory.CreateDirectory(runDir);

        // Create a minimal test run directory structure
        var runId = "test_run_123";
        var runPath = Path.Combine(runDir, runId);
        Directory.CreateDirectory(runPath);
        Directory.CreateDirectory(Path.Combine(runPath, "series"));

        // Create index.json
        var indexContent = @"{
  ""schemaVersion"": 1,
  ""grid"": {
    ""bins"": 3,
    ""binSize"": 1,
    ""binUnit"": ""hours"",
    ""timezone"": ""UTC""
  },
  ""series"": [
    {
      ""id"": ""test@TEST@DEFAULT"",
      ""kind"": ""flow"",
      ""path"": ""series/test@TEST@DEFAULT.csv"",
      ""unit"": ""entities/bin"",
      ""componentId"": ""TEST"",
      ""class"": ""DEFAULT"",
      ""points"": 3,
      ""hash"": ""sha256:test""
    }
  ]
}";
        await File.WriteAllTextAsync(Path.Combine(runPath, "series", "index.json"), indexContent);

        // Create series CSV
        var csvContent = "t,value\n0,10\n1,20\n2,30\n";
        await File.WriteAllTextAsync(Path.Combine(runPath, "series", "test@TEST@DEFAULT.csv"), csvContent);

        // Configure the factory to use our temp directory
        var clientWithConfig = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ArtifactsDirectory", runDir);
        }).CreateClient();

        // Act
        var response = await clientWithConfig.GetAsync($"/v1/runs/{runId}/index");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("schemaVersion", content);
        Assert.Contains("test@TEST@DEFAULT", content);

        // Cleanup
        Directory.Delete(runDir, true);
    }

    [Fact]
    public async Task GET_Runs_ExistingSeries_Returns_CSV()
    {
        // Arrange: Create test artifacts
        var tempDir = Path.GetTempPath();
        var runDir = Path.Combine(tempDir, "test_artifacts_series");
        Directory.CreateDirectory(runDir);

        var runId = "test_run_series_123";
        var runPath = Path.Combine(runDir, runId);
        Directory.CreateDirectory(runPath);
        Directory.CreateDirectory(Path.Combine(runPath, "series"));

        // Create series CSV
        var csvContent = "t,value\n0,10\n1,20\n2,30\n";
        await File.WriteAllTextAsync(Path.Combine(runPath, "series", "test@TEST@DEFAULT.csv"), csvContent);

        // Configure the factory to use our temp directory
        var clientWithConfig = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ArtifactsDirectory", runDir);
        }).CreateClient();

        // Act
        var response = await clientWithConfig.GetAsync($"/v1/runs/{runId}/series/test@TEST@DEFAULT");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.ToString());
        var content = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("t,value", content);
        Assert.Contains("0,10", content);
        Assert.Contains("1,20", content);
        Assert.Contains("2,30", content);

        // Cleanup
        Directory.Delete(runDir, true);
    }

    [Fact]
    public async Task GET_Runs_NonExistentSeries_Returns_NotFound()
    {
        // Arrange: Create empty run directory
        var tempDir = Path.GetTempPath();
        var runDir = Path.Combine(tempDir, "test_artifacts_empty");
        Directory.CreateDirectory(runDir);

        var runId = "test_run_empty_123";
        var runPath = Path.Combine(runDir, runId);
        Directory.CreateDirectory(runPath);
        Directory.CreateDirectory(Path.Combine(runPath, "series"));

        // Configure the factory to use our temp directory
        var clientWithConfig = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ArtifactsDirectory", runDir);
        }).CreateClient();

        // Act
        var response = await clientWithConfig.GetAsync($"/v1/runs/{runId}/series/nonexistent@SERIES@DEFAULT");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // Cleanup
        Directory.Delete(runDir, true);
    }

    [Fact]
    public async Task GET_Series_MissingIndexJson_Returns404NotFound()
    {
        // Arrange: Create run directory with series folder but NO index.json
        var tempDir = Path.GetTempPath();
        var runDir = Path.Combine(tempDir, "test_artifacts_no_index");
        Directory.CreateDirectory(runDir);

        var runId = "test_run_no_index_456";
        var runPath = Path.Combine(runDir, runId);
        Directory.CreateDirectory(runPath);
        Directory.CreateDirectory(Path.Combine(runPath, "series"));
        // NOTE: Intentionally NOT creating series/index.json

        // Configure the factory to use our temp directory
        var clientWithConfig = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ArtifactsDirectory", runDir);
        }).CreateClient();

        // Act - Request a series when index.json doesn't exist
        var response = await clientWithConfig.GetAsync($"/v1/runs/{runId}/series/demand@DEMAND@DEFAULT");

        // Assert - Should return 404, not 500 (this tests the FileNotFoundException fix)
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("not found", content.ToLower());

        // Cleanup
        Directory.Delete(runDir, true);
    }

    [Fact]
    public async Task GET_Series_PartialSeriesName_FindsMatchingSeries()
    {
        // Arrange: Create run with proper artifacts
        var tempDir = Path.GetTempPath();
        var runDir = Path.Combine(tempDir, "test_artifacts_partial");
        Directory.CreateDirectory(runDir);

        var runId = "test_run_partial_789";
        var runPath = Path.Combine(runDir, runId);
        Directory.CreateDirectory(runPath);
        Directory.CreateDirectory(Path.Combine(runPath, "series"));

        // Create index.json with a series
        var indexContent = @"{
  ""schemaVersion"": 1,
  ""grid"": {
    ""bins"": 2,
    ""binSize"": 30,
    ""binUnit"": ""minutes"",
    ""timezone"": ""UTC""
  },
  ""series"": [
    {
      ""id"": ""demand@DEMAND@DEFAULT"",
      ""kind"": ""flow"",
      ""path"": ""series/demand@DEMAND@DEFAULT.csv"",
      ""unit"": ""entities/bin"",
      ""componentId"": ""DEMAND"",
      ""class"": ""DEFAULT"",
      ""points"": 2,
      ""hash"": ""sha256:abcd1234""
    }
  ]
}";
        await File.WriteAllTextAsync(Path.Combine(runPath, "series", "index.json"), indexContent);

        // Create the actual CSV file
        var csvContent = "t,value\n0,100\n1,200\n";
        await File.WriteAllTextAsync(Path.Combine(runPath, "series", "demand@DEMAND@DEFAULT.csv"), csvContent);

        // Configure the factory to use our temp directory
        var clientWithConfig = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ArtifactsDirectory", runDir);
        }).CreateClient();

        // Act - Request series using partial name (should find "demand@DEMAND@DEFAULT")
        var response = await clientWithConfig.GetAsync($"/v1/runs/{runId}/series/demand");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("t,value", content);
        Assert.Contains("0,100", content);
        Assert.Contains("1,200", content);

        // Cleanup
        Directory.Delete(runDir, true);
    }

    [Fact]
    public async Task POST_Export_Creates_All_Formats_And_Returns_Success()
    {
        // Arrange: Create a test run with data
        var tempDir = Path.GetTempPath();
        var runDir = Path.Combine(tempDir, "test_export_artifacts");
        Directory.CreateDirectory(runDir);

        var runId = "test_run_export_123";
        var runPath = Path.Combine(runDir, runId);
        Directory.CreateDirectory(runPath);
        Directory.CreateDirectory(Path.Combine(runPath, "series"));

        // Create series/index.json (this is what the FileSeriesReader expects)
        var seriesIndexContent = @"{
  ""schemaVersion"": 1,
  ""grid"": {
    ""bins"": 3,
    ""binSize"": 1,
    ""binUnit"": ""hours"",
    ""timezone"": ""UTC""
  },
  ""series"": [
    {
      ""id"": ""demand@DEMAND@DEFAULT"",
      ""kind"": ""flow"",
      ""path"": ""demand@DEMAND@DEFAULT.csv"",
      ""unit"": ""entities/bin"",
      ""componentId"": ""DEMAND"",
      ""class"": ""DEFAULT"",
      ""points"": 3,
      ""hash"": ""sha256:abcd1234""
    }
  ]
}";
        await File.WriteAllTextAsync(Path.Combine(runPath, "series", "index.json"), seriesIndexContent);

        // Create series data file
        var csvContent = "t,value\n0,100\n1,150\n2,200\n";
        await File.WriteAllTextAsync(Path.Combine(runPath, "series", "demand@DEMAND@DEFAULT.csv"), csvContent);

        // Configure the factory to use our temp directory
        var clientWithConfig = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ArtifactsDirectory", runDir);
        }).CreateClient();

        // Act - POST to create export artifacts
        var response = await clientWithConfig.PostAsync($"/v1/runs/{runId}/export", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var jsonResponse = await response.Content.ReadFromJsonAsync<dynamic>();
        Assert.NotNull(jsonResponse);

        // Verify export files were created
        var aggregatesDir = Path.Combine(runPath, "aggregates");
        Assert.True(Directory.Exists(aggregatesDir));
        Assert.True(File.Exists(Path.Combine(aggregatesDir, "export.csv")));
        Assert.True(File.Exists(Path.Combine(aggregatesDir, "export.ndjson")));
        Assert.True(File.Exists(Path.Combine(aggregatesDir, "export.parquet")));

        // Cleanup
        Directory.Delete(runDir, true);
    }

    [Fact]
    public async Task GET_Export_CSV_Returns_Correct_Format()
    {
        // Arrange: Create a test run with exported data
        var tempDir = Path.GetTempPath();
        var runDir = Path.Combine(tempDir, "test_csv_export");
        Directory.CreateDirectory(runDir);

        var runId = "test_run_csv_456";
        var runPath = Path.Combine(runDir, runId);
        var aggregatesDir = Path.Combine(runPath, "aggregates");
        Directory.CreateDirectory(aggregatesDir);

        // Create export.csv file directly (simulating POST export already happened)
        var csvContent = "time_bin,component_id,measure,value\n0,DEMAND,flow,100\n1,DEMAND,flow,150\n2,DEMAND,flow,200\n";
        await File.WriteAllTextAsync(Path.Combine(aggregatesDir, "export.csv"), csvContent);

        // Configure the factory
        var clientWithConfig = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ArtifactsDirectory", runDir);
        }).CreateClient();

        // Act - GET CSV export
        var response = await clientWithConfig.GetAsync($"/v1/runs/{runId}/export/csv");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("time_bin,component_id,measure,value", content);
        Assert.Contains("0,DEMAND,flow,100", content);
        Assert.Contains("1,DEMAND,flow,150", content);
        Assert.Contains("2,DEMAND,flow,200", content);

        // Cleanup
        Directory.Delete(runDir, true);
    }

    [Fact]
    public async Task GET_Export_NDJSON_Returns_Correct_Format()
    {
        // Arrange: Create a test run with exported data
        var tempDir = Path.GetTempPath();
        var runDir = Path.Combine(tempDir, "test_ndjson_export");
        Directory.CreateDirectory(runDir);

        var runId = "test_run_ndjson_789";
        var runPath = Path.Combine(runDir, runId);
        var aggregatesDir = Path.Combine(runPath, "aggregates");
        Directory.CreateDirectory(aggregatesDir);

        // Create export.ndjson file directly
        var ndjsonContent = @"{""time_bin"":0,""component_id"":""DEMAND"",""measure"":""flow"",""value"":100}
{""time_bin"":1,""component_id"":""DEMAND"",""measure"":""flow"",""value"":150}
{""time_bin"":2,""component_id"":""DEMAND"",""measure"":""flow"",""value"":200}
";
        await File.WriteAllTextAsync(Path.Combine(aggregatesDir, "export.ndjson"), ndjsonContent);

        // Configure the factory
        var clientWithConfig = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ArtifactsDirectory", runDir);
        }).CreateClient();

        // Act - GET NDJSON export
        var response = await clientWithConfig.GetAsync($"/v1/runs/{runId}/export/ndjson");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/x-ndjson", response.Content.Headers.ContentType?.MediaType);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(@"""time_bin"":0", content);
        Assert.Contains(@"""component_id"":""DEMAND""", content);
        Assert.Contains(@"""measure"":""flow""", content);
        Assert.Contains(@"""value"":100", content);

        // Cleanup
        Directory.Delete(runDir, true);
    }

    [Fact]
    public async Task GET_Export_Parquet_Returns_Binary_Data()
    {
        // Arrange: Create a test run with exported data
        var tempDir = Path.GetTempPath();
        var runDir = Path.Combine(tempDir, "test_parquet_export");
        Directory.CreateDirectory(runDir);

        var runId = "test_run_parquet_101";
        var runPath = Path.Combine(runDir, runId);
        var aggregatesDir = Path.Combine(runPath, "aggregates");
        Directory.CreateDirectory(aggregatesDir);

        // Create a minimal parquet file (we'll create a dummy binary file for this test)
        var parquetBytes = new byte[] { 0x50, 0x41, 0x52, 0x31 }; // "PAR1" magic bytes
        await File.WriteAllBytesAsync(Path.Combine(aggregatesDir, "export.parquet"), parquetBytes);

        // Configure the factory
        var clientWithConfig = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ArtifactsDirectory", runDir);
        }).CreateClient();

        // Act - GET Parquet export
        var response = await clientWithConfig.GetAsync($"/v1/runs/{runId}/export/parquet");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType?.MediaType);
        
        var content = await response.Content.ReadAsByteArrayAsync();
        Assert.True(content.Length > 0);
        Assert.Equal(0x50, content[0]); // Verify first byte is 'P'

        // Cleanup
        Directory.Delete(runDir, true);
    }

    [Fact]
    public async Task POST_Export_NonexistentRun_Returns_NotFound()
    {
        // Arrange: Use empty temp directory (no runs)
        var tempDir = Path.GetTempPath();
        var runDir = Path.Combine(tempDir, "test_empty_export");
        Directory.CreateDirectory(runDir);

        var clientWithConfig = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ArtifactsDirectory", runDir);
        }).CreateClient();

        // Act
        var response = await clientWithConfig.PostAsync("/v1/runs/nonexistent_run/export", null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // Cleanup
        Directory.Delete(runDir, true);
    }

    [Fact]
    public async Task GET_Export_InvalidFormat_Returns_BadRequest()
    {
        // Arrange: Create minimal run setup
        var tempDir = Path.GetTempPath();
        var runDir = Path.Combine(tempDir, "test_invalid_format");
        Directory.CreateDirectory(runDir);

        var runId = "test_run_invalid_format";
        var runPath = Path.Combine(runDir, runId);
        var aggregatesDir = Path.Combine(runPath, "aggregates");
        Directory.CreateDirectory(aggregatesDir);

        var clientWithConfig = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ArtifactsDirectory", runDir);
        }).CreateClient();

        // Act - Request invalid format
        var response = await clientWithConfig.GetAsync($"/v1/runs/{runId}/export/invalidformat");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Cleanup
        Directory.Delete(runDir, true);
    }

    [Fact]
    public async Task GET_Export_MissingExportFile_Returns_NotFound()
    {
        // Arrange: Create run directory but no export files
        var tempDir = Path.GetTempPath();
        var runDir = Path.Combine(tempDir, "test_missing_export");
        Directory.CreateDirectory(runDir);

        var runId = "test_run_no_exports";
        var runPath = Path.Combine(runDir, runId);
        Directory.CreateDirectory(runPath);
        // Note: Not creating gold directory or export files

        var clientWithConfig = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ArtifactsDirectory", runDir);
        }).CreateClient();

        // Act - Request CSV when no exports exist
        var response = await clientWithConfig.GetAsync($"/v1/runs/{runId}/export/csv");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // Cleanup
        Directory.Delete(runDir, true);
    }

    [Fact]
    public async Task POST_ArtifactsArchive_MarksArtifactsAndHidesFromDefaultQueries()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"test_artifacts_archive_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var runSuffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var runId = $"run_{DateTime.UtcNow:yyyyMMddTHHmmssZ}_{runSuffix}";
            var runDir = Path.Combine(tempRoot, runId);
            Directory.CreateDirectory(runDir);

            await File.WriteAllTextAsync(Path.Combine(runDir, "manifest.json"), "{ \"schemaVersion\": 1, \"title\": \"Archive Candidate\" }");
            await File.WriteAllTextAsync(Path.Combine(runDir, "spec.yaml"), "schemaVersion: 1\nmetadata:\n  tags: [sample]\n");

            using var clientWithConfig = factory.WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ArtifactsDirectory", tempRoot);
            }).CreateClient();

            await clientWithConfig.PostAsync("/v1/artifacts/index", null);

            var beforeDefault = await GetArtifactsAsync(clientWithConfig, "/v1/artifacts");
            Assert.Single(beforeDefault.Artifacts);

            var archiveResponse = await clientWithConfig.PostAsJsonAsync("/v1/artifacts/archive", new[] { runId });
            archiveResponse.EnsureSuccessStatusCode();

            var afterDefault = await GetArtifactsAsync(clientWithConfig, "/v1/artifacts");
            Assert.Empty(afterDefault.Artifacts);

            var afterInclude = await GetArtifactsAsync(clientWithConfig, "/v1/artifacts?includeArchived=true");
            Assert.Single(afterInclude.Artifacts);
            Assert.Contains("archived", afterInclude.Artifacts[0].Tags);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    [Fact]
    public async Task POST_ArtifactsBulkDelete_RemovesArtifactsAndDirectories()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"test_artifacts_delete_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var runIds = Enumerable.Range(0, 2)
                .Select(offset =>
                {
                    var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
                    return $"run_{DateTime.UtcNow.AddSeconds(offset):yyyyMMddTHHmmssZ}_{suffix}";
                })
                .ToArray();

            foreach (var runId in runIds)
            {
                var runDir = Path.Combine(tempRoot, runId);
                Directory.CreateDirectory(runDir);
                await File.WriteAllTextAsync(Path.Combine(runDir, "manifest.json"), "{ \"schemaVersion\": 1 }");
                await File.WriteAllTextAsync(Path.Combine(runDir, "spec.yaml"), "schemaVersion: 1\n");
            }

            using var clientWithConfig = factory.WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ArtifactsDirectory", tempRoot);
            }).CreateClient();

            await clientWithConfig.PostAsync("/v1/artifacts/index", null);

            var beforeDelete = await GetArtifactsAsync(clientWithConfig, "/v1/artifacts?includeArchived=true");
            Assert.Equal(2, beforeDelete.Artifacts.Count);

            var deleteResponse = await clientWithConfig.PostAsJsonAsync("/v1/artifacts/bulk-delete", runIds);
            deleteResponse.EnsureSuccessStatusCode();

            foreach (var runId in runIds)
            {
                Assert.False(Directory.Exists(Path.Combine(tempRoot, runId)));
            }

            var afterDelete = await GetArtifactsAsync(clientWithConfig, "/v1/artifacts?includeArchived=true");
            Assert.Empty(afterDelete.Artifacts);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    private static async Task<ArtifactListResponse> GetArtifactsAsync(HttpClient client, string route)
    {
        var response = await client.GetAsync(route);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ArtifactListResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new ArtifactListResponse();
    }
}
