using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Serilog;
using PdfToolkit.API.Configuration;
using PdfToolkit.API.Middleware;
using PdfToolkit.Application.Factories;
using PdfToolkit.Application.Services;
using PdfToolkit.Application.Strategies;
using PdfToolkit.Domain.Interfaces;
using PdfToolkit.Infrastructure.Data;
using PdfToolkit.Infrastructure.Processors;
using PdfToolkit.Infrastructure.Repositories;
using PdfToolkit.Infrastructure.Storage;
using PdfToolkit.Infrastructure.Services;

// ── Serilog Bootstrap Logger ──────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting PdfToolkit API");

    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog ───────────────────────────────────────────────────────────
    builder.Host.UseSerilog((context, services, configuration) =>
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .WriteTo.Console());

    // ── Configuration Options ─────────────────────────────────────────────
    builder.Services.Configure<AzureStorageOptions>(
        builder.Configuration.GetSection(
            AzureStorageOptions.SectionName));

    builder.Services.Configure<ProcessingOptions>(
        builder.Configuration.GetSection(
            ProcessingOptions.SectionName));

    // ── Stripe ────────────────────────────────────────────────────────────────
    builder.Services.Configure<StripeOptions>(
        builder.Configuration.GetSection(StripeOptions.SectionName));

    builder.Services.Configure<FileLimit>(
        builder.Configuration.GetSection(FileLimit.SectionName));

    // ── Database ──────────────────────────────────────────────────────────
    builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration
            .GetConnectionString("DefaultConnection"),
        b => b.MigrationsAssembly("PdfToolkit.Infrastructure")));

    // ── Azure Blob Storage ────────────────────────────────────────────────
    builder.Services.AddSingleton(x =>
        new BlobServiceClient(
            builder.Configuration
                .GetSection(AzureStorageOptions.SectionName)
                ["ConnectionString"]));

    // ── Repositories ──────────────────────────────────────────────────────
    builder.Services.AddScoped<IJobRepository, JobRepository>();

    // ── Blob Storage Service ──────────────────────────────────────────────
    builder.Services.AddScoped<IBlobStorageService,
        AzureBlobStorageService>();

    // ── PDF Processors ────────────────────────────────────────────────────
    builder.Services.AddScoped<PdfCompressor>();
    builder.Services.AddScoped<PdfRedactor>();
    builder.Services.AddScoped<PdfMerger>();
    builder.Services.AddScoped<PdfToWordConverter>();
    builder.Services.AddScoped<PdfFormFiller>();
    builder.Services.AddScoped<DeletePagesProcessor>();
    builder.Services.AddScoped<WordToPdfProcessor>();
    builder.Services.AddScoped<OrganizePdfProcessor>();
    builder.Services.AddScoped<SignPdfProcessor>();
    builder.Services.AddScoped<EditPdfProcessor>();
    builder.Services.AddScoped<AnnotatePdfProcessor>();
    builder.Services.AddScoped<SubscriptionService>();

    // ── Strategies ────────────────────────────────────────────────────────
    builder.Services.AddScoped<IProcessingStrategy>(sp =>
    new CompressStrategy(
        sp.GetRequiredService<PdfCompressor>()));

    builder.Services.AddScoped<IProcessingStrategy>(sp =>
    new RedactStrategy(
        sp.GetRequiredService<PdfRedactor>()));

    builder.Services.AddScoped<IProcessingStrategy>(sp =>
    new MergeStrategy(
        sp.GetRequiredService<PdfMerger>()));

    builder.Services.AddScoped<IProcessingStrategy>(sp =>
    new PdfToWordStrategy(
        sp.GetRequiredService<PdfToWordConverter>()));

    builder.Services.AddScoped<IProcessingStrategy>(sp =>
    new FillFormStrategy(
        sp.GetRequiredService<PdfFormFiller>()));


    // ── Factory ───────────────────────────────────────────────────────────
    builder.Services.AddScoped<PdfProcessorFactory>(sp =>
    new PdfProcessorFactory(
        sp.GetServices<IProcessingStrategy>()));

    // ── Application Services ──────────────────────────────────────────────
    builder.Services.AddScoped<IPdfService, PdfService>();

    // ── CORS — allow Blazor frontend ──────────────────────────────────────
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("BlazorPolicy", policy =>
        {
            policy.WithOrigins(
                    "https://localhost:7025",
                    "http://localhost:7025",
                    "https://yoursite.com")
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
    });

    // ── Controllers + Swagger ─────────────────────────────────────────────
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    // ── Middleware Pipeline ───────────────────────────────────────────────
    // Order matters — error handling must be first
    app.UseMiddleware<ErrorHandlingMiddleware>();
    app.UseMiddleware<RateLimitingMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseCors("BlazorPolicy");
    app.UseAuthorization();
    app.MapControllers();

    // ── Auto-migrate database on startup ──────────────────────────────────
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();
        try
        {
            await db.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            Log.Warning("Migration warning: {Message}", ex.Message);
        }
    }

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "PdfToolkit API failed to start");
}
finally
{
    Log.CloseAndFlush();
}
