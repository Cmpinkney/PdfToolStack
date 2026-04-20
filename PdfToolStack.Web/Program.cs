using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PdfToolStack.Web;
using PdfToolStack.Web.Services;
using Syncfusion.Blazor;
using Syncfusion.Licensing;

SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1JHaF5cWWdCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdlWXtfeXVSRmddWUF2WUtWYEo=");
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? "https://localhost:7100/";

builder.Services.AddHttpClient<ApiService>(
    client => client.BaseAddress = new Uri(apiBaseUrl));

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