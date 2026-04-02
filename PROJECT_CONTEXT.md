# PdfToolStack – Project Context

## 🧠 Overview

PdfToolStack is a SaaS web application that provides AI-powered PDF tools (compress, merge, convert, edit, AI document analysis) with a focus on solving real user problems rather than offering generic utilities.

**Positioning:** "The AI document workspace for small teams"

The goal is to monetize through:
- Subscription (primary — Pro and Teams tiers)
- Ads (secondary, Free tier only)
- API access (future — post $10K MRR)

---

## 🎯 Product Strategy

This project follows a **problem-first approach**, not a tool-first approach.

Instead of:
- "Compress PDF"

We position tools as:
- "Fix PDF too large for email"
- "Convert PDF to editable Word"
- "Merge PDFs for submission"
- "Extract invoice data to Excel"
- "Review contract with AI"

Each tool is mapped to:
- A real-world problem
- A high-intent search query
- A conversion opportunity

**Primary target audience:** SMB professionals who touch documents daily — accountants, paralegals, real estate agents, insurance adjusters, HR coordinators. They process 10–50 PDFs per day and will pay $19–39/month for a tool that also extracts data, summarizes contracts, and answers questions about their documents.

**Secondary target:** Solo freelancers and VA contractors who work in both PDF and Excel. ExcelToolStack.com (also owned) bridges this gap.

---

## 💰 Monetization Model

### Free Tier
- Up to 25MB file size
- All 35+ standard tools
- Files deleted within 1 hour
- No signup required
- Ads enabled
- No AI tools
- No download history

### Pro — $19/month or $150/year
- Unlimited file size
- No ads
- Priority processing
- Download history (30 days)
- All AI tools
- Batch processing (up to 20 files, ZIP output)
- PDF compare / diff viewer
- PDF OCR access

### Teams — $39/seat/month (3–10 seats) — PLANNED
- Everything in Pro
- Shared document workspace
- Admin dashboard
- Invoice + PO billing
- AI data extractor (invoice/form → Excel)
- AI contract reviewer
- Priority support

### API — $99/month — PLANNED (post $10K MRR)
- REST API access, documented, rate-limited
- For developers integrating PDF processing into their own products

---

## ⚙️ Core Workflow

All tools follow this flow:

1. User uploads file
2. System detects issue (file size, scanned doc, etc.)
3. Show options:
   - Paid (fast, optimized)
   - Free (manual or limited)
4. Process file
5. Deliver result
6. Upsell (subscription or upgrade)

---

## 🧰 Core Tools (Current — 33 implemented)

### Compress & Convert
- Fix PDF too large for email (Compress)
- Convert PDF to editable Word
- Extract tables to Excel
- Convert images to PDF
- Word/PPT/Excel to PDF
- PDF to JPG

### Edit & Annotate
- Edit PDF content
- Add annotations
- Fill & sign forms
- Flatten PDF
- Watermark PDF
- Number pages
- Crop PDF

### Organize
- Merge PDFs
- Split PDFs
- Extract pages
- Remove pages
- Rotate pages
- Organize/reorder pages

### Security
- Add password protection
- Remove password
- Redact sensitive data

### Compare ⭐ (Pro only)
- PDF Compare / diff viewer — LCS word-level diff, highlighted PDF report

### AI Tools ⭐ (Pro only — headline feature)
- Chat with PDF
- AI Summarizer
- AI Questions Generator
- AI PDF Assist

### Batch Processing ⭐ (Pro only)
- Apply any tool to up to 20 PDFs at once
- ZIP output with error log

---

## 🚧 Planned Features

### High priority (next sprint)
- **Intent-based URL slugs** — `/compress-for-email`, `/extract-invoice-data`, `/sign-contract` with proper meta descriptions
- **AI data extractor** — invoice/form → structured JSON → Excel/CSV export (flagship feature)
- **AI contract reviewer** — highlight risky clauses, key dates, obligations

### Medium priority
- **Teams tier** — shared workspace, admin dashboard, seat management
- **Cloud integrations** — Google Drive, Dropbox
- **PDF Repair** — fix corrupted files

### Future (post $10K MRR)
- **API tier** — REST API for developers, $99/month
- **ExcelToolStack** — companion product at exceltoolstack.com

---

## 🧱 Tech Stack

