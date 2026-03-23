using PdfToolkit.Domain.Enums;
using PdfToolkit.Application.DTOs;
using System.Net.Http.Json;

namespace PdfToolkit.Web.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ApiService> _logger;

        public ApiService(
            HttpClient httpClient,
            ILogger<ApiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<ProcessResponse?> ProcessPdfAsync(
            byte[] fileBytes,
            string fileName,
            ToolType toolType,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                using var fileContent = new ByteArrayContent(fileBytes);

                fileContent.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue(
                        "application/pdf");

                content.Add(fileContent, "file", fileName);

                var response = await _httpClient.PostAsync(
                    $"api/pdf/process?toolType={(int)toolType}",
                    content,
                    cancellationToken);

                if (response.IsSuccessStatusCode)
                    return await response.Content
                        .ReadFromJsonAsync<ProcessResponse>(
                            cancellationToken: cancellationToken);

                _logger.LogWarning(
                    "API returned {StatusCode} for {ToolType}",
                    response.StatusCode,
                    toolType);

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error calling API for {ToolType}", toolType);
                return null;
            }
        }

        private byte[]? _lastMergedBytes;

        public async Task<ProcessResponse?> MergePdfsAsync(
            MultipartFormDataContent content,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.PostAsync(
                    "api/pdf/merge",
                    content,
                    cancellationToken);

                var responseBody = await response.Content
                    .ReadAsStringAsync(cancellationToken);

                _logger.LogInformation(
                    "Merge API Response {StatusCode}: {Body}",
                    response.StatusCode,
                    responseBody);

                if (response.IsSuccessStatusCode)
                {
                    // Store raw bytes for download
                    _lastMergedBytes = await response.Content
                        .ReadAsByteArrayAsync(cancellationToken);

                    return new ProcessResponse
                    {
                        IsSuccess = true,
                        OutputSizeBytes = _lastMergedBytes.Length
                    };
                }

                return new ProcessResponse
                {
                    IsSuccess = false,
                    ErrorMessage = responseBody
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling merge API");
                return new ProcessResponse
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public Task<byte[]?> GetMergedBytesAsync()
        {
            return Task.FromResult(_lastMergedBytes);
        }

        public async Task<DetectFieldsResponse?> DetectFormFieldsAsync(
        byte[] fileBytes,
        string fileName,
        CancellationToken cancellationToken = default)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                using var fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.ContentType =
                    new System.Net.Http.Headers
                        .MediaTypeHeaderValue("application/pdf");
                content.Add(fileContent, "file", fileName);

                var response = await _httpClient.PostAsync(
                    "api/pdf/detect-fields",
                    content,
                    cancellationToken);

                if (response.IsSuccessStatusCode)
                    return await response.Content
                        .ReadFromJsonAsync<DetectFieldsResponse>(
                            cancellationToken: cancellationToken);

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting form fields");
                return null;
            }
        }

        public async Task<byte[]?> FillFormAsync(
            byte[] fileBytes,
            string fileName,
            Dictionary<string, string> fieldValues,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                using var fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.ContentType =
                    new System.Net.Http.Headers
                        .MediaTypeHeaderValue("application/pdf");
                content.Add(fileContent, "file", fileName);

                var fieldsJson = System.Text.Json.JsonSerializer
                    .Serialize(fieldValues);
                content.Add(
                    new StringContent(fieldsJson), "fieldsJson");

                var response = await _httpClient.PostAsync(
                    "api/pdf/fill-form",
                    content,
                    cancellationToken);

                if (response.IsSuccessStatusCode)
                    return await response.Content
                        .ReadAsByteArrayAsync(cancellationToken);

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filling form");
                return null;
            }
        }

        public async Task<int> GetPageCountAsync(
        byte[] fileBytes,
        string fileName,
        CancellationToken cancellationToken = default)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                using var fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.ContentType =
                    new System.Net.Http.Headers
                        .MediaTypeHeaderValue("application/pdf");
                content.Add(fileContent, "file", fileName);

                var response = await _httpClient.PostAsync(
                    "api/pdf/page-count",
                    content,
                    cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content
                        .ReadFromJsonAsync<PageCountResponse>(
                            cancellationToken: cancellationToken);
                    return result?.PageCount ?? 1;
                }

                return 1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting page count");
                return 1;
            }
        }

        public async Task<byte[]?> RedactPdfAsync(
            byte[] fileBytes,
            string fileName,
            object regions,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                using var fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.ContentType =
                    new System.Net.Http.Headers
                        .MediaTypeHeaderValue("application/pdf");
                content.Add(fileContent, "file", fileName);

                var regionsJson = System.Text.Json.JsonSerializer
                    .Serialize(regions);
                content.Add(
                    new StringContent(regionsJson), "regionsJson");

                var response = await _httpClient.PostAsync(
                    "api/pdf/redact",
                    content,
                    cancellationToken);

                if (response.IsSuccessStatusCode)
                    return await response.Content
                        .ReadAsByteArrayAsync(cancellationToken);

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error redacting PDF");
                return null;
            }
        }

        private class PageCountResponse
        {
            public int PageCount { get; set; }
        }
        public async Task<JobStatusResponse?> GetJobStatusAsync(
            Guid jobId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<JobStatusResponse>(
                    $"api/pdf/status/{jobId}",
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error getting job status for {JobId}", jobId);
                return null;
            }
        }

        public async Task<byte[]?> DeletePagesAsync(
    byte[] fileBytes,
    string fileName,
    IEnumerable<int> pages)
        {
            using var content = new MultipartFormDataContent();
            content.Add(
                new ByteArrayContent(fileBytes),
                "file", fileName);
            content.Add(
                new StringContent(
                    string.Join(",", pages)),
                "pageNumbers");

            var response = await _httpClient.PostAsync(
                "api/pdf/delete-pages", content);

            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsByteArrayAsync();
            return null;
        }

        public async Task<byte[]?> WordToPdfAsync(
            byte[] fileBytes,
            string fileName)
        {
            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType =
                new System.Net.Http.Headers
                    .MediaTypeHeaderValue(
                        "application/vnd.openxmlformats-officedocument" +
                        ".wordprocessingml.document");
            content.Add(fileContent, "file", fileName);

            var response = await _httpClient.PostAsync(
                "api/pdf/word-to-pdf", content);

            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsByteArrayAsync();
            return null;
        }

        public async Task<byte[]?> OrganizePdfAsync(
            byte[] fileBytes,
            string fileName,
            object operations)
        {
            using var content = new MultipartFormDataContent();
            content.Add(
                new ByteArrayContent(fileBytes),
                "file", fileName);
            content.Add(
                new StringContent(
                    System.Text.Json.JsonSerializer
                        .Serialize(operations)),
                "operationsJson");

            var response = await _httpClient.PostAsync(
                "api/pdf/organize", content);

            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsByteArrayAsync();
            return null;
        }

        public async Task<byte[]?> SignPdfAsync(
            byte[] fileBytes,
            string fileName,
            byte[] signatureBytes,
            float x, float y,
            float width, float height,
            int pageNumber)
        {
            using var content = new MultipartFormDataContent();
            content.Add(
                new ByteArrayContent(fileBytes),
                "file", fileName);
            content.Add(
                new ByteArrayContent(signatureBytes),
                "signature", "signature.png");
            content.Add(new StringContent(
                x.ToString()), "x");
            content.Add(new StringContent(
                y.ToString()), "y");
            content.Add(new StringContent(
                width.ToString()), "width");
            content.Add(new StringContent(
                height.ToString()), "height");
            content.Add(new StringContent(
                pageNumber.ToString()), "pageNumber");

            var response = await _httpClient.PostAsync(
                "api/pdf/sign", content);

            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsByteArrayAsync();
            return null;
        }

        public async Task<byte[]?> EditPdfAsync(
            byte[] fileBytes,
            string fileName,
            object annotations)
        {
            using var content = new MultipartFormDataContent();
            content.Add(
                new ByteArrayContent(fileBytes),
                "file", fileName);
            content.Add(
                new StringContent(
                    System.Text.Json.JsonSerializer
                        .Serialize(annotations)),
                "annotationsJson");

            var response = await _httpClient.PostAsync(
                "api/pdf/edit", content);

            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsByteArrayAsync();
            return null;
        }

        public async Task<byte[]?> AnnotatePdfAsync(
            byte[] fileBytes,
            string fileName,
            object highlights)
        {
            using var content = new MultipartFormDataContent();
            content.Add(
                new ByteArrayContent(fileBytes),
                "file", fileName);
            content.Add(
                new StringContent(
                    System.Text.Json.JsonSerializer
                        .Serialize(highlights)),
                "highlightsJson");

            var response = await _httpClient.PostAsync(
                "api/pdf/annotate", content);

            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsByteArrayAsync();
            return null;
        }
    }
}
