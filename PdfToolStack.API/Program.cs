using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using PdfToolStack.API.Configuration;
using PdfToolStack.API.Middleware;
using PdfToolStack.API.Services;
using PdfToolStack.Application.Factories;
using PdfToolStack.Application.Interfaces;
using PdfToolStack.Application.Services;
using PdfToolStack.Application.Strategies;
using PdfToolStack.Domain.Interfaces;
using PdfToolStack.Infrastructure.Auth;
using PdfToolStack.Infrastructure.Configuration;
using PdfToolStack.Infrastructure.Data;
using PdfToolStack.Infrastructure.Processors;
using PdfToolStack.Infrastructure.Repositories;
using PdfToolStack.Infrastructure.Services;
using PdfToolStack.Infrastructure.Services.Ocr;
using PdfToolStack.Infrastructure.Storage;
using Serilog;
using Syncfusion.Blazor;

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

    builder.Services.Configure<GoogleVisionOptions>(
        builder.Configuration.GetSection(GoogleVisionOptions.SectionName));

    builder.Services.AddScoped<IFeatureAccessService, FeatureAccessService>();

    // ── Multipart upload limit — must cover 500 MB batch/merge endpoints ──
    builder.Services.Configure<FormOptions>(options =>
    {
        options.MultipartBodyLengthLimit = 524288000; // 500 MB
    });

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
    else
    {
        builder.Services.AddScoped<IBlobStorageService, MissingBlobStorageService>();
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
    builder.Services.AddScoped<FlattenPdfProcessor>();
    builder.Services.AddScoped<RotatePdfProcessor>();
    builder.Services.AddScoped<WatermarkPdfProcessor>();
    builder.Services.AddScoped<PdfToolStack.Domain.Interfaces.IWatermarkProcessor>(sp => sp.GetRequiredService<WatermarkPdfProcessor>());
    builder.Services.AddScoped<SplitPdfProcessor>();
    builder.Services.AddScoped<NumberPagesPdfProcessor>();
    builder.Services.AddScoped<UnlockPdfProcessor>();
    builder.Services.AddScoped<ProtectPdfProcessor>();
    builder.Services.AddScoped<JpgToPdfProcessor>();
    builder.Services.AddScoped<PptToPdfProcessor>();
    builder.Services.AddScoped<ExcelToPdfProcessor>();
    builder.Services.AddScoped<PdfToJpgProcessor>();
    builder.Services.AddScoped<PdfToExcelProcessor>();
    builder.Services.AddScoped<CropPdfProcessor>();
    builder.Services.AddScoped(_ =>
        new PdfOcrProcessor(Path.Combine(AppContext.BaseDirectory, "tessdata")));
    builder.Services.AddScoped<TesseractOcrTextProvider>();
    builder.Services.AddScoped<GoogleVisionOcrTextProvider>();
    builder.Services.AddScoped<SmartOcrTextService>();

    if (hasDatabase)
    {
        builder.Services.AddScoped<SubscriptionService>();
        builder.Services.AddScoped<IAiUsageService, AiUsageService>();
    }
    else
    {
        builder.Services.AddScoped<IAiUsageService, NullAiUsageService>();
    }

    if (hasDatabase)
    {
        builder.Services.AddScoped<IApiKeyService, ApiKeyService>();
    }

    if (hasDatabase)
    {
        builder.Services.AddScoped<IReferralService,
            ReferralService>();
    }

    builder.Services.AddScoped<IDeletePagesProcessor, DeletePagesProcessor>();
    builder.Services.AddScoped<IExtractPagesProcessor, ExtractPagesProcessor>();
    builder.Services.AddScoped<IFileValidationService, FileValidationService>();

    builder.Services.AddMemoryCache();
    builder.Services.AddScoped<ICloudAuthStateStore, InMemoryCloudAuthStateStore>();

    // ── Team Service ─────────────────────────────────────────────────────
    if (hasDatabase)
    {
        builder.Services.AddScoped<ITeamService, TeamService>();
    }

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

    builder.Services.AddScoped<IProcessingStrategy>(sp =>
    new PdfToolStack.Infrastructure.Strategies.CropStrategy(
        sp.GetRequiredService<CropPdfProcessor>()));

    // -- Email service ---------------------------------------------------------
    builder.Services.Configure<EmailOptions>(
        builder.Configuration.GetSection(EmailOptions.SectionName));
    builder.Services.AddHttpClient<IEmailService, EmailService>();

    // GDPR deletion services ---------------------------------------------------
    builder.Services.AddHttpClient<IAuth0ManagementService,
        Auth0ManagementService>();

    if (hasDatabase)
    {
        builder.Services.AddScoped<IUserDeletionService,
            UserDeletionService>();
    }

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
            config["Anthropic:ApiKey"] ?? throw new InvalidOperationException("Missing Anthropic:ApiKey"),
            config["Anthropic:Model"] ?? "claude-opus-4-6",
            int.TryParse(config["Anthropic:MaxTokens"],
                out var mt) ? mt : 2000,
            logger,
            sp.GetRequiredService<SmartOcrTextService>());
    });

    // ── Cloud Service ────────────────────────────────────────────────────────────
    builder.Services.Configure<OneDriveOptions>(
    builder.Configuration.GetSection(OneDriveOptions.SectionName));

    builder.Services.AddHttpClient("CloudProxy", client => {
        client.DefaultRequestHeaders.Add("User-Agent", "PdfToolStack/1.0");
        client.Timeout = TimeSpan.FromSeconds(35);
    });

    builder.Services.AddHttpClient("OneDrive", client => {
        client.Timeout = TimeSpan.FromSeconds(35);
    });

    builder.Services.AddScoped<ICloudStorageService, OneDriveService>();

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
                policy.WithOrigins(
                        "https://localhost:7025",
                        "http://localhost:5049")
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
    builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // ── Health Checks ─────────────────────────────────────────────────────
    var healthChecks = builder.Services.AddHealthChecks();
    if (hasDatabase)
    {
        healthChecks.AddDbContextCheck<AppDbContext>("database");
    }

    // ── Background Services ───────────────────────────────────────────────
    if (hasDatabase && hasBlobStorage)
    {
        builder.Services.AddHostedService<JobCleanupService>();
    }

    // ── Application Insights — no-op if connection string is absent ───────
    var appInsightsConnectionString =
    builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

    if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
    {
        builder.Services.AddApplicationInsightsTelemetry(options =>
        {
            options.ConnectionString = appInsightsConnectionString;
        });
    }

    // ── Authentication — Auth0 JWT Bearer ────────────────────────────────
    var auth0Domain = builder.Configuration["Auth0:Domain"] ?? "";
    var auth0Audience = builder.Configuration["Auth0:Audience"] ?? "";

    // In production, a missing audience allows any JWT to pass — fail fast.
    if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(auth0Audience))
    {
        throw new InvalidOperationException(
            "Auth0:Audience must be configured in production. " +
            "Set Auth0__Audience in Azure App Settings.");
    }

    builder.Services
        .AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            if (!string.IsNullOrWhiteSpace(auth0Domain))
                options.Authority = $"https://{auth0Domain}/";

            if (!string.IsNullOrWhiteSpace(auth0Audience))
            {
                options.Audience = auth0Audience;
            }
            else
            {
                // Development only: audience not configured, skip validation.
                options.TokenValidationParameters =
                    new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                    {
                        ValidateAudience = false,
                        NameClaimType =
                            System.Security.Claims.ClaimTypes.NameIdentifier
                    };
            }
        });

    var app = builder.Build();

    // ── Middleware Pipeline ───────────────────────────────────────────────
    app.UseMiddleware<ErrorHandlingMiddleware>();
    app.UseMiddleware<AuditLoggingMiddleware>();
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
        context.Response.Headers.Append("Permissions-Policy",
            "camera=(), microphone=(), geolocation=(), payment=()");

        // connect-src: include localhost only in development
        var connectSrcLocalhost = app.Environment.IsDevelopment()
            ? "https://localhost:7100 "
            : "";

        var auth0ConnectSrc = string.IsNullOrWhiteSpace(auth0Domain)
            ? ""
            : $"https://{auth0Domain} ";

        // CSP — clarity domains are placed inside the correct directives.
        // script-src includes https://www.clarity.ms for the Clarity snippet.
        // connect-src includes https://*.clarity.ms for Clarity beacons.
        context.Response.Headers.Append("Content-Security-Policy",
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline' 'unsafe-eval' " +
                "https://js.stripe.com " +
                "https://cdn.jsdelivr.net " +
                "https://cdnjs.cloudflare.com " +
                "https://www.clarity.ms; " +
            "style-src 'self' 'unsafe-inline' " +
                "https://fonts.googleapis.com; " +
            "font-src 'self' " +
                "https://fonts.gstatic.com; " +
            "img-src 'self' data: blob: " +
                "https://*.stripe.com; " +
            "frame-src 'self' " +
                "https://js.stripe.com " +
                "https://hooks.stripe.com; " +
            "connect-src 'self' " +
                connectSrcLocalhost +
                "https://pdftoolstack-api-grcxhqergtgcd0g7.westus2-01.azurewebsites.net " +
                auth0ConnectSrc +
                "https://api.anthropic.com " +
                "https://*.clarity.ms; " +
            "object-src 'none'; " +
            "base-uri 'self';"
        );

        // HSTS — only in production
        if (!app.Environment.IsDevelopment())
        {
            context.Response.Headers.Append(
                "Strict-Transport-Security",
                "max-age=31536000; includeSubDomains");
        }

        await next();
    });

    app.UseHttpsRedirection();
    app.UseCors("BlazorPolicy");
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    // ── /healthz — suitable for Azure App Service Health Check probe ──────
    app.MapHealthChecks("/healthz");

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