### Frontend
- Blazor WebAssembly (.NET 9)

### Backend
- ASP.NET Core API (.NET 9 Linux)

### Storage
- Azure Blob Storage

### Database
- Azure SQL (EF Core, Code First migrations)

### Payments
- Stripe (PaymentController + SubscriptionController implemented)
- Price IDs stored in Azure App Service environment variables (never hardcoded)

### Auth
- OAuth (GitHub)

### Hosting
- Azure App Service (B1) + Azure Static Web Apps
- DNS via Namecheap (ALIAS + CNAME)
- GitHub Actions CI/CD

---

## 📁 Project Structure

```
PdfToolStack.Web            → Blazor WASM frontend
PdfToolStack.API            → ASP.NET Core REST API
PdfToolStack.Application    → Business logic (Services, Strategies, DTOs, Factories)
PdfToolStack.Domain         → Core entities, enums, interfaces
PdfToolStack.Infrastructure → Processors, Strategies, Repositories, Storage, Migrations
```

### Key patterns in place
- **Strategy pattern** — `IProcessingStrategy` implemented across Application + Infrastructure
- **Factory pattern** — `PdfProcessorFactory` dispatches by `ToolType`
- **Background service** — `JobCleanupService` (auto-deletes old blobs)
- **Rate limiting** — `RateLimitingMiddleware`
- **Error handling** — `ErrorHandlingMiddleware`
- **Logging** — `ILogger<T>` throughout
- **Security headers** — middleware registered
- **CORS** — locked to pdftoolstack.com

### Critical architecture rule
`Application` references `Domain` only. Infrastructure types (processors, iTextSharp, etc.) must NOT be used in `Application` layer — this creates a circular dependency. The four strategies for Organize, Sign, Edit, and Annotate live in `PdfToolStack.Infrastructure/Strategies/` for this reason.

### Stripe config
- Price IDs served from `/api/subscription/plans` endpoint — never hardcoded in Blazor pages
- Azure env vars use double-underscore notation: `Stripe__ProMonthlyPriceIdV2`, `Stripe__ProYearlyPriceIdV2`
- Legacy price IDs (`ProMonthlyPriceId`, `ProYearlyPriceId`) kept in config for existing subscribers
- `StripeOptions` and `FileLimit` class both live in `PdfToolStack.API/Configuration/StripeOptions.cs`

### JS interop
- `downloadFile(fileName, base64Data)` exists in `wwwroot/index.html` — use for ALL file downloads
- Supports: pdf, docx, zip (mime type resolved from extension)

---

## ✅ Completed Work (March 2026)

### Architecture cleanup
- Refactored 4 TODO endpoints (Organize, Sign, Edit, Annotate) to Strategy/Factory pattern
- Four new strategies in `PdfToolStack.Infrastructure/Strategies/`: `OrganizeStrategy`, `SignStrategy`, `EditStrategy`, `AnnotateStrategy`
- `ProcessRequest` extended with: `PageOperations`, `SignatureBytes` + placement fields, `Annotations`, `Highlights`, `PageOperationDto`
- `ParseColor` moved from controller into the strategies that need it

### Monetization
- Pro raised $9.99 → $19/month, $79 → $150/year
- `/api/subscription/plans` endpoint added — Blazor pricing page fetches price IDs from server
- `Pricing.razor` rewritten — no hardcoded price IDs, updated feature list, privacy FAQ added
- `StripeOptions` updated with V2 price ID properties + `SectionName` constant restored
- `FileLimit` class restored in `StripeOptions.cs`

### My Documents dashboard
- `Account.razor` rewritten — live subscription status, renewal date, manage billing
- Download history table with file name, tool, size, timestamp
- Pro upsell nudge for free users
- `TrackDownloadAsync` wired into `PdfController.Process` (fire-and-forget, auth-gated)

### Batch processing
- `POST api/pdf/batch` — up to 20 files, ZIP output, per-file error log
- `BatchProcess.razor` at `/batch` — tool selector, per-file status indicators, Pro gate
- `ApiService.BatchProcessAsync` added
- Added to NavMenu desktop + mobile

