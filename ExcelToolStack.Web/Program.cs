using ExcelToolStack.Web;
using ExcelToolStack.Web.Configuration;
using ExcelToolStack.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var branding = new ProductBranding(
    builder.Configuration["Product:SiteName"] ?? "ExcelToolStack",
    builder.Configuration["Product:SiteUrl"] ?? "https://exceltoolstack.com",
    builder.Configuration["Product:SupportEmail"] ?? "support@exceltoolstack.com");

builder.Services.AddSingleton(branding);

var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? "https://pdftoolstack-api-grcxhqergtgcd0g7.westus2-01.azurewebsites.net/";

builder.Services.AddScoped(_ =>
    new FormulaApiService(new HttpClient
    {
        BaseAddress = new Uri(apiBaseUrl)
    }));
builder.Services.AddScoped<FormulaUsageService>();

var auth0Authority = builder.Configuration["Auth0:Authority"];
var auth0ClientId = builder.Configuration["Auth0:ClientId"];

if (!string.IsNullOrWhiteSpace(auth0Authority) &&
    !string.IsNullOrWhiteSpace(auth0ClientId))
{
    builder.Services.AddOidcAuthentication(options =>
    {
        options.ProviderOptions.Authority = auth0Authority;
        options.ProviderOptions.ClientId = auth0ClientId;
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
}
else
{
    builder.Services.AddAuthorizationCore();
    builder.Services.AddScoped<AuthenticationStateProvider,
        AnonymousAuthenticationStateProvider>();
}

await builder.Build().RunAsync();
