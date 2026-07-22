#Requires -Version 5.1
<#
.SYNOPSIS
  Copies reusable content from every game Assets/ folder into shared_assets/.

.DESCRIPTION
  - Writes a deduped union under shared_assets/Assets/ (same relative paths).
  - Skips scenes/ and map/ (game-specific).
  - Skips third-party Libraries/ trees.
  - On path collisions, keeps the preferred game's file (see $GamePriority).
  - Copies tools/FBX and tools/tripo_output into shared_assets/source/.
  - Writes shared_assets/inventory.json with origin metadata.

  Does NOT delete or modify source game Assets folders.
#>
[CmdletBinding()]
param(
  [string]$RepoRoot = ""
)

$ErrorActionPreference = "Stop"

if (-not $RepoRoot) {
  $RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
}

$DestRoot = Join-Path $RepoRoot "shared_assets"
$DestAssets = Join-Path $DestRoot "Assets"
$DestSource = Join-Path $DestRoot "source"
$InventoryPath = Join-Path $DestRoot "inventory.json"

# Prefer richer / more complete trees first when relative paths collide.
$GamePriority = @(
  "terraingen",
  "thorns",
  "fauna2",
  "under_pressure",
  "aimbox",
  "offshore",
  "scene_lab",
  "the_final_outpost",
  "youarenotalone",
  "deep",
  "deep_dive",
  "offshore_fishing",
  "think_drink",
  "heights_hotel",
  "dynastyfootball",
  "run_gun",
  "plunge",
  "belowthesand"
)

$SkipTopDirs = @(
  "scenes",
  "map"
)

function Get-PriorityIndex([string]$game) {
  $i = [array]::IndexOf($GamePriority, $game)
  if ($i -lt 0) { return 1000 }
  return $i
}

function Should-SkipRelative([string]$rel) {
  $norm = $rel.Replace("\", "/")
  foreach ($skip in $SkipTopDirs) {
    if ($norm -eq $skip -or $norm.StartsWith("$skip/")) { return $true }
  }
  return $false
}

New-Item -ItemType Directory -Force -Path $DestAssets | Out-Null
New-Item -ItemType Directory -Force -Path $DestSource | Out-Null

# relPath -> @{ game; fullPath; length; writeTime }
$winners = @{}
$origins = @{}  # relPath -> list of games that had it

$gamesWithAssets = Get-ChildItem -Path $RepoRoot -Directory | Where-Object {
  $_.Name -ne "shared_assets" -and (Test-Path (Join-Path $_.FullName "Assets"))
} | Sort-Object { Get-PriorityIndex $_.Name }

Write-Host "Scanning $($gamesWithAssets.Count) games..."

foreach ($gameDir in $gamesWithAssets) {
  $game = $gameDir.Name
  $assetsRoot = Join-Path $gameDir.FullName "Assets"
  $prefixLen = $assetsRoot.Length + 1

  Get-ChildItem -Path $assetsRoot -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
    $rel = $_.FullName.Substring($prefixLen)
    if (Should-SkipRelative $rel) { return }

    if (-not $origins.ContainsKey($rel)) {
      $origins[$rel] = New-Object System.Collections.Generic.List[string]
    }
    if (-not $origins[$rel].Contains($game)) {
      $origins[$rel].Add($game)
    }

    $candidate = @{
      game      = $game
      fullPath  = $_.FullName
      length    = $_.Length
      writeTime = $_.LastWriteTimeUtc
    }

    if (-not $winners.ContainsKey($rel)) {
      $winners[$rel] = $candidate
      return
    }

    $cur = $winners[$rel]
    $curPri = Get-PriorityIndex $cur.game
    $newPri = Get-PriorityIndex $game

    $replace = $false
    if ($newPri -lt $curPri) {
      $replace = $true
    }
    elseif ($newPri -eq $curPri) {
      if ($candidate.writeTime -gt $cur.writeTime) { $replace = $true }
      elseif ($candidate.writeTime -eq $cur.writeTime -and $candidate.length -gt $cur.length) { $replace = $true }
    }

    if ($replace) {
      $winners[$rel] = $candidate
    }
  }
}

Write-Host "Copying $($winners.Count) unique asset files into shared_assets/Assets ..."
$copied = 0
$bytes = [long]0
foreach ($rel in ($winners.Keys | Sort-Object)) {
  $src = $winners[$rel].fullPath
  $dst = Join-Path $DestAssets $rel
  $dstDir = Split-Path $dst -Parent
  if (-not (Test-Path $dstDir)) {
    New-Item -ItemType Directory -Force -Path $dstDir | Out-Null
  }
  Copy-Item -LiteralPath $src -Destination $dst -Force
  $copied++
  $bytes += $winners[$rel].length
  if (($copied % 200) -eq 0) {
    Write-Host "  ... $copied / $($winners.Count)"
  }
}

# Source meshes from tools/
$fbxSrc = Join-Path $RepoRoot "tools\FBX"
$fbxDst = Join-Path $DestSource "fbx"
if (Test-Path $fbxSrc) {
  New-Item -ItemType Directory -Force -Path $fbxDst | Out-Null
  Copy-Item -Path (Join-Path $fbxSrc "*") -Destination $fbxDst -Recurse -Force
  Write-Host "Copied tools/FBX -> source/fbx"
}

$tripoSrc = Join-Path $RepoRoot "tools\tripo_output"
$tripoDst = Join-Path $DestSource "tripo"
if (Test-Path $tripoSrc) {
  New-Item -ItemType Directory -Force -Path $tripoDst | Out-Null
  Copy-Item -Path (Join-Path $tripoSrc "*") -Destination $tripoDst -Recurse -Force
  Write-Host "Copied tools/tripo_output -> source/tripo"
}

$inventoryEntries = @()
foreach ($rel in ($winners.Keys | Sort-Object)) {
  $w = $winners[$rel]
  $inventoryEntries += [ordered]@{
    path     = $rel.Replace("\", "/")
    chosenFrom = $w.game
    alsoIn   = @($origins[$rel] | Where-Object { $_ -ne $w.game } | Sort-Object)
    bytes    = $w.length
  }
}

$inventory = [ordered]@{
  generatedUtc = (Get-Date).ToUniversalTime().ToString("o")
  fileCount    = $copied
  totalBytes   = $bytes
  skipTopDirs  = $SkipTopDirs
  gamePriority = $GamePriority
  entries      = $inventoryEntries
}

$inventory | ConvertTo-Json -Depth 6 | Set-Content -Path $InventoryPath -Encoding UTF8

Write-Host ""
Write-Host "Done."
Write-Host "  Files: $copied"
Write-Host "  Size:  $([math]::Round($bytes/1MB, 1)) MB"
Write-Host "  Dest:  $DestAssets"
Write-Host "  Index: $InventoryPath"
