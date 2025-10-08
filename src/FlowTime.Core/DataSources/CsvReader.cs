using System;
using System.Globalization;
using System.IO;

namespace FlowTime.Core.DataSources;

public static class CsvReader
{
    public static double[] ReadTimeSeries(string filePath, int expectedBins)
    {
        if (expectedBins <= 0)
            throw new ArgumentOutOfRangeException(nameof(expectedBins), expectedBins, "expectedBins must be positive");

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"CSV file not found: {filePath}", filePath);

        using var reader = new StreamReader(filePath);
        var header = reader.ReadLine();
        if (header is null)
            throw new InvalidDataException($"CSV file {filePath} is empty");

        if (!string.Equals(header.Trim(), "bin_index,value", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Invalid CSV header in {filePath}. Expected 'bin_index,value'.");

        var values = new double[expectedBins];
        var populated = new bool[expectedBins];

        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Split(',');
            if (parts.Length != 2)
                throw new InvalidDataException($"Invalid CSV row '{line}' in {filePath}.");

            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var binIndex))
                throw new InvalidDataException($"Invalid bin_index '{parts[0]}' in {filePath}.");

            if (binIndex < 0 || binIndex >= expectedBins)
                throw new InvalidDataException($"Bin index {binIndex} out of range for {filePath} (expected 0..{expectedBins - 1}).");

            double value = double.NaN;
            var rawValue = parts[1];
            if (!string.IsNullOrWhiteSpace(rawValue) && !rawValue.Equals("NaN", StringComparison.OrdinalIgnoreCase))
                value = double.Parse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);

            if (populated[binIndex])
                throw new InvalidDataException($"Duplicate bin_index {binIndex} in {filePath}.");

            values[binIndex] = value;
            populated[binIndex] = true;
        }

        return values;
    }
}
