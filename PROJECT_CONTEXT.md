# PdfToolStack — Project Context

---

## What This Is

A free PDF tools website built on **ASP.NET Core + Blazor WebAssembly**, targeting the same market as SmallPDF and ILovePDF. Differentiators: AI-first features (Chat with PDF, Summarizer, Questions Generator) and privacy-focused positioning.

- **Repo:** https://github.com/Cmpinkney/PdfToolStack
- **Stack:** .NET 9, ASP.NET Core, Blazor WASM, Entity Framework Core, SQL Server, Azure Blob Storage, Stripe, Serilog, iTextSharp

---

## Solution Structure

```
PdfToolStack.sln
├── PdfToolStack.Domain          # Entities, enums, interfaces — no dependencies
├── PdfToolStack.Application     # Services, strategies, DTOs, factory — depends on Domain
├── PdfToolStack.Infrastructure  # EF Core, processors, blob storage — depends on Domain
├── PdfToolStack.API             # ASP.NET Core Web API — depends on all layers
└── PdfToolStack.Web             # Blazor WASM frontend — calls API via HttpClient
```

---

## Architecture

Clean Architecture with Strategy + Factory pattern for PDF operations.

### Request flow (every tool)
```
Blazor (ApiService) 
  → PdfController 
  → BuildRequestAsync() helper 
  → IPdfService.ProcessAsync() 
  → PdfProcessorFactory.GetStrategy(toolType) 
  → IProcessingStrategy.ExecuteAsync() 
  → IPdfProcessor.ProcessAsync() 
  → ProcessingResult 
  → ProcessResponse (with OutputBytes)
  → File() response
```

### Key types

| Type | Location | Purpose |
|---|---|---|
| `ToolType` | Domain/Enums | Enum for all 32 tool types |
| `PdfJob` | Domain/Entities | DB entity — tracks every job |
| `ProcessRequest` | Application/DTOs | Input to service layer |
| `ProcessResponse` | Application/DTOs | Output from service layer — includes `OutputBytes` |
| `ProcessingResult` | Domain/Entities | Output from processor — `Success()` / `Failure()` factory methods |
| `IPdfProcessor` | Domain/Interfaces | Base processor interface — `ProcessAsync(byte[], CancellationToken)` |
| `IProcessingStrategy` | Application/Strategies | Strategy interface — `ToolType` + `ExecuteAsync(ProcessRequest)` |
| `PdfProcessorFactory` | Application/Factories | Resolves strategy by `ToolType` from DI-registered `IEnumerable<IProcessingStrategy>` |
| `PdfService` | Application/Services | Orchestrates job creation → strategy execution → job update |
| `AppDbContext` | Infrastructure/Data | EF Core context — `PdfJobs`, `UserSubscriptions`, `DownloadHistory` |

---

## Registered Strategies (Program.cs)

All registered as `IProcessingStrategy` via factory lambdas in `PdfToolStack.API/Program.cs`.

| Strategy | ToolType |
|---|---|
| `CompressStrategy` | CompressPdf |
| `RedactStrategy` | RedactPdf |
| `MergeStrategy` | MergePdf |
| `PdfToWordStrategy` | PdfToWord |
| `FillFormStrategy` | FillPdfForm |
| `FlattenStrategy` | FlattenPdf |
| `RotateStrategy` | RotatePdf |
| `WatermarkStrategy` | WatermarkPdf |
| `SplitStrategy` | SplitPdf |
| `NumberPagesStrategy` | NumberPages |
| `UnlockStrategy` | UnlockPdf |
| `ProtectStrategy` | ProtectPdf |
| `WordToPdfStrategy` | WordToPdf |
| `PptToPdfStrategy` | PptToPdf |
| `ExcelToPdfStrategy` | ExcelToPdf |
| `ExtractPagesStrategy` | ExtractPages |
| `DeletePagesStrategy` | DeletePages |

### Special interfaces (Domain/Interfaces)
- `IDeletePagesProcessor` — extends `IPdfProcessor` with `ProcessAsync(byte[], IEnumerable<int>)`
- `IExtractPagesProcessor` — extends `IPdfProcessor` with `ProcessAsync(byte[], IEnumerable<int>)`

---

## ProcessRequest Extended Properties

Beyond the base `FileBytes / FileName / FileSizeBytes / ToolType`, these extra fields carry tool-specific params:

```csharp
List<int>?   PageNumbers          // delete-pages, extract-pages
int          Rotation             // rotate (default 90)
string       WatermarkText        // watermark (default "CONFIDENTIAL")
float        WatermarkOpacity     // watermark (default 0.3)
float        WatermarkFontSize    // watermark (default 48)
string       PageNumberPosition   // number-pages (default "bottom-center")
int          PageNumberStart      // number-pages (default 1)
string?      Password             // unlock
string       UserPassword         // protect
string       OwnerPassword        // protect
bool         AllowPrinting        // protect
bool         AllowCopying         // protect
int?         SplitFromPage        // split range
int?         SplitToPage          // split range
```

