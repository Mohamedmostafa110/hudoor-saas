using System.Text;

namespace Hudoor.Api.Services;

public class ExportService : IExportService
{
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    public byte[] GenerateCsv(IEnumerable<string> headers, IEnumerable<IEnumerable<string?>> rows)
    {
        var sb = new StringBuilder();

        // Write header row
        sb.AppendLine(string.Join(",", headers.Select(EscapeField)));

        // Write data rows
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",", row.Select(EscapeField)));
        }

        var csvTextBytes = Encoding.UTF8.GetBytes(sb.ToString());

        // Prepend UTF-8 BOM
        var result = new byte[Utf8Bom.Length + csvTextBytes.Length];
        Buffer.BlockCopy(Utf8Bom, 0, result, 0, Utf8Bom.Length);
        Buffer.BlockCopy(csvTextBytes, 0, result, Utf8Bom.Length, csvTextBytes.Length);

        return result;
    }

    private static string EscapeField(string? field)
    {
        if (string.IsNullOrEmpty(field))
        {
            return string.Empty;
        }

        // If the field contains comma, quote, or newline, escape quotes and wrap in quotes
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }
}
