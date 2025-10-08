using System;
using System.IO;
using FlowTime.Core.DataSources;
using Xunit;

namespace FlowTime.Core.Tests.DataSources;

public class CsvReaderTests
{
    [Fact]
    public void ReadTimeSeries_WithCompleteCsv_ReturnsExpectedValues()
    {
        var csv = @"bin_index,value
0,100
1,150
2,200
";
        using var tempFile = TempFile.Create(csv);

        var result = CsvReader.ReadTimeSeries(tempFile.Path, expectedBins: 3);

        Assert.Equal(new[] { 100d, 150d, 200d }, result);
    }

    [Fact]
    public void ReadTimeSeries_WithMissingBins_ZerosMissingEntries()
    {
        var csv = @"bin_index,value
0,10
2,30
";
        using var tempFile = TempFile.Create(csv);

        var result = CsvReader.ReadTimeSeries(tempFile.Path, expectedBins: 3);

        Assert.Equal(10d, result[0]);
        Assert.Equal(0d, result[1]);
        Assert.Equal(30d, result[2]);
    }

    [Fact]
    public void ReadTimeSeries_WithInvalidHeader_Throws()
    {
        var csv = @"bad_header
0,10
";
        using var tempFile = TempFile.Create(csv);

        Assert.Throws<InvalidDataException>(() => CsvReader.ReadTimeSeries(tempFile.Path, 2));
    }

    [Fact]
    public void ReadTimeSeries_MissingFile_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => CsvReader.ReadTimeSeries("missing.csv", 2));
    }

    [Fact]
    public void ReadTimeSeries_BinIndexOutOfRange_Throws()
    {
        var csv = @"bin_index,value
0,10
3,20
";
        using var tempFile = TempFile.Create(csv);

        Assert.Throws<InvalidDataException>(() => CsvReader.ReadTimeSeries(tempFile.Path, 3));
    }

    private sealed class TempFile : IDisposable
    {
        private TempFile(string path) => Path = path;

        public string Path { get; }

        public static TempFile Create(string content)
        {
            var path = System.IO.Path.GetTempFileName();
            File.WriteAllText(path, content);
            return new TempFile(path);
        }

        public void Dispose()
        {
            if (File.Exists(Path))
            {
                try
                {
                    File.Delete(Path);
                }
                catch
                {
                    // ignore cleanup failures
                }
            }
        }
    }
}
