using System.Globalization;
using System.Text.Json;

namespace Nerves.Cli.CommandLine;

/// <summary>
/// Human tables for a terminal, one JSON document when <c>--json</c> is set.
/// Every command writes through here so the two modes cannot drift apart.
/// </summary>
public sealed class CliOutput(bool json)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public bool Json { get; } = json;

    public void Line(string text)
    {
        if (!Json)
        {
            Console.WriteLine(text);
        }
    }

    public void Table(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);

        if (Json)
        {
            return;
        }

        var widths = new int[headers.Count];
        for (var column = 0; column < headers.Count; column++)
        {
            widths[column] = headers[column].Length;
            foreach (var row in rows)
            {
                if (column < row.Count)
                {
                    widths[column] = Math.Max(widths[column], row[column].Length);
                }
            }
        }

        Console.WriteLine(Format(headers, widths));

        foreach (var row in rows)
        {
            Console.WriteLine(Format(row, widths));
        }
    }

    /// <summary>Writes the machine-readable payload; a no-op in table mode.</summary>
    public void Emit(object payload)
    {
        if (!Json)
        {
            return;
        }

        Console.WriteLine(JsonSerializer.Serialize(payload, SerializerOptions));
    }

    private static string Format(IReadOnlyList<string> cells, int[] widths)
    {
        var parts = new List<string>(cells.Count);

        for (var column = 0; column < cells.Count; column++)
        {
            parts.Add(cells[column].PadRight(widths[column]));
        }

        return string.Join("  ", parts).TrimEnd();
    }

    /// <summary>Current UTC timestamp in the sortable form used in reports.</summary>
    public static string Timestamp() =>
        DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
}
