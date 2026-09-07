<#
.SYNOPSIS
Auto-crop an image by trimming excess background in three ordered passes.

.DESCRIPTION
Pass 1 — Trim TOP:     Measures top offset from a dry-run full-trim, then chops that many rows.
Pass 2 — Trim LEFT:    Same measurement, chops columns from left.
Pass 3 — Trim BOTTOM + RIGHT: For grid/solid, masks the bottom-right 100x100 region first
                               (removes Gemini watermark sparkle), then trims remaining edges.
                               For transparent, trims by alpha directly.

Originals are moved to trash/ — never deleted.

.PARAMETER File
Absolute (or resolvable) path to the image file.

.PARAMETER Background
  grid        Gemini checkerboard. Two alternating grey shades (~86 dark / ~133 light, ~18% gap).
              Uses fuzz 25% to span both shades without touching warm banner content.
              Masks bottom-right 100x100 before trimming bottom/right (Gemini watermark region).
  transparent Alpha-channel background. Trims by alpha=0. No fuzz, no masking.
  solid       Uniform solid color. Samples top-left corner pixel. Uses fuzz 10%.
#>
param(
    [Parameter(Mandatory)]
    [string]$File,

    [Parameter(Mandatory)]
    [ValidateSet("grid", "transparent", "solid")]
    [string]$Background
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$trash = "C:\Users\meesles\Coding\project-avalon\trash"
$File  = (Resolve-Path $File).Path
$base  = [System.IO.Path]::GetFileNameWithoutExtension($File)
$ext   = [System.IO.Path]::GetExtension($File)
$dir   = [System.IO.Path]::GetDirectoryName($File)
$name  = [System.IO.Path]::GetFileName($File)

$step1 = Join-Path $dir "${base}-s1${ext}"
$step2 = Join-Path $dir "${base}-s2${ext}"
$final = Join-Path $dir "${base}-crop${ext}"

# Remove any stale temp files from a previous run
foreach ($tmp in @($step1, $step2, $final)) {
    if (Test-Path $tmp) { Remove-Item $tmp -Force }
}

# ── Background config ────────────────────────────────────────────────────────
switch ($Background) {
    "grid" {
        # Gemini checkerboard: two grey shades with up to ~24% gap → fuzz 35%
        # Shades vary by export (e.g. grey21 ~53 and grey45 ~114 seen in practice)
        $fuzz    = "35%"
        $bgColor = & magick identify -format "%[pixel:u.p{0,0}]" $File
    }
    "transparent" {
        $fuzz    = "0%"
        $bgColor = "none"
    }
    "solid" {
        $fuzz    = "10%"
        $bgColor = & magick identify -format "%[pixel:u.p{0,0}]" $File
    }
}

$origW = [int](& magick identify -format "%w" $File)
$origH = [int](& magick identify -format "%h" $File)

Write-Host ""
Write-Host "=== autocrop ==="
Write-Host "File:       $File"
Write-Host "Dimensions: ${origW}x${origH}"
Write-Host "Background: $Background  color=$bgColor  fuzz=$fuzz"
Write-Host ""

# ── Dry-run: measure top + left trim ────────────────────────────────────────
# The Gemini watermark is bottom-right, so it does not affect the top/left measurement.
$geomStr = & magick $File -fuzz $fuzz -trim -format "%wx%h%O" info:
Write-Host "Dry-run geometry: $geomStr"

$trimX = 0; $trimY = 0
if ($geomStr -match '(\d+)x(\d+)\+(\d+)\+(\d+)') {
    $trimX = [int]$Matches[3]   # columns to remove from left
    $trimY = [int]$Matches[4]   # rows to remove from top
}
Write-Host "  top=$trimY px  left=$trimX px"
Write-Host ""

# ── Pass 1: Trim top ─────────────────────────────────────────────────────────
if ($trimY -gt 0) {
    Write-Host "Pass 1: Chopping $trimY px from top..."
    & magick $File -gravity North -chop "0x${trimY}" $step1
} else {
    Write-Host "Pass 1: No top trim."
    Copy-Item $File $step1
}

# ── Pass 2: Trim left ────────────────────────────────────────────────────────
if ($trimX -gt 0) {
    Write-Host "Pass 2: Chopping $trimX px from left..."
    & magick $step1 -gravity West -chop "${trimX}x0" $step2
} else {
    Write-Host "Pass 2: No left trim."
    Copy-Item $step1 $step2
}
Remove-Item $step1 -Force -ErrorAction SilentlyContinue

# ── Pass 3: Mask Gemini watermark → trim bottom + right ─────────────────────
$W2 = [int](& magick identify -format "%w" $step2)
$H2 = [int](& magick identify -format "%h" $step2)
Write-Host "Pass 3: After top/left: ${W2}x${H2}"

if ($Background -eq "grid" -or $Background -eq "solid") {
    $maskX = $W2 - 100
    $maskY = $H2 - 100
    Write-Host "  Masking bottom-right 100x100 at (${maskX},${maskY}) with '$bgColor'..."
    & magick $step2 `
        -fill $bgColor `
        -draw "rectangle ${maskX},${maskY} ${W2},${H2}" `
        -fuzz $fuzz -trim +repage `
        $final
} else {
    Write-Host "  Trimming by alpha..."
    & magick $step2 -trim +repage $final
}
Remove-Item $step2 -Force -ErrorAction SilentlyContinue

# ── Verify ───────────────────────────────────────────────────────────────────
$newW = [int](& magick identify -format "%w" $final)
$newH = [int](& magick identify -format "%h" $final)

if ($newW -eq $origW -and $newH -eq $origH) {
    Write-Host ""
    Write-Host "No change — file left untouched."
    Remove-Item $final -Force -ErrorAction SilentlyContinue
    exit 0
}

# ── Replace original ─────────────────────────────────────────────────────────
Write-Host ""
Write-Host "Result: ${origW}x${origH}  →  ${newW}x${newH}"
Write-Host "  Removed: $($origW - $newW) px horizontal, $($origH - $newH) px vertical"
Write-Host "Moving original to trash/..."
$trashDest = Join-Path $trash $name
if (Test-Path $trashDest) {
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $trashDest = Join-Path $trash "${base}-${stamp}${ext}"
}
Move-Item $File $trashDest
Rename-Item $final $name
Write-Host "Done. Original in trash/  |  Updated: $File"
