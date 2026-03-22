using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PdfToolkit.Web;
using PdfToolkit.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Register HttpClient pointing to our API
builder.Services.AddScoped(sp =>
    new HttpClient
    {
        BaseAddress = new Uri(
            builder.Configuration["ApiBaseUrl"]
            ?? "https://localhost:7001/")
    });

// Register our API service
builder.Services.AddScoped<ApiService>();

await builder.Build().RunAsync();