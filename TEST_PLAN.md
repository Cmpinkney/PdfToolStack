# PdfToolStack — Growth Strategy & Session Log

> This file tracks the product strategy, completed work, and next steps. Paste it into a new Claude chat alongside PROJECT_CONTEXT.md to resume without re-explaining anything.

---

## How to resume in a new chat

> "I'm Chrystal, building PdfToolStack.com — AI-powered PDF SaaS on .NET 9, Blazor WASM, Clean Architecture, Azure, Stripe, Auth0. I've uploaded my project context and growth strategy files. Please read both before we start."

Attach: `PdfToolStack-Context.md` + `PdfToolStack_GrowthStrategy.md` + `PdfToolStack.zip`

---

## Current product state (April 2026)

| Property | Value |
|---|---|
| URL | pdftoolstack.com |
| Status | Built, tested, staged — pre-launch |
| Revenue | $0 (pre-launch) |
| Target | $1,000 MRR → $10,000 MRR |
| Pricing | Free / Pro $12/mo or $99/yr / Teams $29/mo (5 seats) / Developer API $49/mo |
| Stack | ASP.NET Core + Blazor WASM, Azure, Stripe, Auth0, Serilog, Clarity |
| GitHub | https://github.com/Cmpinkney/PdfToolkit |

---

## Strategy summary

### Positioning
"The AI document workspace for small teams" — not a PDF utility site.

### Target customer
SMB professionals (accountants, paralegals, HR, real estate agents) who process 10–50 PDFs/day. Willingness to pay: $12–$39/month. Current alternative: Smallpdf Pro (no AI) or Adobe Acrobat ($25+, enterprise-heavy).

### Moat
1. PDF ↔ Excel bridge (own both domains — no competitor does this at this price)
2. AI tools at SMB pricing (Smallpdf has none, Adobe is enterprise)
3. Privacy-first (files deleted in 1hr, no training on documents, GDPR compliant)
4. Batch processing at $12/mo (competitors charge 2–3x)
5. Developer API tier live — passive revenue ready
6. Referral flywheel — automatic Stripe coupon reward drives viral growth

---

## Completed implementation

### ✅ Session 1–2 (March 2026) — Architecture & Core Features
- Refactored 4 TODO endpoints (Organize, Sign, Edit, Annotate) to Strategy/Factory pattern
- Resolved circular dependency: complex strategies moved to `Infrastructure/Strategies/`
- Pro raised from $9.99 → $19/month (later corrected to $12/mo), $79 → $99/year
- Price IDs moved from hardcoded Blazor → server config → `/api/subscription/plans` endpoint
- My Documents dashboard built (Account.razor)
- Batch processing (`/batch`, `api/pdf/batch`)
- PDF Compare (`/compare-pdf`, `api/pdf/compare`, `ComparePdfProcessor`)
- AI Contract Reviewer (`/review-contract`)
- AI Invoice Data Extractor (`/extract-invoice-data`)
- JpgToPdf, PdfToJpg, PdfToExcel processors
- Security hardening (rate limiting, magic byte validation)
- SEO meta descriptions for all tool pages
- New logo, favicons, profile dropdown, theme switcher
- Cloud storage: Google Drive ✅, Dropbox ✅, OneDrive ❌ (token_failed — pending)

### ✅ Session 3 (April 2026) — Compliance, Security & Growth

#### Legal & Compliance
- **GDPR data deletion flow** — full stack: SQL rows, Azure blobs, Stripe subscription cancel, Auth0 account delete
- **Cookie consent** — banner updated, only essential cookies, no decline button needed
- **Privacy policy** — rewritten accurately (removed false ad/analytics claims, added Auth0/Stripe/Azure/Anthropic/Resend disclosures, data residency, GDPR rights)
- **Account deletion page** — `/account/delete` with two-step confirmation
- **Data residency disclosure** — Azure West US 2 disclosed in Privacy Policy section 5

#### Security
- **Content Security Policy** — header covering all known origins (Stripe, Auth0, Anthropic, Azure, Clarity, CDNs)
- **Per-user rate limiting** — authenticated users tracked by userId, anonymous by IP, OPTIONS preflights never counted
- **Audit logging** — `AuditLoggingMiddleware` logs Tool, UserId, FileSize, Duration, StatusCode with `[AUDIT]` prefix
- **Secrets rotated** — Anthropic, Stripe, OneDrive, Resend all rotated
- **`appsettings.Development.json` added to `.gitignore`**
- **Security/trust page** — `/security` modeled after smallpdf/ilovepdf

