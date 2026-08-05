param(
  [string[]]$Path = @(),
  [int]$MinLines = 3,
  [int]$Top = 0,
  [switch]$Full,
  [switch]$Strict
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

if ($Path.Count -eq 0) {
  $Path = @("sim", "client", "server", "tools", "tests")
}

$excludePattern = '[\\/](bin|obj|\.godot|addons|vendor|backup|node_modules)[\\/]'

$files = foreach ($p in $Path) {
  $target = if ([System.IO.Path]::IsPathRooted($p)) { $p } else { Join-Path $repoRoot $p }
  if (-not (Test-Path -LiteralPath $target)) { continue }
  if ((Get-Item -LiteralPath $target) -is [System.IO.FileInfo]) {
    Get-Item -LiteralPath $target
  } else {
    Get-ChildItem -LiteralPath $target -Recurse -File -Filter *.cs
  }
}

$files = $files | Where-Object { $_.FullName -notmatch $excludePattern } | Sort-Object FullName -Unique

$blocks = New-Object System.Collections.Generic.List[object]

foreach ($file in $files) {
  $lines = [System.IO.File]::ReadAllLines($file.FullName)
  $rel = $file.FullName.Substring($repoRoot.Length).TrimStart('\', '/')

  $inBlockComment = $false
  $runStart = -1
  $runLines = New-Object System.Collections.Generic.List[string]

  for ($i = 0; $i -le $lines.Length; $i++) {
    if ($i -eq $lines.Length) {
      # Sentinel pass so a block running to EOF still gets recorded.
      if ($runStart -ge 0 -and $runLines.Count -ge $MinLines) {
        $blocks.Add([pscustomobject]@{
            File  = $rel
            Start = $runStart
            End   = $runStart + $runLines.Count - 1
            Count = $runLines.Count
            Text  = $runLines.ToArray()
          })
      }
      break
    }

    $trimmed = $lines[$i].Trim()
    $isComment = $false

    if ($inBlockComment) {
      $isComment = $true
      if ($trimmed -match '\*/') {
        $inBlockComment = $false
        # Code trailing the block terminator means this line is not comment-only.
        if ($trimmed -notmatch '\*/\s*$') { $isComment = $false }
      }
    } elseif ($trimmed.StartsWith('//')) {
      $isComment = $true
    } elseif ($trimmed.StartsWith('/*')) {
      $isComment = $true
      if ($trimmed -notmatch '\*/') { $inBlockComment = $true }
    }

    if ($isComment) {
      if ($runStart -lt 0) { $runStart = $i + 1 }
      $runLines.Add($trimmed)
    } elseif ($runStart -ge 0) {
      if ($runLines.Count -ge $MinLines) {
        $blocks.Add([pscustomobject]@{
            File  = $rel
            Start = $runStart
            End   = $runStart + $runLines.Count - 1
            Count = $runLines.Count
            Text  = $runLines.ToArray()
          })
      }
      $runStart = -1
      $runLines.Clear()
    }
  }
}

$blocks = $blocks | Sort-Object -Property @{Expression = 'Count'; Descending = $true }, File, Start
$totalBlocks = @($blocks).Count
$totalLines = ($blocks | Measure-Object -Property Count -Sum).Sum
if (-not $totalLines) { $totalLines = 0 }

$shown = if ($Top -gt 0) { @($blocks | Select-Object -First $Top) } else { @($blocks) }
$shown = @($shown | Sort-Object File, Start)

function Format-Cell([string]$text, [int]$width) {
  if ($text.Length -gt $width) { return $text.Substring(0, $width - 1) + [char]0x2026 }
  return $text.PadRight($width)
}

function Split-Wrap([string]$text, [int]$width) {
  if ([string]::IsNullOrWhiteSpace($text)) { return @("") }
  $out = New-Object System.Collections.Generic.List[string]
  $line = ""
  foreach ($word in ($text -split '\s+')) {
    if ($word.Length -gt $width) {
      if ($line) { $out.Add($line); $line = "" }
      while ($word.Length -gt $width) {
        $out.Add($word.Substring(0, $width))
        $word = $word.Substring($width)
      }
    }
    if (-not $line) { $line = $word }
    elseif (($line.Length + 1 + $word.Length) -le $width) { $line = "$line $word" }
    else { $out.Add($line); $line = $word }
  }
  if ($line) { $out.Add($line) }
  return $out.ToArray()
}

$consoleWidth = 120
try {
  $w = $Host.UI.RawUI.WindowSize.Width
  if ($w -gt 40) { $consoleWidth = $w }
} catch {}

$locWidth = 11
$countWidth = 5
$previewWidth = $consoleWidth - $locWidth - $countWidth - 11
if ($previewWidth -lt 24) { $previewWidth = 24 }
$innerWidth = $locWidth + $countWidth + $previewWidth + 6

$h = [string][char]0x2500
$v = [string][char]0x2502
function New-Rule([char]$left, [char]$join, [char]$right) {
  "$left$h" + ($h * $locWidth) + "$h$join$h" + ($h * $countWidth) + "$h$join$h" + ($h * $previewWidth) + "$h$right"
}
function New-Span([char]$left, [char]$right) {
  "$left$h" + ($h * $innerWidth) + "$h$right"
}
$ruleTop = New-Span ([char]0x250C) ([char]0x2510)
$ruleSplit = New-Rule ([char]0x251C) ([char]0x252C) ([char]0x2524)
$ruleJoin = New-Rule ([char]0x251C) ([char]0x2534) ([char]0x2524)
$ruleBot = New-Rule ([char]0x2514) ([char]0x2534) ([char]0x2518)

Write-Host ""
Write-Host "comment-lint: comment blocks of $MinLines+ lines in .cs files" -ForegroundColor Cyan
Write-Host ""

if ($shown.Count -eq 0) {
  Write-Host "  No comment blocks found. " -NoNewline
  Write-Host "clean" -ForegroundColor Green
  Write-Host ""
  exit 0
}

$groups = @($shown | Group-Object File)

for ($gi = 0; $gi -lt $groups.Count; $gi++) {
  $g = $groups[$gi]
  $sum = ($g.Group | Measure-Object -Property Count -Sum).Sum
  $plural = if ($g.Count -eq 1) { "block" } else { "blocks" }
  $header = "{0}  ({1} {2}, {3} lines)" -f $g.Name, $g.Count, $plural, $sum

  Write-Host $(if ($gi -eq 0) { $ruleTop } else { $ruleJoin }) -ForegroundColor DarkGray

  Write-Host "$v " -NoNewline -ForegroundColor DarkGray
  Write-Host (Format-Cell $header $innerWidth) -NoNewline -ForegroundColor White
  Write-Host " $v" -ForegroundColor DarkGray
  Write-Host $ruleSplit -ForegroundColor DarkGray

  foreach ($b in $g.Group) {
    $loc = "{0}-{1}" -f $b.Start, $b.End
    $preview = ($b.Text[0] -replace '^\s*(///|//|/\*+)\s*', '')
    if ([string]::IsNullOrWhiteSpace($preview) -and $b.Text.Count -gt 1) {
      $preview = ($b.Text[1] -replace '^\s*(///|//|\*+)\s*', '')
    }

    $color = if ($b.Count -ge 10) { "Red" } elseif ($b.Count -ge 6) { "Yellow" } else { "DarkYellow" }

    Write-Host "$v " -NoNewline -ForegroundColor DarkGray
    Write-Host (Format-Cell $loc $locWidth) -NoNewline -ForegroundColor Gray
    Write-Host " $v " -NoNewline -ForegroundColor DarkGray
    Write-Host (Format-Cell ([string]$b.Count) $countWidth) -NoNewline -ForegroundColor $color
    Write-Host " $v " -NoNewline -ForegroundColor DarkGray
    Write-Host (Format-Cell $preview $previewWidth) -NoNewline -ForegroundColor DarkGray
    Write-Host " $v" -ForegroundColor DarkGray

    if ($Full) {
      foreach ($line in $b.Text) {
        $wrapped = @(Split-Wrap $line ($previewWidth - 8))
        for ($wi = 0; $wi -lt $wrapped.Count; $wi++) {
          $indent = if ($wi -eq 0) { "  " } else { "       " }
          Write-Host ("$v " + (" " * $locWidth) + " $v " + (" " * $countWidth) + " $v " + (Format-Cell "$indent$($wrapped[$wi])" $previewWidth) + " $v") -ForegroundColor DarkGray
        }
      }
    }
  }

  if ($gi -eq $groups.Count - 1) { Write-Host $ruleBot -ForegroundColor DarkGray }
}

$byFile = $blocks | Group-Object File | Sort-Object -Property @{Expression = { ($_.Group | Measure-Object -Property Count -Sum).Sum }; Descending = $true }

Write-Host ""
Write-Host "  Worst offenders" -ForegroundColor Cyan
foreach ($g in ($byFile | Select-Object -First 5)) {
  $sum = ($g.Group | Measure-Object -Property Count -Sum).Sum
  Write-Host ("    {0,4} lines in {1,-3} block(s)  {2}" -f $sum, $g.Count, $g.Name) -ForegroundColor DarkGray
}

Write-Host ""
Write-Host ("  {0} block(s), {1} comment lines, across {2} file(s) of {3} .cs scanned" -f $totalBlocks, $totalLines, @($byFile).Count, @($files).Count) -ForegroundColor Cyan
if ($Top -gt 0 -and $totalBlocks -gt $shown.Count) {
  Write-Host ("  showing top {0}; rerun without -Top for all" -f $shown.Count) -ForegroundColor DarkGray
}
Write-Host ""

if ($Strict -and $totalBlocks -gt 0) { exit 1 }
exit 0
