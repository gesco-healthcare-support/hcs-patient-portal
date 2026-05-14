[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $SourceDocx,

    [Parameter(Mandatory = $true)]
    [string] $OutputPdf,

    [Parameter()]
    [hashtable] $TokenReplacements,

    # Path to a PNG image to stamp at the ##Appointments.Signature##
    # placeholder. When supplied, the placeholder text is removed and
    # an inline image is inserted in its place (mirrors OLD
    # InsertAPicture at AppointmentDocumentDomain.cs:954-968).
    # Optional -- pass $null to skip signature stamping.
    [Parameter()]
    [string] $SignatureImagePath,

    # Optional: when supplied, the post-replacement DOCX (intermediate
    # output that OLD's flow stops at) is also saved here. Useful for
    # side-by-side review of the DOCX vs the PDF render.
    [Parameter()]
    [string] $OutputIntermediateDocx
)

# Phase 1 POC: Open OLD DOCX in Microsoft Word via COM, optionally
# replace ##Token## placeholders via Word's native Find / Replace
# (which handles split-runs across <w:t> elements automatically),
# export as PDF using Word's built-in PDF exporter. Output is the
# same path Word's "File > Save As > PDF" UI produces, because it
# IS Word doing the conversion.
#
# This mirrors the OLD app's intent (token replacement + Word render)
# but automates the SaveAs step. Production parity will swap Word for
# LibreOffice headless on the Linux server container; for the POC,
# Word is the gold-standard renderer to compare against.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $SourceDocx)) {
    throw "Source DOCX not found: $SourceDocx"
}

$resolvedSource = (Resolve-Path -LiteralPath $SourceDocx).ProviderPath

