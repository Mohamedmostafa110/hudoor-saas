using System.Text;
using Hudoor.Api.Services;

namespace Hudoor.Api.Tests.Unit;

public class ExportServiceTests
{
    private readonly IExportService _service = new ExportService();

    [Fact]
    public void GenerateCsv_StartsWithUtf8Bom()
    {
        var headers = new[] { "Code", "Name" };
        var rows = new[]
        {
            new[] { "HD-123456", "أحمد محمود" }
        };

        var bytes = _service.GenerateCsv(headers, rows);

        // UTF-8 BOM is 0xEF, 0xBB, 0xBF
        Assert.True(bytes.Length >= 3);
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);
    }

    [Fact]
    public void GenerateCsv_ProperlyEscapesCommasAndQuotes()
    {
        var headers = new[] { "Name", "Note" };
        var rows = new[]
        {
            new[] { "John, Doe", "Says \"Hello\"" }
        };

        var bytes = _service.GenerateCsv(headers, rows);
        var content = Encoding.UTF8.GetString(bytes[3..]); // skip BOM

        Assert.Contains("\"John, Doe\"", content);
        Assert.Contains("\"Says \"\"Hello\"\"\"", content);
    }
}
