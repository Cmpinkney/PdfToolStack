using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using PdfToolkit.Web;
using PdfToolkit.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// ── API HTTP Client ───────────────────────────────────────
var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? "https://localhost:7100/";

builder.Services.AddHttpClient<ApiService>(
    client => client.BaseAddress = new Uri(apiBaseUrl));

// ── Auth0 Authentication ──────────────────────────────────
builder.Services.AddOidcAuthentication(options =>
{
    builder.Configuration.Bind("Auth0", options.ProviderOptions);
    options.ProviderOptions.ResponseType = "code";
    options.ProviderOptions.AdditionalProviderParameters
        .Add("audience",
            $"https://{builder.Configuration["Auth0:Authority"]}/api/v2/");
});

// ── Services ──────────────────────────────────────────────
builder.Services.AddScoped<PaymentService>();

await builder.Build().RunAsync();