#### Growth & Monetization
- **Contextual upsell modal** — `ProUpsellModal` + `SessionUsageService` fires after 3 tool completions per session
- **Teams tier** — Stripe product wired, pricing page complete, $29/mo for 5 seats
- **Developer API tier** — API key generation (SHA-256 hashed, `pts_live_` prefix), per-key monthly usage tracking, `/account/api-keys` dashboard
- **Referral program** — unique codes, click tracking, Stripe 100% coupon reward on conversion, stats in Account page
- **Microsoft Clarity** — analytics installed (heatmaps + session recordings)
- **SEO** — `sitemap.xml`, `robots.txt`, JSON-LD structured data, Open Graph + Twitter Card tags

#### Bug Fixes
- **Sign PDF** — JS interop ordering fixed (capture canvas data before state change), blank canvas on ProcessFile resolved
- **Nav logo** — light mode color fixed (Pdf dark, Tool blue, stack dark)
- **Rate limiter** — OPTIONS preflight no longer counts against limit

---

## Revenue path

### $0 → $1K MRR (immediate focus)
Don't rely on SEO yet — it takes 6–12 months to compound.

**Actions:**
1. Fix remaining bugs and deploy to production
2. Post "Show HN: I built an AI PDF tool for small teams" on Hacker News
3. Post on Indie Hackers with batch + AI contract reviewer as the hook
4. Cold outreach to 50 small accounting firms — free 30-day Pro trial
5. Post in r/freelance, r/legaladvice, r/accounting with AI data extractor as hook
6. Submit to: AlternativeTo, SaaSHub, Capterra (free listings)
7. Offer annual billing at $99/yr — cash upfront, customer saves $45
8. Activate referral program — existing users become growth channel

**Target:** 84 Pro subscribers @ $12/mo = $1,008 MRR

### $1K → $10K MRR
SEO + referral flywheel + word of mouth compounds.

**Actions:**
1. Build 5 intent-based landing pages (see SEO table)
2. Launch Teams tier outreach — one 5-seat team = $29/mo
3. Activate Developer API tier pricing at $49/mo
4. Write 10 long-form SEO articles targeting high-intent queries
5. Submit sitemap to Google Search Console (post production deployment)
6. Set up Azure Application Insights for per-tool conversion tracking

**Target:** ~600 Pro + 15 Teams + 5 API accounts

### $10K → $100K MRR
- API tier passive revenue ($49/mo → scale to $99/mo at volume)
- Mid-size business customers (20–50 seats)
- ExcelToolStack.com launch as funnel into PdfToolStack Pro
- SOC 2 Type II certification — unlocks enterprise and government sales

---

## Next build priorities (in order)

| Priority | Feature | Why |
|---|---|---|
| 1 | Fix Sign PDF Y-coordinate | Core tool broken — must fix before launch |
| 2 | Fix OneDrive token_failed | Cloud storage incomplete |
| 3 | Set Stripe webhook secret in Azure | Subscriptions won't verify in production |
| 4 | Deploy to production | Nothing else matters until live |
| 5 | Submit sitemap to Google Search Console | SEO clock starts on go-live |
| 6 | Intent-based SEO slug pages | Foundation for all organic traffic |
| 7 | Azure Application Insights | Per-tool usage charts and error monitoring |
| 8 | Show HN + Indie Hackers launch post | First traffic spike |
| 9 | Cold outreach to 50 accounting firms | First paid users |
| 10 | ExcelToolStack.com — companion product | PDF↔Excel bridge, second funnel |

---

## What NOT to build yet

- Mobile app
- More standard PDF tools (have 35+ — enough)
- Blog / content marketing (week 5+ activity — SEO takes time)
- SOC 2 certification (pursue at $50K+ ARR)
- Paid advertising (organic first until $5K MRR)

---

## SEO target pages

| URL slug | Target query | Est. monthly searches |
|---|---|---|
| `/compress-for-email` | "compress PDF for email" | 450K |
| `/sign-contract` | "sign PDF contract online" | 200K |
| `/extract-invoice-data` | "extract data from PDF invoice" | 90K |
| `/compare-contracts` | "compare two PDF documents" | 60K |
| `/merge-pdfs-for-tax` | "merge PDFs for tax filing" | 40K |

---

## PDF to Word — regression tests

### Bug: DOCX bytes returned as 0 (fixed May 2026)

