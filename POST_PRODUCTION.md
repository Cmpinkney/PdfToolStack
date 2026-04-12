# PdfToolStack — Post-Production Growth Playbook

> Do NOT execute any of this until pdftoolstack.com is live, all tools verified, and Stripe checkout confirmed working in production.

---

## Pre-Publish Checklist (must be ✅ before any marketing)

- [ ] pdftoolstack.com loads correctly
- [ ] All 35+ tools work in production
- [ ] Stripe checkout completes end-to-end
- [ ] Pro subscription activates after payment
- [ ] Sign PDF places signature correctly
- [ ] AI tools work with admin account
- [ ] No console errors on homepage
- [ ] Security headers present
- [ ] sitemap.xml accessible at pdftoolstack.com/sitemap.xml
- [ ] robots.txt accessible at pdftoolstack.com/robots.txt
- [ ] Microsoft Clarity tracking confirmed
- [ ] Google Search Console domain verified and sitemap submitted
- [ ] Contact form sends email
- [ ] Pro welcome email sends after checkout

---

## Your Story (fill this in before writing any content)

Answer these before writing any posts — every channel converts better with a personal story:

- **Why did you build PdfToolStack?** (the personal pain point)
- **What problem does it solve that competitors don't?**
- **Any early users or results?** (even one stat helps — "processed X PDFs in beta")
- **What makes you the right person to build this?** (.NET developer, background in X)

---

## Week 1 — Launch Day

### 1. Show HN (Hacker News)

**URL:** news.ycombinator.com/submit
**Best time to post:** Tuesday–Thursday, 8–10am EST

**Title format:**
```
Show HN: PdfToolStack – AI-powered PDF tools for small teams ($12/mo, privacy-first)
```

**Post template:**
```
Hi HN,

I built PdfToolStack — an AI document workspace for small teams that 
actually work with PDFs every day (accountants, paralegals, HR coordinators).

The problem: Smallpdf and Adobe have no meaningful AI at SMB pricing. 
Claude AI at $25+/month enterprise is overkill. Nothing in the middle 
existed.

What I built:
- 35+ PDF tools (compress, merge, split, sign, convert)
- AI Contract Reviewer — highlights risky clauses, key dates, obligations
- AI Invoice Data Extractor — PDF invoice → structured Excel in seconds
- Batch processing — apply any tool to 20 PDFs at once, ZIP output
- $12/month Pro, no ads, files deleted in 1 hour

Tech: ASP.NET Core + Blazor WASM, Azure, Claude AI (Anthropic), Stripe, Auth0

Privacy-first: files auto-deleted in 1 hour, no training on your documents, 
no ads on Pro tier.

Would love feedback on the AI tools especially — the contract reviewer and 
invoice extractor are the headline features I'm most proud of.

https://pdftoolstack.com
```

**Tips:**
- Respond to every comment within the first 2 hours
- Be honest about what's not built yet
- Don't oversell — HN readers see through it
- If it doesn't hit front page in 2 hours, it won't — don't repost same day

---

### 2. Indie Hackers Launch Post

**URL:** indiehackers.com/post/new
**Section:** Products

**Title:**
```
Launched PdfToolStack — AI PDF tools for small teams. Here's how I built it and what's next.
```

**Post template:**
```
Hey IH,

I just launched PdfToolStack.com — an AI-powered PDF workspace targeting 
SMB professionals who process documents daily.

**The problem I'm solving:**
[YOUR PERSONAL STORY HERE]

**What makes it different:**
- AI Contract Reviewer (Claude-powered) — not just search, actual risk analysis
- AI Invoice Extractor — PDF → structured Excel in seconds
- Batch processing at $12/mo (competitors charge 3x)
- Privacy-first: files deleted in 1 hour, no training on documents

**Traction so far:**
[ANY EARLY NUMBERS HERE]

**Tech stack:**
ASP.NET Core + Blazor WASM, Azure, Stripe, Auth0, Claude AI

**Revenue model:**
Free tier (25MB, all tools) → Pro $12/mo → Teams $29/mo → Developer API $49/mo

**What I'm focused on next:**
- Cold outreach to accounting firms
- Intent-based SEO pages
- ExcelToolStack.com as a companion product

Happy to answer any questions about the build, the tech, or the go-to-market.

https://pdftoolstack.com
```

