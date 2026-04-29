# PdfToolStack — Master TODO Checklist
*Last updated: April 28, 2026*

---

## 🔴 Blocking — Fix Before Launch

- [ ] Fix OneDrive OAuth `token_failed` — server-side PKCE flow built, token exchange still failing
- [ ] Run EF migrations against production DB
- [ ] Deploy API to Azure App Service (publish via VS Code / VS)
- [ ] Push to master → GitHub Actions deploys frontend
- [ ] Update `ApiBaseUrl` in `wwwroot/appsettings.json` to production API URL before push
- [ ] Test all 35+ tools in production environment
- [ ] Test Stripe checkout end-to-end in production (Pro, Teams, all 4 addons)

---

## ✅ Completed — Blocking Items Resolved

- [x] Fix Sign PDF Y-coordinate placement — resolved via Syncfusion built-in signature tool
- [x] Set Stripe webhook secret in Azure App Service (`Stripe__WebhookSecret`)
- [x] Confirm all Azure env vars set — all confirmed including `Stripe__AiCredits50PriceId`
- [x] Lock CORS to pdftoolstack.com in production — environment-aware in Program.cs
- [x] Security hardening — all 4 criticals + 3 security issues fixed (see below)
- [x] Subscription gating — Pro/Teams no longer see upsell modal
- [x] Free tier file size corrected 100KB → 25MB
- [x] Support chat serialization bug fixed (anonymous type → named record types)
- [x] Addon purchase flow fixed (AddonType, URLs, price ID key names)
- [x] AiCreditPack expiry changed to never expire (DateTime.MaxValue sentinel)
- [x] Stripe webhook expanded to 8 events (refunds, disputes, renewals added)
- [x] Dual webhook deduplicated — only `/api/stripe/webhook` registered in Stripe
- [x] RateLimitingMiddleware build error fixed (CS1503 tuple type mismatch)

---

## ✅ Completed — Security & Code Quality

- [x] Division by zero guard in `AiController.GetUsage()`
- [x] Null check for `_subscriptionService` in `GetUsage()`
- [x] `[Authorize]` added to `CreateCheckout` and `CreateAddonCheckout`
- [x] UserId pulled from claims not request body in checkout endpoints
- [x] Stripe redirect URL allowlist validation in `PaymentController`
- [x] Anonymous support chat IP rate limiting (5/hr)
- [x] `charge.refunded` and `charge.dispute.created` webhook handlers added
- [x] 30s timeout on all Anthropic API calls
- [x] Support chat history clamped to last 10 messages
- [x] `CreatePortalSessionAsync` returns null instead of throwing
- [x] Anthropic API key fails fast at startup if missing
- [x] Duplicate `CloudProxy` HttpClient registration removed
- [x] RateLimitingMiddleware hourly eviction of stale buckets
- [x] `AdSlot` hidden from anonymous users
- [x] `SessionUsageService` counter moved to sessionStorage
- [x] `IsTestModeEnabled` moved to configuration (false in production)

---

## ✅ Completed — Infrastructure & Services

- [x] Stripe webhook configured — 8 events, correct endpoint
- [x] Resend domain verified (DKIM ✅, MX + SPF added to Namecheap)
- [x] Azure Application Insights created for API (`pdftoolstack-api`)
- [x] Azure Application Insights created for Web (`pdftoolstack-web`)
- [x] Auth0 callback/logout/CORS URLs updated for localhost + production
- [x] Azure env vars set: `Stripe__AiCredits50PriceId`, `Stripe__WebhookSecret`, `PaymentService__TestMode=false`
- [x] Admin bypass in `SubscriptionService` with no-expiry Pro status

---

## ✅ Completed — UI & Design

- [x] Hero illustration — animated orbit SVG (7 tools orbiting hub, two counter-rotating rings)
- [x] All 4 badges moved to right side in concave arc with float animations
- [x] Claude AI references removed from homepage
- [x] Navbar responsive — tool links hidden below 1100px, hamburger shown
- [x] Mobile accordion menu — 5 collapsible sections with icons and descriptions
- [x] Support chat UI — iOS-style bubbles, suggestion pills, borderless input
- [x] Pricing page — three-tier layout with feature comparison table
- [x] Homepage hero — stats row, orbit illustration, strong value prop
- [x] LoginDisplay — profile dropdown with avatar, theme selector, account links
- [x] Footer — updated logo, social icons, trust badges