# Copy to a temp file so token replacement does not touch the source.
$tempDir = Join-Path $env:TEMP ("docx-pdf-poc-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempDir | Out-Null
$tempDocx = Join-Path $tempDir (Split-Path -Leaf $resolvedSource)
Copy-Item -LiteralPath $resolvedSource -Destination $tempDocx

# Ensure output directory exists.
$outputDir = Split-Path -Parent $OutputPdf
if ($outputDir -and -not (Test-Path -LiteralPath $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$word = $null
$doc = $null
$replacedCount = 0

try {
    $word = New-Object -ComObject Word.Application
    $word.Visible = $false
    $word.DisplayAlerts = 0  # wdAlertsNone

    # Open the temp DOCX (read-only false so SaveAs works without touching the source).
    # Constants: ReadOnly=$false, AddToRecentFiles=$false, Visible=$false
    $doc = $word.Documents.Open($tempDocx, $false, $false, $false)

    if ($TokenReplacements -and $TokenReplacements.Count -gt 0) {
        # Use Word's native Find / Replace via Range.Find. This handles
        # split-runs (a single visible token broken across multiple <w:t>
        # elements) natively because Word resolved the document into its
        # internal model on Open.
        # Mirrors OLD AppointmentDocumentDomain.cs:865-952 token-replacement
        # behavior: every value already comes through the resolver
        # ToUpper'd (per :1070), so we just substitute verbatim.
        $find = $doc.Content.Find
        $find.ClearFormatting()
        $find.Replacement.ClearFormatting()
        $find.MatchCase = $true
        $find.MatchWholeWord = $false
        $find.MatchWildcards = $false
        $find.Forward = $true
        $find.Wrap = 1                     # wdFindContinue
        $find.Format = $false

        $wdReplaceAll = 2

        foreach ($key in $TokenReplacements.Keys) {
            $needle = "##$key##"
            $replacement = [string] $TokenReplacements[$key]

            # Word's Find / Replace silently no-ops when the replacement
            # is empty; insert a single space then trim if you ever need
            # an empty replacement. For the POC every value is non-empty.

            $find.Text = $needle
            $find.Replacement.Text = $replacement
            $applied = $find.Execute(
                $needle,           # FindText
                $true,             # MatchCase
                $false,            # MatchWholeWord
                $false,            # MatchWildcards
                $false,            # MatchSoundsLike
                $false,            # MatchAllWordForms
                $true,             # Forward
                1,                 # Wrap = wdFindContinue
                $false,            # Format
                $replacement,      # ReplaceWith
                $wdReplaceAll
            )

            # Find.Execute returns $true / $false depending on whether at
            # least one replacement happened. We can't get a count from
            # Word directly, so increment by 1 if it returned true.
            if ($applied) {
                $replacedCount++
                Write-Verbose "Replaced token: $needle -> $replacement"
            }
        }
    }

    # Mirrors OLD InsertAPicture at AppointmentDocumentDomain.cs:954-968:
    # locate the ##Appointments.Signature## placeholder, remove the
    # text, insert the user's signature image as an inline shape at the
    # matched range. OLD uses a fixed 880000 EMU (~0.96 in) square.
    # Word's InlineShapes.AddPicture defaults to the image's intrinsic
    # size; we resize to roughly OLD's dimensions after insertion.
    $signaturePlaced = $false
    if ($SignatureImagePath -and (Test-Path -LiteralPath $SignatureImagePath)) {
        $resolvedSignature = (Resolve-Path -LiteralPath $SignatureImagePath).ProviderPath
        $sigRange = $doc.Content
        $sigFind = $sigRange.Find
        $sigFind.ClearFormatting()
        $sigFind.Text = "##Appointments.Signature##"
        $sigFind.MatchCase = $true
        $sigFind.MatchWholeWord = $false
        $sigFind.MatchWildcards = $false
        $sigFind.Forward = $true
        $sigFind.Wrap = 1   # wdFindContinue

        if ($sigFind.Execute()) {
            # After Execute returns true, $sigRange is reduced to the
            # matched text. Clear it, then insert the picture at the
            # collapsed range.
            $sigRange.Text = ""
            $shape = $doc.InlineShapes.AddPicture($resolvedSignature, $false, $true, $sigRange)
            # Resize to roughly match OLD's 880000 EMU square
            # (880000 EMU = 0.917 in -> 66 pt at 72 dpi).
            $shape.LockAspectRatio = 0  # msoFalse
            $shape.Width = 90
            $shape.Height = 35
            $signaturePlaced = $true
        }
    }

    # Save the intermediate DOCX (post-token-replacement, post-signature)
    # if requested. This is the artifact OLD produces -- our new step is
    # the PDF conversion that follows.
    if ($OutputIntermediateDocx) {
        $absoluteIntermediate = if ([System.IO.Path]::IsPathRooted($OutputIntermediateDocx)) {
            $OutputIntermediateDocx
        } else {
            Join-Path (Get-Location).ProviderPath $OutputIntermediateDocx
        }
        $intermediateDir = Split-Path -Parent $absoluteIntermediate
        if ($intermediateDir -and -not (Test-Path -LiteralPath $intermediateDir)) {
            New-Item -ItemType Directory -Path $intermediateDir -Force | Out-Null
        }
        # wdFormatDocumentDefault = 16 (DOCX)
        $wdFormatDocx = 16
        $doc.SaveAs([ref] $absoluteIntermediate, [ref] $wdFormatDocx)
    }

    # SaveAs PDF. wdFormatPDF = 17.
    $wdFormatPDF = 17
    $absoluteOut = if ([System.IO.Path]::IsPathRooted($OutputPdf)) {
        $OutputPdf
    } else {
        Join-Path (Get-Location).ProviderPath $OutputPdf
    }
    $doc.SaveAs([ref] $absoluteOut, [ref] $wdFormatPDF)
}
finally {
    if ($doc) {
        try { $doc.Close($false) } catch { }
        [System.Runtime.InteropServices.Marshal]::ReleaseComObject($doc) | Out-Null
    }
    if ($word) {
        try { $word.Quit() } catch { }
        [System.Runtime.InteropServices.Marshal]::ReleaseComObject($word) | Out-Null
    }
    [System.GC]::Collect()
    [System.GC]::WaitForPendingFinalizers()

    if (Test-Path -LiteralPath $tempDir) {
        Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$intermediateDocxBytes = if ($OutputIntermediateDocx -and (Test-Path -LiteralPath $absoluteIntermediate)) {
    (Get-Item -LiteralPath $absoluteIntermediate).Length
} else { 0 }

[pscustomobject] @{
    SourceDocx           = $resolvedSource
    OutputIntermediateDocx = if ($OutputIntermediateDocx) { $absoluteIntermediate } else { $null }
    IntermediateDocxBytes = $intermediateDocxBytes
    OutputPdf            = $absoluteOut
    OutputPdfBytes       = (Get-Item -LiteralPath $absoluteOut).Length
    TokensReplaced       = $replacedCount
    SignaturePlaced      = $signaturePlaced
}
