# PdfToolStack — Full Test Plan

## Environment
- Local: localhost:7025 (Blazor) + localhost:5XXX (API)
- Staging: pdftoolstack-staging.azurewebsites.net
- Production: pdftoolstack.com
- Test user: google-oauth2|108268775305485144737 (admin bypass = Pro)

---

## 1. AUTHENTICATION & ACCOUNT

| Test | Steps | Expected |
|------|-------|----------|
| Google sign in | Click Sign In → Google | Redirects to Google, returns authenticated |
| GitHub sign in | Click Sign In → GitHub | Redirects to GitHub, returns authenticated |
| Profile dropdown | Click avatar chip | Dropdown shows name, email, avatar photo |
| Theme switcher | Click Light / Dark / System in dropdown | Theme changes immediately |
| My Account link | Click My Account in dropdown | Navigates to /account |
| My Documents link | Click My Documents in dropdown | Navigates to /account#documents |
| Logout | Click Logout in dropdown | Signs out, redirects to home |
| Admin Pro bypass | Sign in with admin account | Account page shows Pro badge, Pro plan card |
| Profile picture | Sign in with Google | Google avatar shows in navbar chip and account page |
| Initials fallback | Sign in with no picture | Shows correct initials (CP not CH) |

---

## 2. HOMEPAGE

| Test | Steps | Expected |
|------|-------|----------|
| Hero loads | Visit / | No blue rectangle focus ring, hero renders cleanly |
| Stats row | View hero | 35+, AI, $0, 1hr stats show in pill |
| Trust bar | Scroll below hero | Single trust bar shows 4 items |
| Tool search | Type "compress" in search | Compress tool cards appear filtered |
| AI tools section | Scroll to tools | AI tools grid shows with Pro badge |
| PDF tools grid | Scroll further | All tool categories render with colored icons |
| CTA buttons | Click "Try free — no signup" | Scrolls to tools or stays on page |
| "See Pro plans" | Click secondary CTA | Navigates to /pricing |

---

## 3. PDF TOOLS — CORE

Test each with a sample PDF. Download and verify output opens correctly.

| Tool | URL | Test |
|------|-----|------|
| Compress PDF | /compress-pdf | Upload 2MB PDF → download compressed → verify smaller |
| Merge PDF | /merge-pdf | Upload 2 PDFs → download merged → verify pages from both |
| Split PDF | /split-pdf | Upload 3-page PDF → split all → download ZIP → verify 3 files |
| PDF to Word | /pdf-to-word | Upload PDF → download DOCX → open in Word |
| Word to PDF | /word-to-pdf | Upload DOCX → download PDF → verify content |
| PDF to JPG | /pdf-to-jpg | Upload 2-page PDF → download ZIP → verify 2 JPG images |
| JPG to PDF | /jpg-to-pdf | Upload 2 JPGs → download PDF → verify both images |
| PDF to Excel | /pdf-to-excel | Upload PDF with table → download XLSX → verify data |
| Rotate PDF | /rotate-pdf | Upload PDF → rotate 90° → download → verify rotation |
| Delete Pages | /delete-pdf-pages | Upload 3-page PDF → delete page 2 → verify 2 pages remain |
| Extract Pages | /extract-pdf-pages | Upload 5-page PDF → extract pages 2-4 → verify 3 pages |
| Organize PDF | /organize-pdf | Upload PDF → drag pages to reorder → download → verify order |
| Protect PDF | /protect-pdf | Upload PDF → add password → download → verify password required |
| Unlock PDF | /unlock-pdf | Upload password-protected PDF → unlock → download → verify no password |
| Watermark | /watermark-pdf | Upload PDF → add text watermark → download → verify watermark visible |
| Number Pages | /number-pages | Upload PDF → add page numbers → download → verify numbers visible |
| Flatten PDF | /flatten-pdf | Upload form PDF → flatten → download → verify fields uneditable |
| Redact PDF | /redact-pdf | Upload PDF → redact text → download → verify text gone |
| Fill Form | /fill-pdf-form | Upload form PDF → fill fields → download → verify filled |
| Sign PDF | /sign-pdf | Upload PDF → add signature → download → verify signature visible |
| Annotate PDF | /pdf-annotator | Upload PDF → add highlight → download → verify annotation |
| Crop PDF | /crop-pdf | Upload PDF → crop margins → download → verify cropped |

