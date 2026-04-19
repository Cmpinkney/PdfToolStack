# PdfToolStack – Project Context

## 🧠 Overview

PdfToolStack is a SaaS web application that provides AI-powered PDF tools (compress, merge, convert, edit, AI document analysis) with a focus on solving real user problems rather than offering generic utilities.

**Positioning:** "The AI document workspace for small teams"

The goal is to monetize through:
- Subscription (primary — Pro and Teams tiers)
- API access (Developer tier — live)
- Referral program (live — 1 free month per conversion)

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

**Primary target audience:** SMB professionals who touch documents daily — accountants, paralegals, real estate agents, insurance adjusters, HR coordinators. They process 10–50 PDFs per day and will pay $12–39/month for a tool that also extracts data, summarizes contracts, and answers questions about their documents.

**Secondary target:** Solo freelancers and VA contractors who work in both PDF and Excel. ExcelToolStack.com (also owned) bridges this gap.

---

## 💰 Monetization Model

### Free Tier
- Up to 25MB file size
- All 35+ standard tools
- Files deleted within 1 hour
- No signup required
- No AI tools
- No download history

### Pro — $19/month or $152/year
- Unlimited file size (500MB)
- No ads
- Priority processing
- Download history (30 days)
- All AI tools (200 uses/month)
- Batch processing (up to 20 files, ZIP output)
- PDF compare / diff viewer
- PDF OCR access
- Cloud storage (Google Drive, OneDrive, Dropbox)
- Cancel anytime

### Teams — $29/month (5 seats included, $6/mo per additional seat)
- Everything in Pro
- 5 seats included
- 500 AI uses/month (shared)
- Shared team workspace
- Admin usage dashboard
- Invoice & PO billing
- Priority support
- Onboarding call included

### Developer API — $49/month (planned pricing)
- 1,000 API calls/month
- REST API access via API key
- Key management at /account/api-keys
- Per-key usage tracking and monthly reset
- Keys prefixed `pts_live_`

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
6. Upsell (subscription or upgrade — contextual modal fires after 3 completions)

---

## 🧰 Core Tools (Current — 35+ implemented)

### Compress & Convert
- Fix PDF too large for email (Compress)
- Convert PDF to editable Word
- Extract tables to Excel
- Convert images to PDF
- Word/PPT/Excel to PDF
- PDF to JPG
- PDF to Excel
- JPG to PDF

### Edit & Annotate
- Edit PDF content
- Add annotations (PDF Annotator)
- Fill & sign forms
- Sign PDF (electronic signature — draw or type)
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
- AI Contract Reviewer (`/review-contract`)
- AI Invoice Data Extractor (`/extract-invoice-data`)
- AI Rewrite
- Translate PDF

### Batch Processing ⭐ (Pro only)
- Apply any tool to up to 20 PDFs at once
- ZIP output with error log

---

## 🏗️ Architecture

### Tech Stack

**Frontend:** Blazor WebAssembly (.NET 9)
**Backend:** ASP.NET Core API (.NET 9)
**Storage:** Azure Blob Storage (West US 2)
**Database:** Azure SQL (EF Core, Code First migrations)
**Payments:** Stripe
**Auth:** Auth0 (Google, GitHub, Microsoft, Facebook, LinkedIn)
**Hosting:** Azure App Service + Azure Static Web Apps
**DNS:** Namecheap (ALIAS + CNAME)
**CI/CD:** GitHub Actions
**Analytics:** Microsoft Clarity (heatmaps + session recordings)
**Logging:** Serilog → structured audit logs with `[AUDIT]` prefix
**Email:** Resend

### Local Ports
- API: `https://localhost:7100`
- Web: `https://localhost:7025`

### Project Structure

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
- **Rate limiting** — `RateLimitingMiddleware` (per-user when authenticated, per-IP when anonymous, OPTIONS preflights never counted)
- **Audit logging** — `AuditLoggingMiddleware` logs every tool use with `[AUDIT]` prefix
- **Error handling** — `ErrorHandlingMiddleware`
- **Logging** — Serilog throughout
- **Security headers** — CSP, HSTS, X-Frame-Options, X-Content-Type-Options, Referrer-Policy
- **CORS** — locked to pdftoolstack.com in production, localhost in development

### Critical architecture rule
`Application` references `Domain` only. Infrastructure types (processors, iTextSharp, etc.) must NOT be used in `Application` layer — this creates a circular dependency. The four strategies for Organize, Sign, Edit, and Annotate live in `PdfToolStack.Infrastructure/Strategies/` for this reason.

### Stripe config
- Price IDs served from `/api/subscription/plans` endpoint — never hardcoded in Blazor pages
- Azure env vars use double-underscore notation: `Stripe__ProMonthlyPriceIdV2`, `Stripe__ProYearlyPriceIdV2`, `Stripe__TeamsMonthlyPriceId`
- Legacy price IDs (`ProMonthlyPriceId`, `ProYearlyPriceId`) kept in config for existing subscribers
- `StripeOptions` and `FileLimit` class both live in `PdfToolStack.API/Configuration/StripeOptions.cs`

