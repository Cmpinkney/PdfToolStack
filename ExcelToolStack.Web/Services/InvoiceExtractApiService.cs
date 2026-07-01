using System.Net.Http.Headers;

namespace ExcelToolStack.Web.Services;

public sealed class InvoiceExtractApiService
{
    private readonly HttpClient _http;

    public InvoiceExtractApiService(HttpClient http)
    {
        _http = http;
    }

    public async Task<HttpResponseMessage> ExtractInvoiceToExcelAsync(
        byte[] fileBytes,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", fileName);

        return await _http.PostAsync(
            "api/excel-ai/extract-invoice", content, cancellationToken);
    }
}
