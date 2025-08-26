using System.Globalization;
using System.Text;
using System.Text.Json;

namespace FlowTime.Sim.Cli;

public static class Writers
{
    public static async Task WriteCsvAsync(FlowTimeRunResponse res, Stream output, CancellationToken ct)
    {
        var sb = new StringBuilder();

        // Header
        sb.Append("bin,index");
        foreach (var name in res.order) sb.Append(',').Append(CsvEscape(name));
        sb.AppendLine();

        // Rows: assume series[j][i] aligns with order[j] at bin i
        for (var i = 0; i < res.grid.bins; i++)
        {
            sb.Append(i.ToString(CultureInfo.InvariantCulture))
              .Append(',')
              .Append(i.ToString(CultureInfo.InvariantCulture));

            for (var j = 0; j < res.order.Length; j++)
            {
                var name = res.order[j];
                double v = double.NaN;
                if (res.series.TryGetValue(name, out var arr) && i < arr.Length)
                    v = arr[i];
                sb.Append(',').Append(v.ToString(CultureInfo.InvariantCulture));
            }
            sb.AppendLine();
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        await output.WriteAsync(bytes.AsMemory(0, bytes.Length), ct).ConfigureAwait(false);
    }

    public static async Task WriteJsonAsync(FlowTimeRunResponse res, Stream output, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(res, new JsonSerializerOptions { WriteIndented = true });
        var bytes = Encoding.UTF8.GetBytes(json);
        await output.WriteAsync(bytes.AsMemory(0, bytes.Length), ct).ConfigureAwait(false);
    }

    static string CsvEscape(string s)
    {
        return s.Contains(',') || s.Contains('"')
            ? "\"" + s.Replace("\"", "\"\"") + "\""
            : s;
    }
}