**Follow-up:** Post monthly MRR updates — IH loves transparency. Even "$0 → $120 MRR" updates get traction.

---

### 3. Directory Listings (do all 4 on launch day)

#### AlternativeTo
**URL:** alternativeto.net/add-software/
- Name: PdfToolStack
- Category: PDF Software, Document Management
- Alternatives to: Smallpdf, Adobe Acrobat, ilovepdf
- Description: "AI-powered PDF tools for small teams. Compress, merge, split, sign, convert PDFs. AI contract reviewer and invoice data extractor. $12/month Pro, privacy-first, files deleted in 1 hour."

#### SaaSHub
**URL:** saashub.com/add
- Category: PDF Tools, Document Processing
- Same description as AlternativeTo
- Add pricing tiers

#### Capterra
**URL:** capterra.com/vendors/sign-up
- Category: PDF Software, Document Management Software
- Requires: company info, logo, screenshots
- Free listing — paid placement optional (skip for now)

#### G2
**URL:** g2.com/products/new
- Category: PDF Software
- Requires: logo, screenshots, pricing
- Ask your first 5 customers to leave reviews immediately after signup

#### Product Hunt
**URL:** producthunt.com/posts/new
- **Do this separately in Week 2** — PH works best with a dedicated launch strategy
- Need a hunter (someone with PH followers) to post on your behalf
- Best day: Tuesday or Wednesday
- Prepare: tagline, description, 3 images, demo GIF or video

---

## Week 2 — Content & Outreach

### 4. Cold Outreach — Accounting Firms

**Target:** Small accounting firms (2–20 employees), bookkeepers, CPAs
**Find them:** LinkedIn search "CPA firm", Google Maps "accounting firm [city]", AICPA directory

**Email template:**
```
Subject: Free tool for processing PDF invoices — no strings attached

Hi [Name],

Quick question — how much time does your team spend manually pulling 
data from PDF invoices each week?

I built PdfToolStack.com — an AI tool that extracts invoice data 
(vendor, amount, date, line items) from PDFs directly into Excel 
in about 10 seconds. It also handles batch processing of up to 20 
PDFs at once.

I'd like to offer you a free 30-day Pro trial — no credit card required.

If it saves your team even 1 hour a week, it's worth the 2 minutes 
to try it.

https://pdftoolstack.com

Best,
Chrystal
```

**Follow-up (day 5 if no reply):**
```
Subject: Re: Free tool for PDF invoices

Hi [Name],

Just following up on my note from last week. Happy to hop on a 
5-minute call to show you how the invoice extraction works if 
that's easier.

Either way — the free trial offer stands.

Chrystal
```

**Tracking:**
- Send 10/day for 5 days = 50 outreach emails
- Track: sent, opened, replied, trialed, converted
- Target: 5% conversion = 2-3 paying customers from first campaign

---

### 5. Reddit Strategy

**Target subreddits:**
- r/freelance (380K members)
- r/accounting (180K members)
- r/legaladvice (2.1M members)
- r/smallbusiness (1.4M members)
- r/productivity (1.2M members)
- r/paralegal (35K members)

**Rules:**
- Never post "I built X, check it out" — instant downvotes
- Answer existing questions helpfully, mention PdfToolStack naturally
- Only post directly when the subreddit allows it (check rules)

**Post templates:**

r/accounting:
```
Title: How do you handle bulk PDF invoice processing? 
Curious what workflows others use for extracting data 
from 20-50 PDF invoices at a time. Currently testing 
an AI extraction approach that dumps to Excel — happy 
to share what's working.
```

r/freelance:
```
Title: Built a free tool for signing PDFs without Adobe — 
draw your signature, place it anywhere, download instantly. 
No account needed for basic use.
[link to /sign-pdf]
```

r/paralegal:
```
Title: AI contract review — has anyone tried using AI to 
flag risky clauses before attorney review? 
[Share your experience, mention PdfToolStack if asked]
```

**Cadence:** 2-3 helpful comments per day across subreddits. Direct posts 1x/week max per subreddit.

---

## Week 3 — Content Marketing

### 6. Medium Articles

**Publication strategy:**
- Publish on Medium under your own profile
- Submit to "Towards Data Science", "The Startup", or "Better Programming" publications for distribution
- Each article should solve a specific problem and link naturally to PdfToolStack

