using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PdfToolStack.Application.Interfaces;
using PdfToolStack.Infrastructure.Configuration;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PdfToolStack.Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly HttpClient _http;
        private readonly EmailOptions _options;
        private readonly ILogger<EmailService> _logger;

        public EmailService(
            HttpClient http,
            IOptions<EmailOptions> options,
            ILogger<EmailService> logger)
        {
            _http = http;
            _options = options.Value;
            _logger = logger;

            _http.BaseAddress = new Uri("https://api.resend.com/");
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }

        public async Task SendWelcomeEmailAsync(
            string toEmail, string name)
        {
            var subject = "Welcome to PdfToolStack!";
            var html = $"""
                <div style="font-family:sans-serif;max-width:600px;margin:0 auto;">
                    <div style="background:#2250F4;padding:2rem;border-radius:12px 12px 0 0;text-align:center;">
                        <h1 style="color:white;margin:0;font-size:1.5rem;">Welcome to PdfToolStack</h1>
                    </div>
                    <div style="padding:2rem;background:#f9f9f9;border-radius:0 0 12px 12px;">
                        <p style="font-size:1rem;color:#333;">Hi {name},</p>
                        <p style="color:#555;">Thanks for joining PdfToolStack — the AI document workspace for small teams.</p>
                        <p style="color:#555;">You now have access to <strong>35+ free PDF tools</strong> including:</p>
                        <ul style="color:#555;">
                            <li>Compress, merge, split and convert PDFs</li>
                            <li>Sign, protect, and redact documents</li>
                            <li>Fill PDF forms online</li>
                        </ul>
                        <p style="color:#555;">Want AI-powered tools like invoice data extraction and contract review?</p>
                        <a href="https://pdftoolstack.com/pricing"
                           style="display:inline-block;background:#2250F4;color:white;padding:0.75rem 1.5rem;border-radius:8px;text-decoration:none;font-weight:600;margin:1rem 0;">
                            Upgrade to Pro — $19/mo
                        </a>
                        <p style="color:#888;font-size:0.85rem;margin-top:2rem;">
                            Questions? Reply to this email anytime.<br/>
                            — The PdfToolStack Team
                        </p>
                    </div>
                </div>
                """;

            await SendAsync(toEmail, subject, html);
        }

        public async Task SendProWelcomeEmailAsync(
            string toEmail, string name)
        {
            var subject = "⭐ You're now a PdfToolStack Pro member!";
            var html = $"""
                <div style="font-family:sans-serif;max-width:600px;margin:0 auto;">
                    <div style="background:linear-gradient(135deg,#2250F4,#7C3AED);padding:2rem;border-radius:12px 12px 0 0;text-align:center;">
                        <h1 style="color:white;margin:0;font-size:1.5rem;">⭐ Welcome to Pro!</h1>
                    </div>
                    <div style="padding:2rem;background:#f9f9f9;border-radius:0 0 12px 12px;">
                        <p style="font-size:1rem;color:#333;">Hi {name},</p>
                        <p style="color:#555;">Your Pro subscription is now active. Here's what you've unlocked:</p>
                        <ul style="color:#555;">
                            <li>✨ AI Invoice Data Extractor — extract to Excel automatically</li>
                            <li>✨ AI Contract Reviewer — catch risky clauses instantly</li>
                            <li>✨ Chat with PDF — ask your documents anything</li>
                            <li>✨ AI Summarizer — key points in seconds</li>
                            <li>📦 Batch Processing — up to 20 files at once</li>
                            <li>📁 Unlimited file size</li>
                            <li>📋 30-day document history</li>
                        </ul>
                        <a href="https://pdftoolstack.com/ai-pdf-assist"
                           style="display:inline-block;background:#7C3AED;color:white;padding:0.75rem 1.5rem;border-radius:8px;text-decoration:none;font-weight:600;margin:1rem 0;">
                            Explore AI Tools →
                        </a>
                        <p style="color:#888;font-size:0.85rem;margin-top:2rem;">
                            Manage your subscription anytime from your
                            <a href="https://pdftoolstack.com/account" style="color:#2250F4;">account page</a>.<br/>
                            — The PdfToolStack Team
                        </p>
                    </div>
                </div>
                """;

            await SendAsync(toEmail, subject, html);
        }

        public async Task SendWaitlistConfirmationAsync(
            string toEmail, string toolName)
        {
            var subject = $"You're on the waitlist — {toolName}";
            var html = $"""
                <div style="font-family:sans-serif;max-width:600px;margin:0 auto;">
                    <div style="background:#0D9488;padding:2rem;border-radius:12px 12px 0 0;text-align:center;">
                        <h1 style="color:white;margin:0;font-size:1.5rem;">You're on the list!</h1>
                    </div>
                    <div style="padding:2rem;background:#f9f9f9;border-radius:0 0 12px 12px;">
                        <p style="color:#555;">
                            Thanks for your interest in <strong>{toolName}</strong>.
                            We'll email you the moment it launches.
                        </p>
                        <p style="color:#555;">
                            In the meantime, check out our 35+ free PDF tools and AI features
                            that are live right now.
                        </p>
                        <a href="https://pdftoolstack.com"
                           style="display:inline-block;background:#0D9488;color:white;padding:0.75rem 1.5rem;border-radius:8px;text-decoration:none;font-weight:600;margin:1rem 0;">
                            Browse Tools →
                        </a>
                        <p style="color:#888;font-size:0.85rem;margin-top:2rem;">
                            — The PdfToolStack Team
                        </p>
                    </div>
                </div>
                """;

            await SendAsync(toEmail, subject, html);
        }

        public async Task SendContactFormEmailAsync(
            string fromEmail, string name, string message)
        {
            var subject = $"Contact form: message from {name}";
            var html = $"""
                <div style="font-family:sans-serif;max-width:600px;margin:0 auto;padding:2rem;">
                    <h2 style="color:#2250F4;">New contact form submission</h2>
                    <p><strong>Name:</strong> {name}</p>
                    <p><strong>Email:</strong> {fromEmail}</p>
                    <p><strong>Message:</strong></p>
                    <div style="background:#f0f0f0;padding:1rem;border-radius:8px;color:#333;">
                        {message}
                    </div>
                </div>
                """;

            // Send to your support inbox
            await SendAsync(
                _options.FromEmail,
                subject,
                html,
                replyTo: fromEmail);
        }

        private async Task SendAsync(
            string toEmail,
            string subject,
            string html,
            string? replyTo = null)
        {
            if (string.IsNullOrEmpty(_options.ApiKey))
            {
                _logger.LogWarning(
                    "Resend API key not configured — skipping email to {Email}",
                    toEmail);
                return;
            }

            try
            {
                var payload = new
                {
                    from = $"{_options.FromName} <{_options.FromEmail}>",
                    to = new[] { toEmail },
                    subject,
                    html,
                    reply_to = replyTo
                };

                var json = JsonSerializer.Serialize(payload,
                    new JsonSerializerOptions
                    {
                        DefaultIgnoreCondition =
                            System.Text.Json.Serialization
                            .JsonIgnoreCondition.WhenWritingNull
                    });

                var response = await _http.PostAsync(
                    "emails",
                    new StringContent(json, Encoding.UTF8,
                        "application/json"));

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content
                        .ReadAsStringAsync();
                    _logger.LogError(
                        "Resend API error {Status}: {Error}",
                        response.StatusCode, error);
                }
                else
                {
                    _logger.LogInformation(
                        "Email sent to {Email}: {Subject}",
                        toEmail, subject);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to send email to {Email}", toEmail);
            }
        }
    }
}