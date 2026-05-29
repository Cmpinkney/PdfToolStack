# ============================================================
# prerender.ps1
# Place at: PdfToolStack.Web/prerender/prerender.ps1
# Run after dotnet publish in GitHub Actions
# Injects per-route static content into index.html copies
# so Google indexes real page content for each URL
# ============================================================

param(
    [string]$PublishDir = "publish_output/wwwroot"
)

Write-Host "Starting static prerender for $PublishDir..."

# ============================================================
# ROUTE DEFINITIONS
# Each route gets its own index.html copy with injected content
# title       = <title> tag + og:title
# description = <meta description> + og:description  
# h1          = replaces the generic h1 in the loading shell
# body        = replaces the generic p in the loading shell
# links       = nav links shown in loading shell (internal links = crawl signal)
# ============================================================

$routes = @(
    @{
        path        = "compress-pdf"
        title       = "Compress PDF Online Free — Reduce PDF File Size | PdfToolStack"
        description = "Compress PDF files online for free. Reduce PDF size for email, sharing, or uploading. No signup required. Files deleted in 1 hour."
        h1          = "Compress PDF — Reduce file size instantly."
        body        = "Upload your PDF and compress it for email, web, or storage. No quality loss. Files processed securely and deleted within 1 hour."
        links       = @("/merge-pdf", "/split-pdf", "/pdf-to-word", "/compress-pdf")
    },
    @{
        path        = "merge-pdf"
        title       = "Merge PDF Files Online Free — Combine PDFs | PdfToolStack"
        description = "Merge multiple PDF files into one online for free. Combine PDFs in seconds. No signup required. Files deleted in 1 hour."
        h1          = "Merge PDF — Combine multiple PDFs into one."
        body        = "Upload multiple PDF files and merge them into a single document. Drag to reorder pages. Free, fast, and private."
        links       = @("/merge-pdf", "/split-pdf", "/compress-pdf", "/pdf-to-word")
    },
    @{
        path        = "split-pdf"
        title       = "Split PDF Online Free — Extract PDF Pages | PdfToolStack"
        description = "Split a PDF into multiple files online for free. Extract specific pages or split by range. No signup required."
        h1          = "Split PDF — Extract pages from any PDF."
        body        = "Upload a PDF and split it into individual pages or custom page ranges. Download as separate files or a ZIP."
        links       = @("/split-pdf", "/merge-pdf", "/compress-pdf", "/extract-pages-pdf")
    },
    @{
        path        = "extract-invoice-data"
        title       = "Extract Invoice Data from PDF to Excel — AI Invoice Extractor | PdfToolStack"
        description = "Extract invoice data from PDF automatically. AI pulls vendor, amount, date, and line items into structured Excel data in seconds."
        h1          = "AI Invoice Data Extractor — PDF invoice to Excel in 10 seconds."
        body        = "Upload any PDF invoice and our AI extracts vendor name, invoice number, date, line items, and totals into structured data ready for Excel or your accounting system."
        links       = @("/extract-invoice-data", "/review-contract", "/chat-with-pdf", "/pdf-to-excel")
    },
    @{
        path        = "review-contract"
        title       = "AI Contract Reviewer — Review PDF Contracts with AI | PdfToolStack"
        description = "Review contracts with AI. Highlights risky clauses, key dates, obligations, and missing terms in any PDF contract. Pro feature."
        h1          = "AI Contract Reviewer — Catch risky clauses before you sign."
        body        = "Upload a PDF contract and our AI reviews it for risky language, key dates, obligations, and missing standard clauses. Designed for paralegals, small business owners, and legal teams."
        links       = @("/review-contract", "/extract-invoice-data", "/chat-with-pdf", "/redact-pdf")
    },
    @{
        path        = "sign-pdf"
        title       = "Sign PDF Online Free — Electronic Signature | PdfToolStack"
        description = "Sign PDF documents online for free. Draw, type, or upload your signature. Place it anywhere on the document. No signup required."
        h1          = "Sign PDF — Add your signature to any PDF."
        body        = "Draw your signature, type it, or upload an image. Place it anywhere on your PDF document and download the signed file instantly."
        links       = @("/sign-pdf", "/fill-pdf-form", "/merge-pdf", "/compress-pdf")
    },
    @{
        path        = "pdf-to-word"
        title       = "PDF to Word Converter Online Free — Convert PDF to Editable Word | PdfToolStack"
        description = "Convert PDF to editable Word document online. Preserves formatting, tables, and layout. Free, fast, no signup required."
        h1          = "PDF to Word — Convert PDF to editable Word document."
        body        = "Upload a PDF and convert it to an editable Microsoft Word document. Formatting, tables, and layout preserved. Download your .docx file instantly."
        links       = @("/pdf-to-word", "/word-to-pdf", "/pdf-to-excel", "/compress-pdf")
    },
    @{
        path        = "word-to-pdf"
        title       = "Word to PDF Converter Online Free — Convert Word to PDF | PdfToolStack"
        description = "Convert Word documents to PDF online for free. Upload .docx or .doc files and download a perfect PDF. No signup required."
        h1          = "Word to PDF — Convert Word documents to PDF instantly."
        body        = "Upload a .docx or .doc file and convert it to a professional PDF. Perfect formatting every time. Free, fast, secure."
        links       = @("/word-to-pdf", "/pdf-to-word", "/merge-pdf", "/compress-pdf")
    },
    @{
        path        = "batch"
        title       = "Batch Process PDFs — Apply Tools to Multiple PDFs at Once | PdfToolStack"
        description = "Batch process multiple PDFs at once. Apply compress, merge, convert, or other tools to up to 20 PDFs simultaneously. Pro feature."
        h1          = "Batch PDF Processing — Process up to 20 PDFs at once."
        body        = "Upload up to 20 PDF files and apply any tool — compress, convert, redact, watermark — to all of them in one operation. Download results as a ZIP file."
        links       = @("/batch", "/compress-pdf", "/merge-pdf", "/redact-pdf")
    },
    @{
        path        = "compare-pdf"
        title       = "Compare PDF Files Online — PDF Diff Tool | PdfToolStack"
        description = "Compare two PDF documents and see exactly what changed. Word-level diff with highlighted changes. Pro feature."
        h1          = "PDF Compare — See exactly what changed between two documents."
        body        = "Upload two PDF versions and our comparison tool highlights every addition, deletion, and change at the word level. Perfect for contract revisions and document review."
        links       = @("/compare-pdf", "/review-contract", "/merge-pdf", "/split-pdf")
    },
    @{
        path        = "redact-pdf"
        title       = "Redact PDF Online Free — Remove Sensitive Information | PdfToolStack"
        description = "Redact sensitive information from PDF files online. Permanently remove text, images, and data. Free tool, no signup required."
        h1          = "Redact PDF — Permanently remove sensitive information."
        body        = "Draw redaction boxes over sensitive text, account numbers, signatures, or any private information. Permanently removes the content from the PDF."
        links       = @("/redact-pdf", "/compress-pdf", "/merge-pdf", "/sign-pdf")
    },
    @{
        path        = "pricing"
        title       = "PdfToolStack Pricing — Free, Pro, Teams, and Bundle Plans"
        description = "PdfToolStack pricing plans. Free tier with all tools. Pro at \$19/month with AI tools and batch processing. Teams at \$59/month. Bundle with ExcelToolStack at \$29/month."
        h1          = "Simple, transparent pricing for PDF and document workflows."
        body        = "Start free with all 35+ PDF tools. Upgrade to Pro for AI-powered document analysis, batch processing, and unlimited file sizes. Teams and Bundle plans available."
        links       = @("/pricing", "/compress-pdf", "/extract-invoice-data", "/review-contract")
    },
    @{
        path        = "chat-with-pdf"
        title       = "Chat with PDF — Ask Questions About Any PDF Document | PdfToolStack"
        description = "Chat with any PDF document using AI. Ask questions, get summaries, extract key information. Upload your PDF and start chatting instantly."
        h1          = "Chat with PDF — Ask your document anything."
        body        = "Upload any PDF and ask questions about its content. Our AI reads the document and answers your questions, extracts key points, and summarizes complex sections."
        links       = @("/chat-with-pdf", "/ai-summarizer", "/review-contract", "/extract-invoice-data")
    },
    @{
        path        = "ai-summarizer"
        title       = "AI PDF Summarizer — Summarize Any PDF in Seconds | PdfToolStack"
        description = "Summarize any PDF document with AI. Get key points, executive summaries, and structured outlines from any PDF. Pro feature."
        h1          = "AI PDF Summarizer — Get the key points from any document."
        body        = "Upload a PDF and our AI generates a structured summary with key points, main arguments, and important details. Perfect for research papers, reports, and long contracts."
        links       = @("/ai-summarizer", "/chat-with-pdf", "/review-contract", "/extract-invoice-data")
    },
    @{
        path        = "pdf-ocr"
        title       = "PDF OCR — Make Scanned PDFs Searchable | PdfToolStack"
        description = "Convert scanned PDFs to searchable text with OCR. Extract text from image-based PDFs. Free tool, accurate results."
        h1          = "PDF OCR — Make scanned documents searchable."
        body        = "Upload a scanned PDF and our OCR engine extracts all the text, making the document fully searchable and copyable. Supports multi-page documents."
        links       = @("/pdf-ocr", "/chat-with-pdf", "/extract-invoice-data", "/pdf-to-word")
    },
    @{
        path        = "watermark-pdf"
        title       = "Add Watermark to PDF Online Free | PdfToolStack"
        description = "Add text or image watermarks to PDF files online for free. Customize position, opacity, and font. No signup required."
        h1          = "Watermark PDF — Add text or image watermarks to any PDF."
        body        = "Upload a PDF and add a custom text or image watermark. Control opacity, position, rotation, and font size. Download the watermarked PDF instantly."
        links       = @("/watermark-pdf", "/redact-pdf", "/compress-pdf", "/sign-pdf")
    },
    @{
        path        = "lock-pdf"
        title       = "Password Protect PDF Online Free — Encrypt PDF | PdfToolStack"
        description = "Add password protection to PDF files online for free. Encrypt PDFs with 256-bit AES. No signup required."
        h1          = "Lock PDF — Password protect any PDF document."
        body        = "Upload a PDF and add a strong password to prevent unauthorized access. Uses AES-256 encryption. Share confidential documents safely."
        links       = @("/lock-pdf", "/unlock-pdf", "/redact-pdf", "/sign-pdf")
    },
    @{
        path        = "unlock-pdf"
        title       = "Remove PDF Password Online Free — Unlock PDF | PdfToolStack"
        description = "Remove password protection from PDF files online for free. Unlock encrypted PDFs instantly. No signup required."
        h1          = "Unlock PDF — Remove password protection from any PDF."
        body        = "Upload a password-protected PDF, enter the current password, and download the unlocked version. Works with owner and user passwords."
        links       = @("/unlock-pdf", "/lock-pdf", "/compress-pdf", "/merge-pdf")
    }
)