### JS interop
- `downloadFile(fileName, base64Data)` exists in `wwwroot/index.html` — use for ALL file downloads
- Supports: pdf, docx, zip (mime type resolved from extension)
- `scrollToTop()` — smooth scroll to top of page
- `signPdfGetLastSig()` — returns last drawn signature data URL
- `signPdfGetCanvasInternalHeight()` — returns overlay canvas internal pixel height
- `signPdfRenderPage(base64, pageNum)` — renders PDF page onto sign canvas
- `signPdfInitDrag(dotNetRef)` — initializes drag-to-place signature overlay

### Rate limiting config
- Anonymous (IP-based): 20 PDF requests/hour, 5 AI requests/hour
- Authenticated (user-based): 200 PDF requests/hour, 50 AI requests/hour
- Dev environment: 500/100 (never hit during testing)
- OPTIONS preflights: never counted

---

## ✅ Completed Work

### March 2026
- Refactored 4 TODO endpoints (Organize, Sign, Edit, Annotate) to Strategy/Factory pattern
- My Documents dashboard in Account.razor
- Batch processing (`/batch`)
- PDF Compare (`/compare-pdf`)
- AI Contract Reviewer (`/review-contract`)
- AI Invoice Data Extractor (`/extract-invoice-data`)
- JpgToPdf, PdfToJpg, PdfToExcel processors
- Security hardening (rate limiting, CSP/HSTS, magic byte validation)
- SEO meta descriptions for all tool pages
- New logo, favicons, profile dropdown, theme switcher
- Footer with social icons
- Cloud storage: Google Drive, Dropbox (working), OneDrive (token_failed — pending fix)
- Pricing page with Teams tier UI

### April 2026
- **GDPR compliance:** Full data deletion flow — SQL, Azure blobs, Stripe cancel, Auth0 account delete
- **Cookie consent:** Banner updated (no decline button, only essential cookies)
- **Privacy policy:** Rewritten — accurate, no false ad/analytics claims, GDPR rights, data residency disclosure
- **Content Security Policy:** Header added covering Stripe, Auth0, Anthropic, Azure, Clarity, CDNs
- **Per-user rate limiting:** Authenticated users tracked by userId, anonymous by IP, OPTIONS never counted
- **Audit logging:** `AuditLoggingMiddleware` — logs Tool, UserId, FileSize, Duration, StatusCode
- **Security/trust page:** `/security`
- **Account deletion page:** `/account/delete` — two-step confirmation, deletes all data
- **Contextual upsell:** `ProUpsellModal` fires after 3 tool completions via `SessionUsageService`
- **Teams tier:** Stripe product wired, pricing page complete
- **Developer API tier:** API key generation, hashing, per-key usage tracking, `/account/api-keys` dashboard
- **Referral program:** Unique codes, click tracking, Stripe coupon reward on conversion, stats in Account page
- **Microsoft Clarity:** Analytics installed
- **SEO:** `sitemap.xml`, `robots.txt`, JSON-LD structured data, Open Graph tags
- **Secrets rotated:** Anthropic, Stripe, OneDrive, Resend
- **`appsettings.Development.json` added to `.gitignore`**
- **Nav logo fix:** Light mode color corrected (Pdf dark, Tool blue, stack dark)
- **Sign PDF fixes:** JS interop ordering fixed, canvas state captured before state change

---

## 🔐 Security Architecture

### Headers (production)
- `Content-Security-Policy` — covers all known origins
- `Strict-Transport-Security` — HSTS with includeSubDomains
- `X-Frame-Options: DENY`
- `X-Content-Type-Options: nosniff`
- `Referrer-Policy: strict-origin-when-cross-origin`
- `Permissions-Policy` — camera, mic, geolocation, payment all blocked

### Auth0 setup
- M2M app required for GDPR deletion (`delete:users` permission)
- Config keys: `Auth0__ManagementClientId`, `Auth0__ManagementClientSecret`

### Secrets management
- Never in `appsettings.json` or committed files
- Local dev: `appsettings.Development.json` (gitignored)
- Production: Azure App Service → Configuration → Application settings
- Azure env var format: `Section__Key` (double underscore)

---

## 🗄️ Database Schema (Azure SQL)

### Tables
- `PdfJobs` — job tracking with blob URLs
- `UserSubscriptions` — Stripe subscription data per userId
- `DownloadHistory` — per-user tool usage history
- `AiUsageLogs` — AI tool usage for limit enforcement
- `ApiKeys` — developer API key hashes, prefixes, usage counts
- `Referrals` — referral codes, status (Pending/Converted/Rewarded), Stripe discount IDs

### Admin bypass
`SubscriptionService.GetStatusAsync` reads `AdminUserIds` from config — comma-separated Auth0 user IDs that always return Pro status. Used for Chrystal's account during development.

