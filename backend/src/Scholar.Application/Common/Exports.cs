namespace Scholar.Application.Common;

public enum ExportFormat { Csv, Xlsx }

public sealed record ExportFile(byte[] Content, string ContentType, string FileName);

/// <summary>Writes tabular data as CSV (UTF-8 with BOM so Excel shows Urdu names correctly) or XLSX.</summary>
public interface ITabularExporter
{
    ExportFile Write(string name, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows, ExportFormat format);
}

/// <summary>Reads a CSV with a header row into dictionaries keyed by normalised header names (lowercase, underscores).</summary>
public interface ITabularReader
{
    IReadOnlyList<IReadOnlyDictionary<string, string>> ReadCsv(Stream content);
}

public sealed record ImportRowError(int Row, string Column, string Message);

public sealed record ImportReport(int TotalRows, int ValidRows, IReadOnlyList<ImportRowError> Errors, bool Committed, int Created);
