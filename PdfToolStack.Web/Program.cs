using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PdfToolStack.Web;
using PdfToolStack.Web.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

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

    // Fix Auth0 logout redirect
    options.ProviderOptions.AdditionalProviderParameters
        .Add("post_logout_redirect_uri",
            builder.HostEnvironment.BaseAddress);
});

builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<FileValidationService>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<CookieConsentService>();
builder.Services.AddScoped<SubscriptionService>();

await builder.Build().RunAsync();