---

## 🚧 Known Issues / Pending

- **OneDrive OAuth** — `cloud_error=token_failed` at end of Authorization Code + PKCE flow — server-side OAuth implemented but token exchange still failing
- **Sign PDF Y-coordinate** — signature placement is close but slightly off vertically — coordinate math needs one more calibration pass
- **Stripe webhook secret** — currently empty in config — needs to be set in Azure for subscription webhooks to verify correctly
- **Google Search Console** — sitemap not yet submitted (waiting for production deployment)
- **Microsoft Clarity** — installed but not yet verified (waiting for production traffic)

---

## 🚀 Deployment

- **Staging:** Push to `staging` branch → GitHub Actions auto-deploys frontend
- **Production:** Push to `master` branch → GitHub Actions auto-deploys
- **API:** Deployed manually via Visual Studio right-click publish (no GitHub Actions workflow yet)
- **Pre-deployment checklist:**
  - All secrets in Azure App Service Configuration (not in code)
  - CORS locked to production origins
  - Stripe webhook secret set
  - `appsettings.Development.json` NOT committed
  - Run all EF migrations against production DB

---

## 🎯 SEO Strategy

### Implemented
- `sitemap.xml` — all 35+ tool pages, prioritized
- `robots.txt` — blocks auth/account pages, allows all tools
- JSON-LD structured data — WebApplication schema with offers
- Open Graph + Twitter Card meta tags
- Unique `<PageTitle>` and `<meta description>` on every tool page
- Canonical URLs on all tool pages
- Multiple URL slugs per tool (e.g. `/compress-pdf`, `/fix-pdf-too-large-for-email`, `/compress-for-email`)

### Next to build

| URL slug | Target query | Monthly searches |
|---|---|---|
| `/compress-for-email` | "compress PDF for email" | 450K |
| `/extract-invoice-data` | "extract data from PDF invoice" | 90K |
| `/sign-contract` | "sign PDF contract online" | 200K |
| `/compare-contracts` | "compare two PDF documents" | 60K |
| `/merge-pdfs-for-tax` | "merge PDFs for tax filing" | 40K |

---

## 🏆 Competitive Edge

1. **PDF ↔ Excel bridge** — own pdftoolstack.com + exceltoolstack.com. No competitor at this price has both.
2. **AI-first at SMB price** — Adobe is $25+/month enterprise. Smallpdf has no meaningful AI.
3. **Privacy-first** — "We don't train on your documents." Wedge for legal, healthcare, finance.
4. **Batch at $12/mo** — competitors charge significantly more or don't offer it at this tier.
5. **Developer API** — API key management live, ready to monetize at $49/month.
6. **Referral flywheel** — automatic Stripe coupon reward drives viral growth.

---

## 🧪 Working with Claude — Architecture Rules

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
- `SubscriptionService` should be nullable throughout controllers (runs without DB locally)
- CORS: open in development, locked to production origins in production
- Rate limiter: always skip OPTIONS preflights
- EF migrations: always run with `-Project PdfToolStack.Infrastructure -StartupProject PdfToolStack.API`

---

## 💬 Continuation Prompts for Next Session

Paste this intro + attach this file + attach PdfToolStack.zip to resume:

> "I'm Chrystal, building PdfToolStack.com — AI-powered PDF SaaS on .NET 9, Blazor WASM, Clean Architecture, Azure, Stripe, Auth0. I've uploaded my project context file. Please read it fully before we start."

**To fix OneDrive token_failed:**
> "Let's fix the OneDrive OAuth token_failed error in PdfToolStack. The server-side Authorization Code + PKCE flow is implemented but the token exchange is failing. Review the CloudStorageController and OneDriveService and diagnose the issue."

**To fix Sign PDF Y-coordinate:**
> "Let's fix the Sign PDF signature placement Y-coordinate offset in PdfToolStack. The signature lands slightly above where the user placed it. Current formula: `pdfY = (float)((canvasH - clampedY) / 1.2) - (sigH / 2)`. Need exact calibration."

**To set up Stripe webhook secret:**
> "Help me set up the Stripe webhook secret for PdfToolStack in production. I need to configure the webhook endpoint in Stripe dashboard and add the secret to Azure App Service."

**To build intent-based SEO pages:**
> "Let's build the intent-based URL slug pages for PdfToolStack. Start with /compress-for-email — problem-first copy, proper PageTitle and meta description, embedded compress tool."

**To add Azure Application Insights:**
> "Let's add Azure Application Insights to PdfToolStack for per-tool usage charts and error rate monitoring. Show the full implementation."

**To set up Google Search Console:**
> "PdfToolStack is now live in production. Help me submit the sitemap to Google Search Console and verify the domain via Namecheap DNS."

**To write launch content:**
> "Help me write a Show HN post for PdfToolStack and a cold outreach email targeting small accounting firms. Lead with batch processing and PDF compare as differentiators over Smallpdf."

---

*Last updated: April 11, 2026*