---

## 4. CONVERSION TOOLS

| Tool | URL | Test |
|------|-----|------|
| PPT to PDF | /ppt-to-pdf | Upload PPTX → download PDF → verify slides |
| Excel to PDF | /excel-to-pdf | Upload XLSX → download PDF → verify data |
| PDF OCR | /pdf-ocr | Upload scanned PDF → download → open → Ctrl+F search for a word → verify searchable |

---

## 5. AI TOOLS (Pro required)

Sign in with admin account before testing these.

| Tool | URL | Test |
|------|-----|------|
| Extract Invoice Data | /extract-invoice-data | Upload invoice PDF → verify fields extracted → download Excel |
| Chat with PDF | /chat-with-pdf | Upload PDF → ask a question → verify relevant answer |
| AI Summarizer | /ai-summarizer | Upload PDF → verify summary generated |
| AI Questions | /ai-questions | Upload PDF → verify questions generated |
| Compare PDFs | /compare-pdf | Upload 2 versions of a doc → verify diff report shows changes |
| Contract Reviewer | /review-contract | Upload NDA → verify risk level, clauses, dates all populate |

---

## 6. BATCH PROCESSING (Pro required)

| Test | Steps | Expected |
|------|-------|----------|
| Batch compress | Upload 3 PDFs, select Compress | Downloads ZIP with 3 compressed files |
| Batch merge | Not applicable | Merge uses separate endpoint |
| Error handling | Upload 1 valid + 1 invalid file | ZIP downloads with valid file + batch_errors.txt |
| File limit | Try uploading 21 files | Error: maximum 20 files |

---

## 7. ACCOUNT DASHBOARD

| Test | Steps | Expected |
|------|-------|----------|
| Stats cards | Process a tool then visit /account | Document count increments |
| History list | Process 3 tools | Recent documents list shows all 3 with correct tool name |
| Relative timestamps | Check history items | Shows "2m ago", "1h ago" not raw dates |
| Tool color icons | Check history items | Each tool type has correct color icon |
| Pro subscription card | View subscription section | Shows teal Pro card, renews April 2036 |
| Manage Billing | Click Manage Billing | Redirects to Stripe portal (requires real subscription) |
| Quick links | Click each quick link | All 5 navigate correctly |
| Empty state | Sign in with fresh account | Shows empty state with Browse Tools button |
| Browse Tools button | Click Browse Tools | Navigates to / |

---

## 8. PRICING PAGE

| Test | Steps | Expected |
|------|-------|----------|
| Three tiers display | Visit /pricing | Free, Pro, Teams cards all render |
| Pro subscribe button | Click Subscribe on Pro | Redirects to Stripe checkout |
| Feature comparison | Scroll to comparison table | Table shows all features correctly |
| Audience section | Scroll to professionals section | 4 cards show: Accountants, Paralegals, Real Estate, HR |
| FAQ section | Scroll to FAQ | Questions expand/collapse or display correctly |

---

## 9. BLOG

| Test | Steps | Expected |
|------|-------|----------|
| Blog index | Visit /blog | 6 article cards render |
| Article 1 | Click "How to Extract Invoice Data" | Article loads, related links work |
| Article 2 | Click "Best PDF Tools for Accountants" | Article loads, all tool links work |
| Article 3 | Click "How to Compress PDF for Email" | Article loads |
| Article 4 | Click "How to Review Contract with AI" | Article loads |
| Article 5 | Click "PDF Tools for Paralegals" | Article loads |
| Article 6 | Click "How to Make Scanned PDF Searchable" | Article loads |
| CTA links | Click CTA button in each article | Links to correct tool page |
| Back link | Click ← Back to Blog | Returns to /blog |
| Related links | Click related article links | Navigate correctly |

