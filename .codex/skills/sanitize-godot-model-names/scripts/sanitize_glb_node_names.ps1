param(
  [Parameter(Mandatory = $true)]
  [string]$Path,

  [string[]]$Rename = @(),

  [switch]$ListOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Read-UInt32LE {
  param([byte[]]$Bytes, [int]$Offset)
  return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Write-UInt32LE {
  param([byte[]]$Bytes, [int]$Offset, [uint32]$Value)
  [Array]::Copy([BitConverter]::GetBytes($Value), 0, $Bytes, $Offset, 4)
}

function Get-JsonChunk {
  param([byte[]]$Bytes)

  if ($Bytes.Length -lt 20) {
    throw "File is too small to be a GLB: $Path"
  }

  $magic = [Text.Encoding]::ASCII.GetString($Bytes, 0, 4)
  if ($magic -ne "glTF") {
    throw "Not a GLB file: $Path"
  }

  $jsonLength = Read-UInt32LE $Bytes 12
  $jsonType = [Text.Encoding]::ASCII.GetString($Bytes, 16, 4)
  if ($jsonType -ne "JSON") {
    throw "First GLB chunk is '$jsonType', expected JSON."
  }

  $jsonStart = 20
  $jsonEnd = $jsonStart + [int]$jsonLength
  if ($jsonEnd -gt $Bytes.Length) {
    throw "GLB JSON chunk length exceeds file length."
  }

  return @{
    Start = $jsonStart
    End = $jsonEnd
    Length = [int]$jsonLength
    Text = [Text.Encoding]::UTF8.GetString($Bytes, $jsonStart, [int]$jsonLength).TrimEnd(" ", "`0")
  }
}

function Parse-Renames {
  param([string[]]$Pairs)

  $map = [ordered]@{}
  $expandedPairs = @()
  foreach ($rawPair in $Pairs) {
    $expandedPairs += ($rawPair -split "," | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
  }

  foreach ($pair in $expandedPairs) {
    $pair = $pair.Trim().Trim('"')
    $equals = $pair.IndexOf("=")
    if ($equals -lt 1) {
      throw "Rename must be old=new: $pair"
    }

    $old = $pair.Substring(0, $equals)
    $new = $pair.Substring($equals + 1)
    if ([string]::IsNullOrWhiteSpace($new)) {
      throw "New name cannot be empty: $pair"
    }

    $map[$old] = $new
  }

  return $map
}

function ConvertTo-JsonString {
  param([string]$Value)
  return ($Value | ConvertTo-Json -Compress)
}

$resolved = Resolve-Path -LiteralPath $Path
$bytes = [System.IO.File]::ReadAllBytes($resolved)
$chunk = Get-JsonChunk $bytes
$gltf = $chunk.Text | ConvertFrom-Json

if (-not $gltf.nodes) {
  Write-Output "No nodes found in $resolved"
  exit 0
}

if ($ListOnly -or $Rename.Count -eq 0) {
  for ($i = 0; $i -lt $gltf.nodes.Count; $i++) {
    $name = $gltf.nodes[$i].name
    Write-Output "${i}: $name"
  }
  exit 0
}

$renameMap = Parse-Renames $Rename
$changed = 0
$newJson = $chunk.Text

foreach ($node in $gltf.nodes) {
  $oldName = [string]$node.name
  $newName = $renameMap[$oldName]
  if ($null -ne $newName) {
    $oldJsonName = ConvertTo-JsonString $oldName
    $newJsonName = ConvertTo-JsonString ([string]$newName)
    $pattern = '("name"\s*:\s*)' + [regex]::Escape($oldJsonName)

    $before = $newJson
    $newJson = [regex]::Replace(
      $newJson,
      $pattern,
      [System.Text.RegularExpressions.MatchEvaluator]{
        param($match)
        return $match.Groups[1].Value + $newJsonName
      }
    )

    if ($newJson -ne $before) {
      $changed++
    }
  }
}

if ($changed -eq 0) {
  Write-Output "No matching node names found."
  exit 0
}

# Validate the edited JSON before writing the binary file.
$null = $newJson | ConvertFrom-Json
$newJsonBytes = [Text.Encoding]::UTF8.GetBytes($newJson)
$newJsonLength = ($newJsonBytes.Length + 3) -band -4
$paddedJson = New-Object byte[] $newJsonLength
[Array]::Copy($newJsonBytes, $paddedJson, $newJsonBytes.Length)
for ($i = $newJsonBytes.Length; $i -lt $newJsonLength; $i++) {
  $paddedJson[$i] = 0x20
}

$restLength = $bytes.Length - $chunk.End
$newTotalLength = 20 + $newJsonLength + $restLength
$output = New-Object byte[] $newTotalLength

[Array]::Copy($bytes, 0, $output, 0, 20)
Write-UInt32LE $output 8 ([uint32]$newTotalLength)
Write-UInt32LE $output 12 ([uint32]$newJsonLength)
[Array]::Copy($paddedJson, 0, $output, 20, $newJsonLength)
[Array]::Copy($bytes, $chunk.End, $output, 20 + $newJsonLength, $restLength)

[System.IO.File]::WriteAllBytes($resolved, $output)
Write-Output "Renamed $changed node(s) in $resolved"
