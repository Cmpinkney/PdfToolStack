# PdfToolStack — Growth Strategy & Session Log

> This file tracks the product strategy, completed work, and next steps. Paste it into a new Claude chat alongside PROJECT_CONTEXT.md to resume without re-explaining anything.

---

## How to resume in a new chat

> "I'm Chrystal, a full-stack .NET developer working on PdfToolStack.com — an AI-powered PDF SaaS. I've uploaded my project context and growth strategy files. Please read both before we start."

Attach: `PROJECT_CONTEXT.md` + `PdfToolStack_GrowthStrategy.md` + `PdfToolStack.zip`

---

## Current product state (March 2026)

| Property | Value |
|---|---|
| URL | pdftoolstack.com |
| Status | Built, tested, staged — not yet publicly launched |
| Revenue | $0 (pre-launch) |
| Target | $1,000 MRR → $10,000 MRR |
| Pricing | Free / Pro $19/mo / Pro $150/yr |
| Stack | ASP.NET Core + Blazor WASM, Azure, Stripe, OAuth |
| GitHub | https://github.com/Cmpinkney/PdfToolkit |

---

## Strategy summary

### Positioning
"The AI document workspace for small teams" — not a PDF utility site.

### Target customer
SMB professionals (accountants, paralegals, HR, real estate agents) who process 10–50 PDFs/day. Willingness to pay: $19–$39/month. Current alternative: Smallpdf Pro (no AI) or Adobe Acrobat ($25+, enterprise-heavy).

### Moat
1. PDF ↔ Excel bridge (own both domains — no competitor does this at this price)
2. AI tools at SMB pricing (Smallpdf has none, Adobe is enterprise)
3. Privacy-first (files deleted in 1hr, no training on documents)
4. Batch processing at $19/mo (competitors charge 2–3x)

---

## Completed implementation (30-day plan — DONE)

### ✅ Architecture cleanup
- Refactored 4 TODO endpoints (Organize, Sign, Edit, Annotate) to Strategy/Factory pattern
- Resolved circular dependency: complex strategies moved to `Infrastructure/Strategies/`
- `ProcessRequest` extended with all necessary payload fields

### ✅ Pricing upgrade
- Pro raised from $9.99 → $19/month, $79 → $150/year
- Price IDs moved from hardcoded Blazor → server config → `/api/subscription/plans` endpoint
- Existing subscribers unaffected (legacy price IDs preserved)

### ✅ My Documents dashboard
- Account page rebuilt with live subscription status, renewal date, manage billing
- Download history table (last 50 jobs, pulled from Azure SQL)
- `TrackDownloadAsync` wired into the process endpoint (fire-and-forget)
- Pro upsell nudge for free users

### ✅ Batch processing
- `POST api/pdf/batch` — 20 files max, ZIP output, per-file error log
- `/batch` Blazor page — tool selector, per-file status, Pro gate
- Supported tools: Compress, PDF to Word, PDF to JPG, Watermark, Number Pages, Flatten, Rotate, Protect, Unlock

### ✅ PDF Compare
- LCS word-level diff using PdfPig (text extraction) + iTextSharp (PDF report)
- Green = added, red = removed, summary stats at top of report
- `/compare-pdf` Blazor page — two-panel upload, Pro gate
- `ToolType.ComparePdf = 33` added

---

## Revenue path

### $0 → $1K MRR (immediate focus)
Don't rely on SEO yet — it takes 6–12 months to compound.

**Actions:**
1. Post "Show HN: I built an AI PDF tool for small teams" on Hacker News
2. Post on Indie Hackers with the batch + compare features as the hook
3. Cold outreach to 50 small accounting firms — free 30-day Pro trial offer
4. Post in r/freelance, r/legaladvice, r/accounting with the AI data extractor as the hook (build this first)
5. Submit to: AlternativeTo, SaaSHub, Capterra (free listings)
6. Offer annual billing at $150/yr — cash upfront, customer saves $78

**Target:** 53 Pro subscribers @ $19/mo = $1,007 MRR

### $1K → $10K MRR
SEO + word of mouth compounds. Build intent-based landing pages.

**Actions:**
1. Build 5 intent-based slug pages (see SEO table in PROJECT_CONTEXT.md)
2. Build AI data extractor — this is the flagship demo feature
3. Launch Teams tier — one 5-seat team @ $195/mo = 10 Pro subscribers equivalent
4. Write 10 long-form SEO articles targeting high-intent queries

**Target:** ~300 Pro + 5 Teams accounts

### $10K → $100K MRR
Requires one of:
- API tier ($99/mo) — passive revenue, developer integrations
- Mid-size business customers (20–50 seats @ $39/seat)
- ExcelToolStack launch as a funnel into PdfToolStack Pro

---

## Next build priorities (in order)

| Priority | Feature | Why |
|---|---|---|
| 1 | AI data extractor (`/extract-invoice-data`) | Flagship demo feature, highest conversion hook, bridges PDF↔Excel |
| 2 | Intent-based SEO slug pages | Foundation for all organic traffic |
| 3 | AI contract reviewer (`/review-contract`) | High willingness-to-pay, compliance/legal audience |
| 4 | Teams tier | One customer = 10 Pro subscribers in revenue |
| 5 | Show HN + Indie Hackers launch post | First traffic spike |
| 6 | Cold outreach to 50 accounting firms | First paid users |

---

## What NOT to build yet

- Mobile app
- More standard PDF tools (have 33 — enough)
- ExcelToolStack features (parallel product, dilutes focus until $5K MRR)
- API tier documentation (premature until $10K MRR)
- Blog / content marketing (week 5+ activity)

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
- Updated PROJECT_CONTEXT.md and this file

---

*Last updated: March 2026*