# ============================================================
# TEMPLATE — reads the base index.html and generates per-route copies
# ============================================================

$indexPath = Join-Path $PublishDir "index.html"
if (-not (Test-Path $indexPath)) {
    Write-Error "index.html not found at $indexPath"
    exit 1
}

$baseHtml = Get-Content $indexPath -Raw

foreach ($route in $routes) {
    $routePath = $route.path
    $routeDir  = Join-Path $PublishDir $routePath

    # Create directory for the route
    if (-not (Test-Path $routeDir)) {
        New-Item -ItemType Directory -Path $routeDir | Out-Null
    }

    # Build the nav links HTML
    $linkHtml = ($route.links | ForEach-Object {
        $label = ($_ -replace "^/", "" -replace "-", " ")
        $label = (Get-Culture).TextInfo.ToTitleCase($label)
        "                    <a href=`"$_`">$label</a>"
    }) -join "`n"

    # Clone index.html and inject per-route content
    $html = $baseHtml

    # 1. Replace <title>
    $html = $html -replace '<title>[^<]*</title>', "<title>$($route.title)</title>"

    # 2. Replace meta description
    $html = $html -replace 'content="PdfToolStack[^"]*"(\s+name="description")', "content=`"$($route.description)`"`$1"
    $html = $html -replace '(name="description"\s+content=")[^"]*"', "`${1}$($route.description)`""

    # 3. Replace og:title
    $html = $html -replace '(property="og:title"\s+content=")[^"]*"', "`${1}$($route.title)`""

    # 4. Replace og:description
    $html = $html -replace '(property="og:description"\s+content=")[^"]*"', "`${1}$($route.description)`""

    # 5. Replace canonical URL
    $html = $html -replace '(rel="canonical"\s+href=")[^"]*"', "`${1}https://pdftoolstack.com/$routePath`""

    # 6. Replace og:url
    $html = $html -replace '(property="og:url"\s+content=")[^"]*"', "`${1}https://pdftoolstack.com/$routePath`""

    # 7. Replace the h1 in the loading shell
    $html = $html -replace '<h1>PDF workflows are getting ready\.</h1>', "<h1>$($route.h1)</h1>"

    # 8. Replace the loading shell description paragraph
    $html = $html -replace 'PdfToolStack helps you compress, merge, split, convert, sign, redact,\s+OCR, and summarize PDFs online with practical document tools\.', $route.body

    # 9. Replace the startup nav links
    $newNav = "<nav class=`"startup-links`" aria-label=`"Popular PDF tools`">`n$linkHtml`n                </nav>"
    $html = $html -replace '<nav class="startup-links" aria-label="Popular PDF tools">[\s\S]*?</nav>', $newNav

    # Write the route-specific index.html
    $outputPath = Join-Path $routeDir "index.html"
    $html | Set-Content $outputPath -Encoding UTF8
    Write-Host "  Generated: /$routePath/index.html"
}

Write-Host ""
Write-Host "Prerender complete. Generated $($routes.Count) static route pages."
Write-Host "Google will now see page-specific content for each URL."