---

## 10. EMAIL (Resend)

| Test | Steps | Expected |
|------|-------|----------|
| Contact form | Visit /contact → submit form | You receive email at hello@pdftoolstack.com |
| Waitlist form | Visit a Coming Soon page → submit email | User receives waitlist confirmation email |
| Pro welcome | Complete Stripe checkout (test mode) | User receives Pro welcome email |

---

## 11. SECURITY

| Test | Steps | Expected |
|------|-------|----------|
| Swagger on staging | Visit staging-api-url/swagger | Returns 404 |
| Security headers | DevTools → Network → any API response → Response Headers | X-Content-Type-Options, X-Frame-Options, CSP all present |
| AI rate limiting | Call /api/ai/extract 21 times quickly | 21st request returns 429 with Retry-After: 3600 |
| PDF rate limiting | Call /api/pdf/process 11 times quickly | 11th request returns 429 |
| File validation | Upload a .txt renamed as .pdf | Returns 400 with error message |
| Pro gate | Visit /extract-invoice-data signed out | Shows upgrade card, not the tool |
| Pro gate signed in free | Visit /review-contract on free account | Shows upgrade card |

---

## 12. NAVIGATION & FOOTER

| Test | Steps | Expected |
|------|-------|----------|
| Logo | Click logo | Navigates to / |
| NavMenu links | Click each nav item | Correct page loads |
| Mobile menu | Resize to mobile → click hamburger | Menu opens with all items |
| Footer logo | Check footer | New SVG logo renders with "Tool" in blue |
| Social sign-in icons | Check footer | 5 icon circles render (Google, GitHub, Microsoft, Facebook, LinkedIn) |
| Footer links | Click each footer link | Correct page loads |
| Trust badges | Check footer | 5 badges: SSL, Auto Delete, Privacy First, No Data Mining, AI Powered |
| Dark mode | Toggle dark mode | All pages render correctly in dark mode |

---

## 13. MOBILE RESPONSIVENESS

Test on mobile or using Chrome DevTools device emulation (375px width).

| Test | Expected |
|------|----------|
| Homepage hero | Stats row wraps to 2x2 grid |
| Tool cards grid | Single column layout |
| Account dashboard | Panels stack vertically |
| Pricing page | Cards stack vertically |
| Blog grid | Single column |
| Navbar | Hamburger menu shows |
| Profile dropdown | Dropdown fits on screen |

---

## PRE-LAUNCH CHECKLIST

- [ ] All core PDF tools pass test
- [ ] All AI tools pass test (with admin account)
- [ ] Contract Reviewer renders full report
- [ ] PDF OCR produces searchable output
- [ ] Email sending confirmed (contact form)
- [ ] Staging Swagger returns 404
- [ ] Security headers present on staging
- [ ] Rate limiting returns 429
- [ ] Blog all 6 articles load
- [ ] Mobile layout verified
- [ ] Dark mode verified on all key pages
- [ ] Admin bypass working (Pro shows for admin user)
- [ ] Stripe checkout flow working in test mode
- [ ] All footer links work
- [ ] No console errors on homepage
- [ ] Azure App Service config has all production keys

---

## TEST FILES NEEDED

- `sample_invoice.pdf` — a real invoice with vendor, amounts, line items
- `sample_contract.pdf` — an NDA or service agreement
- `scanned_document.pdf` — an image-based PDF (no selectable text)
- `sample_form.pdf` — a PDF with fillable form fields
- `sample_presentation.pptx` — a PowerPoint file
- `sample_spreadsheet.xlsx` — an Excel file
- `sample_word.docx` — a Word document
- `sample_image.jpg` — a JPG photo
- `protected_document.pdf` — a password-protected PDF