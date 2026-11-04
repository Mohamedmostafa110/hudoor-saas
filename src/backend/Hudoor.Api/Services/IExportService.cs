namespace Hudoor.Api.Services;

public interface IExportService
{
    /// <summary>
    /// Generates RFC 4180 compliant CSV bytes with UTF-8 BOM (\uFEFF)
    /// to ensure Arabic text renders properly in Microsoft Excel and Google Sheets.
    /// </summary>
    byte[] GenerateCsv(IEnumerable<string> headers, IEnumerable<IEnumerable<string?>> rows);
}