---

## 🟠 Pre-Launch Polish

- [ ] Set up Google Analytics 4 — add `G-XXXXXXXXXX` to `wwwroot/index.html`
- [ ] Verify Google Search Console domain ownership via Namecheap TXT
- [ ] Submit sitemap to Google Search Console (`pdftoolstack.com/sitemap.xml`)
- [ ] Verify Microsoft Clarity fires on production after deploy
- [ ] Create Auth0 M2M app (`PdfToolStack Management`) for GDPR `delete:users` flow
- [ ] Add `Auth0__ManagementClientId` + `Auth0__ManagementClientSecret` to Azure
- [ ] Add Application Insights instrumentation key to API `Program.cs`
- [ ] Set up Azure alerts — failed requests > 5%, response time > 2s, availability < 99%
- [ ] Enable Always On on `pdftoolstack-api` App Service
- [ ] Enable HTTPS Only on `pdftoolstack-api` App Service
- [ ] Check App Service plan tier — B1 vs B2 for launch traffic
- [ ] Stripe branding — upload logo, set `#2250F4` brand color, `#0D9488` accent
- [ ] Add Auth0 application logo (after deploy: `https://pdftoolstack.com/logo.png`)
- [ ] Verify Resend MX + SPF turn green after DNS propagation
- [ ] Test send from Resend dashboard confirms delivery
- [ ] Namecheap email forwarding — forward `support@pdftoolstack.com` to Gmail
- [ ] Update blog articles with images
- [ ] Complete full test plan (auth, free tier, AI tools, payments, addons, teams, email, mobile)

---

## 🟢 Build Now — Parallel to Launch Prep

- [ ] Chrome extension — process PDFs directly from browser
- [ ] Multi-party e-signature requests (send + track)
- [ ] Real-time collaboration for Teams tier
- [ ] Intent-based SEO landing pages (5 pages)
- [ ] ExcelToolStack.com companion product
- [ ] PDF Repair tool
- [ ] Invoice schema memory — AI remembers layout per customer (retention hook)
- [ ] AI credit top-up above monthly limit (usage-based upsell)
- [ ] Draft Show HN post (publish on launch day)
- [ ] Draft Indie Hackers launch post
- [ ] Write 3 Medium articles (draft now, publish after launch)
- [ ] Set up LinkedIn profile and draft first 5 posts
- [ ] Write 10 YouTube video scripts

---

## 🔵 Post-Launch — After First Users

- [ ] Apply to Google AdSense (~500 daily visitors required first)
- [ ] Submit to AlternativeTo, SaaSHub, Capterra, G2
- [ ] Cold outreach to 50 accounting firms
- [ ] Publish Show HN post
- [ ] Publish Indie Hackers post
- [ ] Publish Medium articles
- [ ] Start Reddit comment strategy
- [ ] Start posting on LinkedIn
- [ ] Product Hunt launch (week 2 after launch)
- [ ] Launch affiliate program ($1K MRR milestone)
- [ ] Integration partnership outreach (Zapier, Wave, Clio)
- [ ] Start YouTube channel and upload first video
- [ ] ISO 27001 certification ($50K ARR milestone)

---

## 📋 Known Issues — Not Blocking Launch

- [ ] OneDrive OAuth `token_failed` — deprioritized, Google Drive + Dropbox work
- [ ] `ClosedXML 0.102.3` requires `DocumentFormat.OpenXml < 3.0.0` but 3.0.0 resolved — non-breaking warning
- [ ] `CA1416` Windows-only warnings in `PdfToJpgProcessor` and `PdfOcrProcessor` — Azure runs Windows, not blocking
- [ ] `CS1998` async without await in `AnnotationController` and `AiController` — cosmetic only