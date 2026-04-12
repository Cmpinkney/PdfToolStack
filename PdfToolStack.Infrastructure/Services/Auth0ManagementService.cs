using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PdfToolStack.Application.Interfaces;
using System.Net.Http.Headers;
using System.Text.Json;

namespace PdfToolStack.Infrastructure.Services
{
    public class Auth0ManagementService : IAuth0ManagementService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly ILogger<Auth0ManagementService> _logger;

        public Auth0ManagementService(
            HttpClient httpClient,
            IConfiguration config,
            ILogger<Auth0ManagementService> logger)
        {
            _httpClient = httpClient;
            _config = config;
            _logger = logger;
        }

        public async Task DeleteUserAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            var token = await GetManagementTokenAsync(cancellationToken);
            var domain = _config["Auth0:Authority"]!
                .TrimEnd('/');

            var request = new HttpRequestMessage(
                HttpMethod.Delete,
                $"{domain}/api/v2/users/{Uri.EscapeDataString(userId)}");

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient
                .SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content
                    .ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Auth0 delete failed for {UserId}: {Status} {Body}",
                    userId, response.StatusCode, body);
                throw new Exception(
                    $"Auth0 delete failed: {response.StatusCode}");
            }

            _logger.LogInformation(
                "Auth0 user deleted: {UserId}", userId);
        }

        private async Task<string> GetManagementTokenAsync(
            CancellationToken cancellationToken)
        {
            var domain = _config["Auth0:Authority"]!.TrimEnd('/');
            var clientId = _config["Auth0:ManagementClientId"]!;
            var clientSecret = _config["Auth0:ManagementClientSecret"]!;

            var body = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("grant_type", "client_credentials"),
                new KeyValuePair<string,string>("client_id", clientId),
                new KeyValuePair<string,string>("client_secret", clientSecret),
                new KeyValuePair<string,string>("audience", $"{domain}/api/v2/")
            });

            var response = await _httpClient.PostAsync(
                $"{domain}/oauth/token", body, cancellationToken);

            response.EnsureSuccessStatusCode();

            var json = await response.Content
                .ReadAsStringAsync(cancellationToken);
            var doc = JsonDocument.Parse(json);
            return doc.RootElement
                .GetProperty("access_token").GetString()!;
        }
    }
}