# PdfToolStack – Project Context

## 🧠 Overview
PdfToolStack is a SaaS web application that provides PDF tools (compress, merge, convert, edit, AI tools) with a focus on solving real user problems rather than offering generic utilities.

The goal is to monetize through:
- Pay-per-use (microtransactions)
- Subscription (premium tier)
- Ads (secondary)

---

## 🎯 Product Strategy

This project follows a **problem-first approach**, not a tool-first approach.

Instead of:
- "Compress PDF"

We position tools as:
- "Fix PDF too large for email"
- "Convert PDF to editable Word"
- "Merge PDFs for submission"

Each tool is mapped to:
- A real-world problem
- A high-intent search query
- A conversion opportunity

---

## 💰 Monetization Model

### Free Tier
- Up to 10MB file size
- Basic processing
- Ads enabled

### Pay-Per-Use
- $0.99 → up to 50MB
- $1.99 → up to 200MB
- $2.99 → up to 500MB

### Subscription ($9.99/month)
- Unlimited file size
- Faster processing
- No ads
- Access to AI tools
- Batch processing

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

## 🧠 Smart Detection System

The system analyzes uploaded files to detect issues:

Examples:
- File too large for email
- Scanned PDF (needs OCR)
- Multi-document merge scenario

This is used to:
- Personalize UX
- Increase conversion
- Suggest correct tool automatically

---

## 🧰 Core Tools (Current)

### Compress & Convert
- Fix PDF too large for email (Compress)
- Convert PDF to editable Word
- Extract tables to Excel
- Convert images to PDF

### Edit & Annotate
- Edit PDF content
- Add annotations
- Fill & sign forms

### Organize
- Merge PDFs
- Split PDFs
- Extract pages
- Remove pages
- Rotate pages
- Add page numbers

### Security
- Add password protection
- Remove password
- Redact sensitive data

### AI Tools
- Summarize PDF
- Ask questions about PDF
- Extract key insights

---

## 🚧 Planned Features

High priority:
- PDF Compare (highlight differences)
- PDF Repair (fix corrupted files)
- Email PDF Optimizer (auto-size for Gmail/Outlook)

Medium priority:
- Batch processing
- Cloud integrations (Google Drive, Dropbox)
- PDF Analyzer (file insights)

---

## 🧱 Tech Stack

### Frontend
- Blazor WebAssembly

### Backend
- ASP.NET Core API

### Storage
- Azure Blob Storage

### Payments
- Stripe

---

## 📁 Project Structure

- PdfToolStack.Web → frontend UI
- PdfToolStack.API → backend API
- PdfToolStack.Application → business logic
- PdfToolStack.Domain → core models
- PdfToolStack.Infrastructure → data + services

---

## 🚨 Important Decisions (DO NOT CHANGE)

- Problem-first UX is required
- Free tier must always exist
- Paid option must always be shown alongside free option
- No hard paywalls without alternative path

---

## 🎯 SEO Strategy

Each tool has multiple entry points:

Examples:
- /compress-for-email
- /compress-under-1mb
- /merge-for-submission

Each maps to:
- A specific user problem
- A blog article
- A conversion funnel

---

## 🧠 Key Philosophy

We are NOT building:
> a PDF tool website

We ARE building:
> a problem-solving platform that converts users into paying customers

---

## 🧪 Future AI Context

If working with AI:
- Prioritize conversion over features
- Optimize UX flows before adding tools
- Suggest improvements in monetization and positioning