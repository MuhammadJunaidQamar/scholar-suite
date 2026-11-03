using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Scholar.Application.Common;

namespace Scholar.Infrastructure.Exports;

internal sealed class TabularExporter : ITabularExporter
{
    public ExportFile Write(string name, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows, ExportFormat format)
    {
        var stamp = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var safeName = new string(name.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray()).Trim('-');
        return format == ExportFormat.Xlsx ? WriteXlsx(name, safeName, stamp, headers, rows) : WriteCsv(safeName, stamp, headers, rows);
    }

    private static ExportFile WriteCsv(string safeName, string stamp, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows)
    {
        using var memory = new MemoryStream();
        using (var writer = new StreamWriter(memory, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true))
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            foreach (var header in headers) csv.WriteField(header);
            csv.NextRecord();
            foreach (var row in rows)
            {
                foreach (var value in row) csv.WriteField(Format(value));
                csv.NextRecord();
            }
        }

        return new ExportFile(memory.ToArray(), "text/csv; charset=utf-8", $"{safeName}-{stamp}.csv");
    }

    private static ExportFile WriteXlsx(string title, string safeName, string stamp, IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<object?>> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(title.Length > 31 ? title[..31] : title);
        for (var c = 0; c < headers.Count; c++)
        {
            var cell = sheet.Cell(1, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F0EB");
        }

        var r = 2;
        foreach (var row in rows)
        {
            for (var c = 0; c < row.Count; c++)
            {
                var cell = sheet.Cell(r, c + 1);
                switch (row[c])
                {
                    case null: break;
                    case decimal d: cell.Value = d; cell.Style.NumberFormat.Format = "#,##0.00"; break;
                    case int i: cell.Value = i; break;
                    case long l: cell.Value = l; break;
                    case DateOnly date: cell.Value = date.ToDateTime(TimeOnly.MinValue); cell.Style.DateFormat.Format = "dd-mmm-yyyy"; break;
                    case DateTimeOffset dto: cell.Value = dto.UtcDateTime; cell.Style.DateFormat.Format = "dd-mmm-yyyy hh:mm"; break;
                    case bool b: cell.Value = b ? "Yes" : "No"; break;
                    default: cell.Value = row[c]!.ToString(); break;
                }
            }

            r++;
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Range(1, 1, Math.Max(1, r - 1), headers.Count).SetAutoFilter();
        sheet.Columns().AdjustToContents(1, Math.Min(r, 200), 8, 60);
        using var memory = new MemoryStream();
        workbook.SaveAs(memory);
        return new ExportFile(memory.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{safeName}-{stamp}.xlsx");
    }

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTimeOffset dto => dto.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        decimal m => m.ToString("0.00", CultureInfo.InvariantCulture),
        bool b => b ? "Yes" : "No",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
    };
}

internal sealed class TabularReader : ITabularReader
{
    public IReadOnlyList<IReadOnlyDictionary<string, string>> ReadCsv(Stream content)
    {
        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            PrepareHeaderForMatch = a => Normalize(a.Header),
            MissingFieldFound = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim,
            DetectDelimiter = true,
        };
        using var csv = new CsvReader(reader, config);
        var rows = new List<IReadOnlyDictionary<string, string>>();
        if (!csv.Read() || !csv.ReadHeader()) return rows;
        var headers = csv.HeaderRecord!.Select(Normalize).ToArray();
        while (csv.Read())
        {
            var row = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < headers.Length; i++)
            {
                row[headers[i]] = csv.GetField(i)?.Trim() ?? string.Empty;
            }

            if (row.Values.Any(v => v.Length > 0)) rows.Add(row);
        }

        return rows;
    }

    private static string Normalize(string header) =>
        string.Join('_', header.Trim().ToLowerInvariant().Split([' ', '-', '/', '.'], StringSplitOptions.RemoveEmptyEntries));
}
