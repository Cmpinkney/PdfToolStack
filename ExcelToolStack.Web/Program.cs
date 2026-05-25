using ExcelToolStack.Web;
using ExcelToolStack.Web.Configuration;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var branding = new ProductBranding(
    builder.Configuration["Product:SiteName"] ?? "ExcelToolStack",
    builder.Configuration["Product:SiteUrl"] ?? "https://exceltoolstack.com",
    builder.Configuration["Product:SupportEmail"] ?? "support@exceltoolstack.com");

builder.Services.AddSingleton(branding);

await builder.Build().RunAsync();
