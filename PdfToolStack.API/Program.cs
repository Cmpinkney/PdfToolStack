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
        b => b.MigrationsAssembly("PdfToolStack.Infrastructure")));

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
    builder.Services.AddScoped<IDeletePagesProcessor, DeletePagesProcessor>();
    builder.Services.AddScoped<IExtractPagesProcessor, ExtractPagesProcessor>();

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
    new FlattenStrategy(sp.GetRequiredService<FlattenPdfProcessor>()));

    builder.Services.AddScoped<IProcessingStrategy>(sp =>
        new RotateStrategy(sp.GetRequiredService<RotatePdfProcessor>()));

    builder.Services.AddScoped<IProcessingStrategy>(sp =>
        new WatermarkStrategy(sp.GetRequiredService<WatermarkPdfProcessor>()));

    builder.Services.AddScoped<IProcessingStrategy>(sp =>
        new SplitStrategy(sp.GetRequiredService<SplitPdfProcessor>()));

    builder.Services.AddScoped<IProcessingStrategy>(sp =>
        new NumberPagesStrategy(sp.GetRequiredService<NumberPagesPdfProcessor>()));

    builder.Services.AddScoped<IProcessingStrategy>(sp =>
        new UnlockStrategy(sp.GetRequiredService<UnlockPdfProcessor>()));

    builder.Services.AddScoped<IProcessingStrategy>(sp =>
        new ProtectStrategy(sp.GetRequiredService<ProtectPdfProcessor>()));

    builder.Services.AddScoped<IProcessingStrategy>(sp =>
        new WordToPdfStrategy(sp.GetRequiredService<WordToPdfProcessor>()));

    builder.Services.AddScoped<IProcessingStrategy>(sp =>
        new PptToPdfStrategy(sp.GetRequiredService<PptToPdfProcessor>()));

    builder.Services.AddScoped<IProcessingStrategy>(sp =>
        new ExcelToPdfStrategy(sp.GetRequiredService<ExcelToPdfProcessor>()));

    builder.Services.AddScoped<IProcessingStrategy>(sp =>
    new ExtractPagesStrategy(
        sp.GetRequiredService<IExtractPagesProcessor>()));

    builder.Services.AddScoped<IProcessingStrategy>(sp =>
    new DeletePagesStrategy(
        sp.GetRequiredService<IDeletePagesProcessor>()));


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
            policy.WithOrigins(
                    "https://pdftoolstack.com",
                    "https://www.pdftoolstack.com",
                    "https://localhost:7025",
                    "http://localhost:7025")
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
    });

    // ── Controllers + Swagger ─────────────────────────────────────────────
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // ── Background Services ───────────────────────────────────────────────
    builder.Services.AddHostedService<JobCleanupService>();

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

    // ── Security Headers ──────────────────────────────────────────────────
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Append(
            "X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append(
            "X-Frame-Options", "DENY");
        context.Response.Headers.Append(
            "Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.Append(
            "X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Append(
            "Permissions-Policy",
            "camera=(), microphone=(), geolocation=()");
        await next();
    });

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
    Log.Fatal(ex, "PdfToolStack API failed to start");
}
finally
{
    Log.CloseAndFlush();
}