### PDF Compare
- `ComparePdfProcessor` — LCS word diff via PdfPig extraction + iTextSharp report generation
- `POST api/pdf/compare` — returns highlighted PDF diff report
- `ComparePdf.razor` at `/compare-pdf` — two-panel upload, stats summary, Pro gate
- `ApiService.ComparePdfsAsync` added
- `ToolType.ComparePdf = 33` added to enum
- Added to NavMenu search list

---

## 🚨 Important Decisions (DO NOT CHANGE)

- Problem-first UX is required
- Free tier must always exist
- Paid option must always be shown alongside free option
- No hard paywalls without alternative path
- AI tools are the headline — always position above utility tools
- Privacy-first: files deleted within 1 hour, no training on user documents
- Stripe price IDs must never be hardcoded in frontend
- `Application` layer must never reference `Infrastructure` — circular dependency
- Use existing `downloadFile(fileName, base64Data)` JS interop for all downloads

---

## 🎯 SEO Strategy

**Next to build:**

| URL slug | Target query | Tool |
|---|---|---|
| `/compress-for-email` | "compress PDF for email" | Compress |
| `/extract-invoice-data` | "extract data from PDF invoice" | AI Extractor |
| `/sign-contract` | "sign PDF contract online" | Sign |
| `/compare-contracts` | "compare two PDF documents" | Compare |
| `/merge-pdfs-for-tax` | "merge PDFs for tax filing" | Merge |

Each page = problem-first copy + proper `<PageTitle>` + meta description + embedded tool.

---

## 🏆 Competitive Edge

1. **PDF ↔ Excel bridge** — own pdftoolstack.com + exceltoolstack.com. No competitor at this price has both.
2. **AI-first at SMB price** — Adobe is $25+/month enterprise. Smallpdf has no meaningful AI.
3. **Privacy-first** — "We don't train on your documents." Wedge for legal, healthcare, finance.
4. **Batch at $19/mo** — competitors charge significantly more or don't offer it at this tier.

---

## 🧪 If working with AI (Claude context instructions)

- Prioritize conversion over features
- Optimize UX flows before adding new tools
- Clean Architecture — always place code in the correct layer:
  - New processors → `PdfToolStack.Infrastructure/Processors/`
  - Complex strategies (need Infrastructure types) → `PdfToolStack.Infrastructure/Strategies/`
  - Simple strategies (Domain/Application types only) → `PdfToolStack.Application/Strategies/`
  - New ToolType values → `PdfToolStack.Domain/Enums/ToolType.cs`
- Register new processors in `Program.cs` as scoped services
- Register strategies in `Program.cs` using `sp.GetRequiredService<>()` pattern
- All secrets → Azure App Service env vars with double-underscore notation
- Never add `Infrastructure` as a reference in `Application.csproj`
- Use `downloadFile(fileName, base64Data)` for all JS download interop
- Suggest monetization and positioning improvements, not just technical solutions

---

## 💬 Continuation prompts for next session

Paste the intro below + attach this file + attach PdfToolStack.zip to resume:

> "I'm Chrystal, a full-stack .NET developer working on PdfToolStack.com — an AI-powered PDF SaaS built on ASP.NET Core + Blazor WebAssembly with Clean Architecture, deployed on Azure. I've uploaded my project context file. Please read it fully before we start. I want to continue building out the product."

**To build intent-based SEO pages:**
> "Let's build the intent-based URL slug pages for PdfToolStack. Start with /compress-for-email — problem-first copy, proper PageTitle and meta description, embedded compress tool."

**To build the AI data extractor:**
> "Let's build the AI data extractor for PdfToolStack at /extract-invoice-data. User uploads a PDF invoice or form. Claude extracts structured fields (vendor, amount, date, line items) as JSON. User downloads result as Excel or CSV. Create the Blazor page and API endpoint."

**To build the AI contract reviewer:**
> "Let's build the AI contract reviewer for PdfToolStack at /review-contract. User uploads a PDF contract. Claude returns: risky clauses, key dates, obligations, missing elements. Output is a summary panel + downloadable flagged PDF report."

**To build the Teams tier:**
> "Let's build the Teams tier for PdfToolStack — $39/seat/month, 3–10 seats. Start with the DB schema for seat management, then the Stripe product setup."

**To set up the launch:**
> "Help me write a Show HN post for PdfToolStack and a cold outreach email targeting small accounting firms. Lead with batch processing and PDF compare as differentiators over Smallpdf."

---

*Last updated: March 2026*