**Root cause:** `outputStream.ToArray()` was called before `WordprocessingDocument` was
disposed/flushed, so the ZIP package was never sealed and the byte array was empty.

**Fix:** `WordprocessingDocument.Create(...)` is wrapped in a `using` block.
`outputStream.ToArray()` is called only after that block exits, guaranteeing the package
is fully written and closed.

**Regression scenario to test on any refactor of `CreateWordDocument`:**
1. POST a simple text-based PDF to `api/pdf/process?toolType=2`.
2. Response must be HTTP 200 with `isSuccess: true`.
3. `outputBytes` in the response must be non-null and length > 0 (expect > 1 000 bytes for even a one-page document).
4. The decoded bytes must be a valid DOCX ZIP — first four bytes are `PK\x03\x04`, and the archive contains `[Content_Types].xml` and `word/document.xml`.
5. Feeding a scanned-only PDF must return HTTP 422 with an `error` field containing "scanned or image-only pages".
6. A corrupt or password-protected PDF must return HTTP 422 with a friendly error message, never HTTP 200 with empty bytes.

---

## PDF to Word production readiness

- Frontend downloads the API-generated DOCX bytes, not the original uploaded PDF bytes.
- API failures return clean user messages; OCR-required conversion failures should surface as HTTP 422 and should not become 500s.
- Conversion keeps per-page extraction order, inserts page breaks, and sanitizes invalid control characters before writing DOCX text.
- Scan detection treats empty pages as blank and treats low-text pages as effectively blank only when both meaningful characters and word count are low; PDFs with 40% or more blank/effectively blank pages are blocked with an OCR-required message.
- Mixed PDFs below the blank-page threshold continue with warnings so usable text-based documents are not overblocked.

Remaining limitation: scan detection is based on extracted text quality, so unusual PDFs with misleading hidden text or OCR artifacts can still miss edge cases.

---

## Pre-launch checklist

- [ ] Fix Sign PDF Y-coordinate placement
- [ ] Fix OneDrive token_failed
- [ ] Set Stripe webhook secret in Azure App Service
- [ ] Confirm all Azure env vars are set (no empty secrets)
- [ ] Run EF migrations against production DB
- [ ] Test checkout flow end-to-end in production
- [ ] Test GDPR deletion flow end-to-end
- [ ] Verify Clarity tracking fires on production
- [ ] Submit sitemap to Google Search Console
- [ ] Verify domain in Google Search Console via Namecheap DNS TXT record
- [ ] Test all 35+ tools in production environment
- [ ] Confirm CORS is locked to pdftoolstack.com (not localhost)

---

## Session log

### Session 1 (March 2026)
- Full codebase analysis of PdfToolStack.zip
- Growth strategy developed across 7 dimensions
- Identified: circular dependency risk, pricing undercut, AI tools buried, no B2B motion

### Session 2 (March 2026)
- Refactored 4 TODO endpoints → Strategy/Factory pattern
- Fixed circular dependency (strategies moved to Infrastructure layer)
- Restored `StripeOptions.SectionName` and `FileLimit` class
- Raised Pro price to $19/$150
- Built `/api/subscription/plans` endpoint + rewrote `Pricing.razor`
- Built My Documents dashboard (Account.razor)
- Built batch processing (`/batch`, `api/pdf/batch`)
- Built PDF Compare (`/compare-pdf`, `api/pdf/compare`, `ComparePdfProcessor`)
- AI Contract Reviewer, AI Invoice Extractor, JpgToPdf, PdfToJpg, PdfToExcel
- Cloud storage integrations (Google Drive ✅, Dropbox ✅, OneDrive ❌)
- Security hardening, SEO meta, new logo, profile dropdown, footer

### Session 3 (April 11, 2026)
- Full compliance + security overhaul (GDPR, CSP, rate limiting, audit logging)
- Privacy policy rewritten, cookie consent fixed, security page built
- Secrets rotated (Anthropic, Stripe, OneDrive, Resend)
- Contextual upsell modal (SessionUsageService + ProUpsellModal)
- Teams tier Stripe wired, Developer API tier built end-to-end
- Referral program built (codes, tracking, Stripe coupon reward)
- Microsoft Clarity installed
- SEO infrastructure (sitemap, robots.txt, JSON-LD, OG tags)
- Sign PDF JS interop ordering fixed
- Nav logo light mode color fixed
- Pro price corrected to $12/mo, $99/yr across all pages
- Updated PROJECT_CONTEXT.md and this file
- Committed all changes to staging branch

---

*Last updated: April 11, 2026*