**Article 1 — Contract Reviewer:**
```
Title: How I review 50 contracts a week using AI 
— without paying for Adobe Acrobat

Hook: "Last year I spent 6 hours reviewing a service agreement 
that turned out to have a liability clause that could have cost 
my client $50,000. I missed it. An AI caught it in 8 seconds."

Structure:
1. The problem with manual contract review
2. What AI contract review actually does (and doesn't do)
3. Walk through using PdfToolStack contract reviewer
4. Real example: what it flagged in a sample NDA
5. When you still need a lawyer
6. CTA: Try it free at pdftoolstack.com/review-contract
```

**Article 2 — Invoice Extraction:**
```
Title: Stop copy-pasting PDF invoices into Excel. 
There's a better way.

Hook: "I watched an accountant spend 45 minutes copying 
invoice data into a spreadsheet. Data that an AI extracted 
in 40 seconds."

Structure:
1. The real cost of manual data entry
2. How AI invoice extraction works
3. Walk through with a real invoice
4. Accuracy — what it gets right and what to double-check
5. CTA: Try it free at pdftoolstack.com/extract-invoice-data
```

**Article 3 — Privacy angle:**
```
Title: Why I don't trust most PDF tools with my clients' documents

Hook: "Most free PDF tools store your files indefinitely. 
Some train AI models on them. Here's what to look for."

Structure:
1. What happens to your files when you upload them
2. The red flags to look for in any PDF tool
3. What privacy-first actually means (files deleted in 1 hour, no training)
4. CTA: pdftoolstack.com/security
```

**Cadence:** 1 article per week for first month, then 2x/month

---

### 7. LinkedIn Content Strategy

**Profile setup:**
- Headline: "Building PdfToolStack.com — AI PDF tools for small teams | .NET Developer"
- About: Your story + link to pdftoolstack.com

**Content types that work for SaaS founders on LinkedIn:**

**Type 1 — Behind the build (weekly):**
```
I spent 3 days debugging a coordinate system bug in our PDF 
signature tool.

The signature was appearing 40 pixels above where users placed it.

Turns out: iTextSharp uses bottom-left origin. PDF.js uses top-left. 
And the browser canvas uses CSS pixels. Three different coordinate 
systems in one feature.

The fix was 6 lines of math.

Building in public at pdftoolstack.com
```

**Type 2 — Problem spotlight (weekly):**
```
Accountants lose ~3 hours/week copying PDF invoice data into Excel.

That's 150 hours/year. At $75/hour billing rate = $11,250 in lost 
productive time per accountant per year.

AI extraction does it in 10 seconds.

[Screenshot of the tool in action]

pdftoolstack.com/extract-invoice-data
```

**Type 3 — Customer story (when you have one):**
```
A paralegal at [firm type] told me they reviewed 12 contracts 
last week using PdfToolStack's AI reviewer.

They caught 3 clauses their client wouldn't have noticed.

That's the product working exactly as intended.
```

**Type 4 — Industry insight (2x/month):**
```
5 PDF tools accountants actually use (and what each is missing)

1. Adobe Acrobat — powerful but $25+/month enterprise pricing
2. Smallpdf — good UX, zero AI features
3. ilovepdf — free but no batch, no AI
4. DocuSign — signing only, not a PDF workspace
5. PdfToolStack — [your positioning]
```

**Cadence:** 3-4 posts per week. Always end with a question to drive comments.

---

## Month 2 — Video & Partnerships

### 8. YouTube Short-Form Tutorials

**Channel name:** PdfToolStack
**Format:** 60-90 second screen recordings with voiceover
**Upload cadence:** 2x per week

**Video ideas (in priority order):**

1. "Extract invoice data from PDF to Excel in 10 seconds"
2. "Sign a PDF without Adobe — free and takes 30 seconds"
3. "Review a contract with AI — catch risky clauses instantly"
4. "Compress a PDF for email in 5 seconds"
5. "Merge 10 PDFs at once — batch processing tutorial"
6. "How to redact sensitive information from a PDF"
7. "Convert PDF to editable Word document"
8. "Add page numbers to a PDF in seconds"
9. "Protect a PDF with a password"
10. "Compare two PDF versions — find every change instantly"

**SEO titles format:** "[Action] in [Time] — [Tool Name]"
Example: "Extract invoice data from PDF to Excel in 10 seconds — PdfToolStack"

