param(
  [Parameter(Mandatory = $true)]
  [string] $Ref,

  [switch] $SkipBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$upstream = Join-Path $repoRoot "vendor/Klotho"
$source = Join-Path $upstream "dist/addons/klotho"
$target = Join-Path $repoRoot "client/addons/klotho"
$safeDirectory = ($upstream -replace "\\", "/")

if (!(Test-Path $upstream)) {
  throw "Missing Klotho submodule at $upstream. Run: git submodule update --init vendor/Klotho"
}

git -c "safe.directory=$safeDirectory" -C $upstream fetch origin $Ref
git -c "safe.directory=$safeDirectory" -C $upstream checkout $Ref

if (!(Test-Path $source)) {
  throw "Missing addon distribution at $source"
}

$uidBackup = Join-Path ([System.IO.Path]::GetTempPath()) ("avalon-klotho-uids-" + [System.Guid]::NewGuid())
New-Item -ItemType Directory -Force $uidBackup | Out-Null

if (Test-Path $target) {
  Get-ChildItem $target -Recurse -File -Filter "*.uid" | ForEach-Object {
    $relative = [System.IO.Path]::GetRelativePath($target, $_.FullName)
    $backupPath = Join-Path $uidBackup $relative
    New-Item -ItemType Directory -Force (Split-Path $backupPath -Parent) | Out-Null
    Copy-Item -LiteralPath $_.FullName -Destination $backupPath -Force
  }

  Remove-Item -LiteralPath $target -Recurse -Force
}

New-Item -ItemType Directory -Force $target | Out-Null
Copy-Item -Path (Join-Path $source "*") -Destination $target -Recurse -Force

Get-ChildItem $uidBackup -Recurse -File -Filter "*.uid" | ForEach-Object {
  $relative = [System.IO.Path]::GetRelativePath($uidBackup, $_.FullName)
  $restorePath = Join-Path $target $relative
  New-Item -ItemType Directory -Force (Split-Path $restorePath -Parent) | Out-Null
  Copy-Item -LiteralPath $_.FullName -Destination $restorePath -Force
}

Remove-Item -LiteralPath $uidBackup -Recurse -Force

if (!$SkipBuild) {
  dotnet build (Join-Path $repoRoot "client/Client.csproj")
  dotnet build (Join-Path $repoRoot "server/Server.csproj") -o "C:\tmp\avalon-server-build"
}