---

## Controller Helpers (PdfController)

Two private helpers eliminate boilerplate:

```csharp
// Reads file into ProcessRequest
BuildRequestAsync(IFormFile, ToolType, CancellationToken)

// Parses "1,2,3" → List<int> with validation
TryParsePageNumbers(string, out List<int>, out string error)
```

---

## Endpoints Still Using Direct Processor Instantiation (TODOs)

These three have complex multi-param signatures not yet modelled in `ProcessRequest`:

| Endpoint | Reason | TODO |
|---|---|---|
| `POST /api/pdf/organize` | Takes `List<PageOperation>` | Add `OperationsJson` to `ProcessRequest` |
| `POST /api/pdf/sign` | Takes second `IFormFile` (signature image) + position floats | Add `SignatureBytes` + position to `ProcessRequest` |
| `POST /api/pdf/edit` | Takes `List<PdfAnnotation>` | Add `AnnotationsJson` to `ProcessRequest` |
| `POST /api/pdf/annotate` | Takes `List<PdfHighlight>` | Add `HighlightsJson` to `ProcessRequest` |

---

## DTOs Location

All DTOs live in `PdfToolStack.Application/DTOs/`:
- `ProcessRequest.cs`
- `ProcessResponse.cs`
- `JobStatusResponse.cs`
- `DetectFieldsResponse.cs`
- `SubscriptionDtos.cs`
- `AnnotationDtos.cs` — `PdfAnnotationDto`, `PdfHighlightDto`, `PointDto`

---

## Infrastructure

| Service | Implementation |
|---|---|
| File storage | Azure Blob Storage (`AzureBlobStorageService`) |
| Database | SQL Server via EF Core (`AppDbContext`) |
| Payments | Stripe (`PaymentController`, `SubscriptionController`) |
| Logging | Serilog (structured, console + config) |
| Rate limiting | Custom `RateLimitingMiddleware` |
| Error handling | Custom `ErrorHandlingMiddleware` (first in pipeline) |

---

## Blazor Frontend (PdfToolStack.Web)

- Blazor WASM, calls API via `ApiService`
- Auth via `LoginDisplay` / `RedirectToLogin`
- Components: `FileUpload`, `ToastContainer`, `ThemeSwitcher`, `AdSlot`, `LargeFileUpgrade`
- Pages: one `.razor` per tool + Account, Pricing, About, Contact, Privacy, Terms

---

## Known Issues / Next Up

- [ ] `SplitPdf` all-pages returns a `.zip` — Blazor frontend needs to handle zip download
- [ ] Rotate strategy ignores `Rotation` and `PageNumbers` from `ProcessRequest` — strategy calls standard `ProcessAsync` which defaults to 90°. Wire up a `IRotatePdfProcessor` interface (same pattern as delete/extract)
- [ ] CORS origin `"https://yoursite.com"` is a placeholder — replace with real domain before deploy
- [ ] `AdSlot.razor` exists but ad placements not configured — gate behind feature flag before launch
- [ ] Organize / Sign / Edit / Annotate endpoints still use `new XxxProcessor()` directly (see TODOs above)
- [ ] No auth on API endpoints — all processing is anonymous for now

---

## Deployment Target

- API: Azure App Service
- Web: Azure Static Web Apps (or same App Service)
- DB: Azure SQL
- Storage: Azure Blob Storage
- CI/CD: Azure DevOps (already in use)

---

## Session Log

| Date | What was done |
|---|---|
| 2026-03-25 | Initial architecture review — identified Strategy pattern inconsistency |
| 2026-03-25 | Replaced `Console.WriteLine` in `PdfProcessorFactory` with `ILogger` |
| 2026-03-25 | Created 12 missing strategy classes, registered all in `Program.cs` |
| 2026-03-25 | Added `OutputBytes` to `ProcessResponse`, wired through `PdfService` |
| 2026-03-25 | Refactored controller — added `BuildRequestAsync` + `TryParsePageNumbers` helpers |
| 2026-03-25 | Moved `PdfAnnotationDto`, `PdfHighlightDto`, `PointDto` to `Application/DTOs` |
| 2026-03-25 | Fixed Split endpoint — returns zip for all-pages, single PDF for range |
| 2026-03-25 | Added `IDeletePagesProcessor` + `IExtractPagesProcessor` interfaces, wired `PageNumbers` through strategies |
