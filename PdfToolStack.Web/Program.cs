using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PdfToolStack.Web;
using PdfToolStack.Web.Services;
using Syncfusion.Blazor;
using Syncfusion.Licensing;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

var syncfusionKey = builder.Configuration["Syncfusion:LicenseKey"];
if (!string.IsNullOrWhiteSpace(syncfusionKey))
{
    SyncfusionLicenseProvider.RegisterLicense(syncfusionKey);
}
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? "https://pdftoolstack-api-grcxhqergtgcd0g7.westus2-01.azurewebsites.net/";

builder.Services.AddHttpClient<ApiService>(
    client => client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<OptionalAuthorizationMessageHandler>();

builder.Services.AddTransient<OptionalAuthorizationMessageHandler>();

builder.Services.AddOidcAuthentication(options =>
{
    options.ProviderOptions.Authority =
        builder.Configuration["Auth0:Authority"];

    options.ProviderOptions.ClientId =
        builder.Configuration["Auth0:ClientId"];

    options.ProviderOptions.ResponseType = "code";

    options.ProviderOptions.DefaultScopes.Add("openid");
    options.ProviderOptions.DefaultScopes.Add("profile");
    options.ProviderOptions.DefaultScopes.Add("email");

    var audience = builder.Configuration["Auth0:Audience"];

    if (!string.IsNullOrWhiteSpace(audience))
    {
        options.ProviderOptions.AdditionalProviderParameters
            .Add("audience", audience);
    }

    options.ProviderOptions.AdditionalProviderParameters
        .Add("post_logout_redirect_uri",
            builder.HostEnvironment.BaseAddress);
});

builder.Services.AddSyncfusionBlazor();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<FileValidationService>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<CookieConsentService>();
builder.Services.AddScoped<SubscriptionService>();
builder.Services.AddScoped<SessionUsageService>();

builder.Services.AddHttpClient<CloudPickerService>(
    client => client.BaseAddress = new Uri(apiBaseUrl));

await builder.Build().RunAsync();
