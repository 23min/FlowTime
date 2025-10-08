using System;
using System.IO;
using FlowTime.Core.Models;
using FlowTime.Core.DataSources;
using Xunit;

namespace FlowTime.Core.Tests.DataSources;

public class SemanticLoaderTests
{
    [Fact]
    public void LoadNodeData_LoadsRequiredSemanticsFromCsv()
    {
        using var temp = TempDirectory.Create();
        temp.Write("arrivals.csv", @"bin_index,value
0,10
1,20
");
        temp.Write("served.csv", @"bin_index,value
0,8
1,18
");
        temp.Write("errors.csv", @"bin_index,value
0,2
1,2
");

        var node = new Node
        {
            Id = "OrderService",
            Semantics = new NodeSemantics
            {
                Arrivals = "file:arrivals.csv",
                Served = "file:served.csv",
                Errors = "file:errors.csv"
            }
        };

        var loader = new SemanticLoader(temp.Path);
        var data = loader.LoadNodeData(node, 2);

        Assert.Equal("OrderService", data.NodeId);
        Assert.Equal(new[] { 10d, 20d }, data.Arrivals);
        Assert.Equal(new[] { 8d, 18d }, data.Served);
        Assert.Equal(new[] { 2d, 2d }, data.Errors);
        Assert.Null(data.ExternalDemand);
        Assert.Null(data.QueueDepth);
    }

    [Fact]
    public void LoadNodeData_LoadsOptionalSemanticsWhenPresent()
    {
        using var temp = TempDirectory.Create();
        temp.Write("arrivals.csv", @"bin_index,value
0,10
1,20
");
        temp.Write("served.csv", @"bin_index,value
0,8
1,18
");
        temp.Write("errors.csv", @"bin_index,value
0,1
1,2
");
        temp.Write("external.csv", @"bin_index,value
0,12
1,22
");
        temp.Write("queue.csv", @"bin_index,value
0,5
1,7
");

        var node = new Node
        {
            Id = "OrderQueue",
            Semantics = new NodeSemantics
            {
                Arrivals = "file:arrivals.csv",
                Served = "file:served.csv",
                Errors = "file:errors.csv",
                ExternalDemand = "file:external.csv",
                QueueDepth = "file:queue.csv"
            }
        };

        var loader = new SemanticLoader(temp.Path);
        var data = loader.LoadNodeData(node, 2);

        Assert.Equal(new[] { 12d, 22d }, data.ExternalDemand);
        Assert.Equal(new[] { 5d, 7d }, data.QueueDepth);
    }

    [Fact]
    public void LoadNodeData_WhenCsvMissing_Throws()
    {
        using var temp = TempDirectory.Create();
        temp.Write("arrivals.csv", @"bin_index,value
0,10
1,20
");

        var node = new Node
        {
            Id = "OrderService",
            Semantics = new NodeSemantics
            {
                Arrivals = "file:arrivals.csv",
                Served = "file:missing.csv",
                Errors = "file:errors.csv"
            }
        };

        var loader = new SemanticLoader(temp.Path);

        Assert.Throws<FileNotFoundException>(() => loader.LoadNodeData(node, 2));
    }

    private sealed class TempDirectory : IDisposable
    {
        private TempDirectory(string path) => Path = path;

        public string Path { get; }

        public static TempDirectory Create()
        {
            var root = Directory.CreateTempSubdirectory();
            return new TempDirectory(root.FullName);
        }

        public void Write(string relativePath, string contents)
        {
            var filePath = System.IO.Path.Combine(Path, relativePath);
            File.WriteAllText(filePath, contents);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // ignore cleanup failures
            }
        }
    }
}