**Description template:**
```
In this video I show you how to [action] using PdfToolStack.

Try it free: https://pdftoolstack.com/[tool-url]

No signup required for basic use. Pro plan from $12/month.

Timestamps:
0:00 — Upload your file
0:20 — [Step 2]
0:45 — Download result
```

---

### 9. Affiliate Program

**Structure:**
- Commission: 30% recurring for lifetime of customer
- Cookie duration: 90 days
- Minimum payout: $25
- Payment: monthly via Stripe

**Target affiliates:**
- Accounting bloggers (CPA exam prep sites, bookkeeping blogs)
- Paralegal YouTubers and bloggers
- Productivity newsletter writers
- Legal tech reviewers
- Small business resource sites

**Outreach template:**
```
Subject: 30% recurring commission — PDF tools for your audience

Hi [Name],

I follow your content on [platform] — your [specific post] was exactly 
what my target customer reads.

I run PdfToolStack.com — AI-powered PDF tools for accountants, 
paralegals, and small business owners. $12/month Pro.

I'd love to offer you a 30% recurring commission on every customer 
you refer. That's $3.60/month per Pro subscriber, forever.

If your audience processes PDFs regularly, this converts well.

Interested? I can set you up with a custom link today.

Chrystal
```

---

### 10. Integration Partnerships

**Target partners (approach in this order):**

| Company | Why | Angle |
|---------|-----|-------|
| Wave (accounting) | Free, SMB-focused | "PDF invoice → Wave transaction" integration |
| FreshBooks | SMB invoicing | "Extract FreshBooks invoice data from PDFs" |
| Clio (legal) | Legal practice management | "AI contract review before uploading to Clio" |
| Gusto (HR) | SMB HR platform | "Process PDF offer letters and contracts" |
| QuickBooks | Accounting | "PDF receipt → QuickBooks entry via AI extraction" |
| Notion | Productivity | "Extract PDF data into Notion databases" |
| Zapier | Automation | "PdfToolStack + Zapier integration" |

**Outreach approach:**
- Find the partnerships/integrations contact on their website
- Lead with the user benefit, not the product
- Offer to build the integration yourself
- Start with Zapier — their directory drives significant traffic

---

## Ongoing — SEO Content

### Blog Article Calendar (publish 2x/month)

| Month | Article | Target keyword |
|-------|---------|---------------|
| 1 | How to compress PDF for email without losing quality | compress pdf for email |
| 1 | How to extract invoice data from PDF automatically | extract invoice data pdf |
| 2 | How to review a contract with AI before signing | ai contract review |
| 2 | Best Smallpdf alternatives in 2026 | smallpdf alternative |
| 3 | How to merge PDFs for tax filing | merge pdf tax documents |
| 3 | Best Adobe Acrobat alternatives for small teams | adobe acrobat alternative |
| 4 | How to sign a contract online — legal electronic signatures | sign contract online |
| 4 | How to make a scanned PDF searchable (OCR guide) | ocr pdf searchable |
| 5 | How to redact sensitive information from PDF | redact pdf |
| 5 | Best PDF tools for accountants in 2026 | pdf tools accountants |

---

## MRR Milestones & Unlock Criteria

| Milestone | Unlock |
|-----------|--------|
| $500 MRR | Start YouTube channel |
| $1,000 MRR | Launch affiliate program |
| $2,500 MRR | Submit to Product Hunt with proper preparation |
| $5,000 MRR | Start integration partnership outreach |
| $10,000 MRR | Developer API public launch at $49/mo |
| $25,000 MRR | Hire first content writer |
| $50,000 MRR | Begin SOC 2 Type II certification |

---

## Metrics to Track Weekly

| Metric | Tool | Target |
|--------|------|--------|
| Unique visitors | Microsoft Clarity | +20% week over week |
| Free signups | Azure SQL / Account table | Track weekly |
| Free → Pro conversion | Stripe dashboard | Target 3-5% |
| MRR | Stripe dashboard | +$500/month |
| Churn rate | Stripe | Under 5%/month |
| Most used tools | Serilog audit logs | Identify top 5 |
| AI tool usage | AiUsageLogs table | Track per tool |
| Referral conversions | Referrals table | Track monthly |

---

*Last updated: April 11, 2026*
*Status: Pre-production — do not publish until pdftoolstack.com is live*