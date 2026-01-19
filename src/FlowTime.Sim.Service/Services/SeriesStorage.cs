using System.Globalization;
using System.Text.Json;

namespace FlowTime.Sim.Service.Services;

internal sealed class SeriesStorage
{
    private readonly string root;

    public SeriesStorage(string root)
    {
        this.root = root;
        Directory.CreateDirectory(root);
    }

    public SeriesDocument Save(SeriesDocument document)
    {
        var seriesDir = Path.Combine(root, document.SeriesId);
        Directory.CreateDirectory(seriesDir);

        var jsonPath = Path.Combine(seriesDir, "series.json");
        var json = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(jsonPath, json);

        return document;
    }

    public SeriesDocument Load(string seriesId)
    {
        var seriesDir = Path.Combine(root, seriesId);
        var jsonPath = Path.Combine(seriesDir, "series.json");
        if (!File.Exists(jsonPath))
        {
            throw new FileNotFoundException($"Series '{seriesId}' not found.", jsonPath);
        }

        var json = File.ReadAllText(jsonPath);
        var document = JsonSerializer.Deserialize<SeriesDocument>(json);
        if (document == null)
        {
            throw new InvalidOperationException($"Failed to parse series '{seriesId}'.");
        }

        return document;
    }
}

internal sealed record SeriesDocument
{
    public string SeriesId { get; init; } = string.Empty;
    public int[] Bins { get; init; } = Array.Empty<int>();
    public double[] Values { get; init; } = Array.Empty<double>();
    public SeriesMetadata? Metadata { get; init; }
    public string SourceFormat { get; init; } = "csv";
    public DateTimeOffset IngestedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record SeriesMetadata
{
    public string? Units { get; init; }
    public string? Source { get; init; }
    public int? BinSize { get; init; }
    public string? BinUnit { get; init; }
    public string? Timezone { get; init; }
    public SeriesTimeRange? TimeRange { get; init; }
}

public sealed record SeriesTimeRange
{
    public DateTimeOffset? Start { get; init; }
    public DateTimeOffset? End { get; init; }
}

internal sealed record SeriesParseResult(
    int[] Bins,
    double[] Values,
    SeriesParseDiagnostics Diagnostics);

internal sealed record SeriesParseDiagnostics(
    string Delimiter,
    bool HasHeader,
    string[] Columns,
    int RowCount);

internal static class SeriesParser
{
    public static SeriesParseResult Parse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Series content is empty.");
        }

        var lines = content
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToArray();

        if (lines.Length == 0)
        {
            throw new InvalidOperationException("Series content is empty.");
        }

        var delimiter = DetectDelimiter(lines[0]);
        var firstRow = SplitLine(lines[0], delimiter);
        var hasHeader = DetectHeader(firstRow);

        var columns = hasHeader
            ? firstRow.Select(c => c.Trim()).ToArray()
            : new[] { "bin", "value" };

        var binIndex = FindColumnIndex(columns, "bin");
        var valueIndex = FindColumnIndex(columns, "value");

        if (!hasHeader)
        {
            binIndex = 0;
            valueIndex = 1;
        }

        if (binIndex < 0 || valueIndex < 0)
        {
            throw new InvalidOperationException("CSV must include 'bin' and 'value' columns.");
        }

        var startIndex = hasHeader ? 1 : 0;
        var bins = new List<int>();
        var values = new List<double>();

        for (var i = startIndex; i < lines.Length; i++)
        {
            var row = SplitLine(lines[i], delimiter);
            if (row.Length <= Math.Max(binIndex, valueIndex))
            {
                throw new InvalidOperationException($"Row {i + 1} is missing required columns.");
            }

            var binText = row[binIndex].Trim();
            if (!int.TryParse(binText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bin) || bin < 0)
            {
                throw new InvalidOperationException($"Row {i + 1} has invalid bin '{binText}'.");
            }

            var valueText = row[valueIndex].Trim();
            if (!double.TryParse(valueText, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value))
            {
                throw new InvalidOperationException($"Row {i + 1} has invalid value '{valueText}'.");
            }

            bins.Add(bin);
            values.Add(value);
        }

        if (bins.Count == 0)
        {
            throw new InvalidOperationException("No data rows were found.");
        }

        var duplicates = bins.GroupBy(b => b).FirstOrDefault(g => g.Count() > 1);
        if (duplicates != null)
        {
            throw new InvalidOperationException($"Duplicate bin '{duplicates.Key}' found.");
        }

        var ordered = bins
            .Select((bin, index) => new { bin, value = values[index] })
            .OrderBy(entry => entry.bin)
            .ToArray();

        var minBin = ordered[0].bin;
        if (minBin != 0)
        {
            throw new InvalidOperationException("Bin indices must start at 0.");
        }

        for (var i = 0; i < ordered.Length; i++)
        {
            if (ordered[i].bin != i)
            {
                throw new InvalidOperationException("Bin indices must be contiguous.");
            }
        }

        return new SeriesParseResult(
            ordered.Select(entry => entry.bin).ToArray(),
            ordered.Select(entry => entry.value).ToArray(),
            new SeriesParseDiagnostics(delimiter.ToString(), hasHeader, columns, ordered.Length));
    }

    private static char DetectDelimiter(string line)
    {
        var commaCount = line.Count(ch => ch == ',');
        var tabCount = line.Count(ch => ch == '\t');
        if (tabCount > commaCount)
        {
            return '\t';
        }

        return ',';
    }

    private static string[] SplitLine(string line, char delimiter)
    {
        return line.Split(delimiter).Select(value => value.Trim()).ToArray();
    }

    private static bool DetectHeader(string[] row)
    {
        if (row.Length < 2)
        {
            return false;
        }

        var first = row[0].Trim();
        if (first.Equals("bin", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !int.TryParse(first, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }

    private static int FindColumnIndex(string[] columns, string name)
    {
        for (var i = 0; i < columns.Length; i++)
        {
            if (columns[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}
