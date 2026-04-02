using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using PdfToolStack.API.Configuration;
using PdfToolStack.API.Middleware;
using PdfToolStack.API.Services;
using PdfToolStack.Application.Factories;
using PdfToolStack.Application.Services;
using PdfToolStack.Application.Strategies;
using PdfToolStack.Domain.Interfaces;
using PdfToolStack.Infrastructure.Data;
using PdfToolStack.Infrastructure.Processors;
using PdfToolStack.Infrastructure.Repositories;
using PdfToolStack.Infrastructure.Services;
using PdfToolStack.Infrastructure.Storage;
using Serilog;

// ── Serilog Bootstrap Logger ──────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting PdfToolStack API");

    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog ───────────────────────────────────────────────────────────
    builder.Host.UseSerilog((context, services, configuration) =>
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .WriteTo.Console());

    // ── Configuration Options ─────────────────────────────────────────────
    builder.Services.Configure<AzureStorageOptions>(
        builder.Configuration.GetSection(AzureStorageOptions.SectionName));

    builder.Services.Configure<ProcessingOptions>(
        builder.Configuration.GetSection(ProcessingOptions.SectionName));

    builder.Services.Configure<StripeOptions>(
        builder.Configuration.GetSection(StripeOptions.SectionName));

    builder.Services.Configure<FileLimit>(
        builder.Configuration.GetSection(FileLimit.SectionName));

    // ── Config Values ─────────────────────────────────────────────────────
    var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection");

    var azureStorageConnectionString = builder.Configuration
        .GetSection(AzureStorageOptions.SectionName)["ConnectionString"];

    var hasDatabase = !string.IsNullOrWhiteSpace(defaultConnection);
    var hasBlobStorage = !string.IsNullOrWhiteSpace(azureStorageConnectionString);

    // ── Database ──────────────────────────────────────────────────────────
    if (hasDatabase)
    {
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                defaultConnection,
                b => b.MigrationsAssembly("PdfToolStack.Infrastructure")));
    }

    // ── Azure Blob Storage ────────────────────────────────────────────────
    if (hasBlobStorage)
    {
        builder.Services.AddSingleton(_ =>
            new BlobServiceClient(azureStorageConnectionString));
    }

    // ── Repositories ──────────────────────────────────────────────────────
    if (hasDatabase)
    {
        builder.Services.AddScoped<IJobRepository, JobRepository>();
    }
    else
    {
        builder.Services.AddScoped<IJobRepository, InMemoryJobRepository>();
    }

    // ── Blob Storage Service ──────────────────────────────────────────────
    if (hasBlobStorage)
    {
        builder.Services.AddScoped<IBlobStorageService, AzureBlobStorageService>();
    }

    // ── PDF Processors ────────────────────────────────────────────────────
    builder.Services.AddScoped<PdfCompressor>();
    builder.Services.AddScoped<PdfRedactor>();
    builder.Services.AddScoped<PdfMerger>();
    builder.Services.AddScoped<PdfToWordConverter>();
    builder.Services.AddScoped<PdfFormFiller>();
    builder.Services.AddScoped<WordToPdfProcessor>();
    builder.Services.AddScoped<OrganizePdfProcessor>();
    builder.Services.AddScoped<SignPdfProcessor>();
    builder.Services.AddScoped<EditPdfProcessor>();
    builder.Services.AddScoped<AnnotatePdfProcessor>();
    builder.Services.AddScoped<SubscriptionService>();
    builder.Services.AddScoped<FlattenPdfProcessor>();
    builder.Services.AddScoped<RotatePdfProcessor>();
    builder.Services.AddScoped<WatermarkPdfProcessor>();
    builder.Services.AddScoped<SplitPdfProcessor>();
    builder.Services.AddScoped<NumberPagesPdfProcessor>();
    builder.Services.AddScoped<UnlockPdfProcessor>();
    builder.Services.AddScoped<ProtectPdfProcessor>();
    builder.Services.AddScoped<JpgToPdfProcessor>();
    builder.Services.AddScoped<PptToPdfProcessor>();
    builder.Services.AddScoped<ExcelToPdfProcessor>();
    builder.Services.AddScoped<PdfToJpgProcessor>();
    builder.Services.AddScoped<PdfToExcelProcessor>();
    builder.Services.AddScoped<IDeletePagesProcessor, DeletePagesProcessor>();
    builder.Services.AddScoped<IExtractPagesProcessor, ExtractPagesProcessor>();
    builder.Services.AddScoped<IFileValidationService, FileValidationService>();

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

    builder.Services.AddScoped<IProcessingStrategy>(sp =>
        new FlattenStrategy(
            sp.GetRequiredService<FlattenPdfProcessor>()));

    builder.Services.AddScoped<IProcessingStrategy>(sp =>
        new RotateStrategy(
            sp.GetRequiredService<RotatePdfProcessor>()));

    builder.Services.AddScoped<IProcessingStrategy>(sp =>
        new WatermarkStrategy(
            sp.GetRequiredService<WatermarkPdfProcessor>()));

    builder.Services.AddScoped<IProcessingStrategy>(sp =>
        new SplitStrategy(
            sp.GetRequiredService<SplitPdfProcessor>()));

    builder.Services.AddScoped<IProcessingStrategy>(sp =>
        new NumberPagesStrategy(
            sp.GetRequiredService<NumberPagesPdfProcessor>()));

    builder.Services.AddScoped<IProcessingStrategy>(sp =>
        new UnlockStrategy(
            sp.GetRequiredService<UnlockPdfProcessor>()));

    builder.Services.AddScoped<IProcessingStrategy>(sp =>
        new ProtectStrategy(
            sp.GetRequiredService<ProtectPdfProcessor>()));

    builder.Services.AddScoped<IProcessingStrategy>(sp =>
        new WordToPdfStrategy(
            sp.GetRequiredService<WordToPdfProcessor>()));

    builder.Services.AddScoped<IProcessingStrategy>(sp =>
        new PptToPdfStrategy(
            sp.GetRequiredService<PptToPdfProcessor>()));

    builder.Services.AddScoped<IProcessingStrategy>(sp =>
        new ExcelToPdfStrategy(
            sp.GetRequiredService<ExcelToPdfProcessor>()));

    builder.Services.AddScoped<IProcessingStrategy>(sp =>
        new ExtractPagesStrategy(
            sp.GetRequiredService<IExtractPagesProcessor>()));

    builder.Services.AddScoped<IProcessingStrategy>(sp =>
        new DeletePagesStrategy(
            sp.GetRequiredService<IDeletePagesProcessor>()));

    builder.Services.AddScoped<IProcessingStrategy>(sp =>
    new PdfToolStack.Infrastructure.Strategies.OrganizeStrategy(
        sp.GetRequiredService<OrganizePdfProcessor>()));

    builder.Services.AddScoped<IProcessingStrategy>(sp =>
        new PdfToolStack.Infrastructure.Strategies.SignStrategy(
            sp.GetRequiredService<SignPdfProcessor>()));

    builder.Services.AddScoped<IProcessingStrategy>(sp =>
        new PdfToolStack.Infrastructure.Strategies.EditStrategy(
            sp.GetRequiredService<EditPdfProcessor>()));

    builder.Services.AddScoped<IProcessingStrategy>(sp =>
        new PdfToolStack.Infrastructure.Strategies.AnnotateStrategy(
            sp.GetRequiredService<AnnotatePdfProcessor>()));

    builder.Services.AddScoped<IProcessingStrategy>(sp =>
    new PdfToolStack.Infrastructure.Strategies.JpgToPdfStrategy(
        sp.GetRequiredService<JpgToPdfProcessor>()));

    builder.Services.AddScoped<IProcessingStrategy>(sp =>
    new PdfToolStack.Infrastructure.Strategies.PdfToJpgStrategy(
        sp.GetRequiredService<PdfToJpgProcessor>()));

    builder.Services.AddScoped<IProcessingStrategy>(sp =>
        new PdfToolStack.Infrastructure.Strategies.PdfToExcelStrategy(
            sp.GetRequiredService<PdfToExcelProcessor>()));

    // ── AI Service ────────────────────────────────────────────────────────────
    builder.Services.AddHttpClient();
    builder.Services.AddScoped<AiService>(sp =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var http = sp.GetRequiredService<IHttpClientFactory>()
            .CreateClient();
        var logger = sp.GetRequiredService<ILogger<AiService>>();
        return new AiService(
            http,
            config["Anthropic:ApiKey"] ?? string.Empty,
            config["Anthropic:Model"] ?? "claude-opus-4-6",
            int.TryParse(config["Anthropic:MaxTokens"],
                out var mt) ? mt : 2000,
            logger);
    });

    // ── Factory ───────────────────────────────────────────────────────────
    builder.Services.AddScoped<PdfProcessorFactory>(sp =>
        new PdfProcessorFactory(
            sp.GetServices<IProcessingStrategy>(),
            sp.GetRequiredService<ILogger<PdfProcessorFactory>>()));

    // ── Application Services ──────────────────────────────────────────────
    builder.Services.AddScoped<IPdfService, PdfService>();

    // ── CORS — allow Blazor frontend ──────────────────────────────────────
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("BlazorPolicy", policy =>
        {
            if (builder.Environment.IsDevelopment())
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            }
            else
            {
                policy.WithOrigins(
                        "https://pdftoolstack.com",
                        "https://www.pdftoolstack.com")
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            }
        });
    });

    // ── Controllers + Swagger ─────────────────────────────────────────────
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // ── Background Services ───────────────────────────────────────────────
    if (hasDatabase && hasBlobStorage)
    {
        builder.Services.AddHostedService<JobCleanupService>();
    }

    var app = builder.Build();

    // ── Middleware Pipeline ───────────────────────────────────────────────
    app.UseMiddleware<ErrorHandlingMiddleware>();
    app.UseMiddleware<RateLimitingMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    // ── Security Headers ──────────────────────────────────────────────────
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
        await next();
    });

    app.UseHttpsRedirection();
    app.UseCors("BlazorPolicy");
    app.UseAuthorization();
    app.MapControllers();

    // ── Auto-migrate database on startup ──────────────────────────────────
    if (hasDatabase)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

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
    Log.Fatal(ex, "PdfToolStack API failed to start");
}
finally
{
    Log.CloseAndFlush();
}