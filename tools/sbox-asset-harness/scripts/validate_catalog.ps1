# Validate s&box asset harness catalog JSON files (PowerShell; no Python required).
$ErrorActionPreference = "Stop"
$CatalogDir = Join-Path $PSScriptRoot "..\catalog" | Resolve-Path
$files = Get-ChildItem -Path $CatalogDir -Filter "*.catalog.json"
if (-not $files) {
	Write-Error "No *.catalog.json files found"
	exit 1
}

$allowedLanes = @("kit", "mesh", "place")
$allowedStatus = @("ready", "placeholder", "blocked_no_blender", "needs_import")
$errors = New-Object System.Collections.Generic.List[string]
$seen = @{}

foreach ($file in $files) {
	try {
		$data = Get-Content -Raw -Path $file.FullName | ConvertFrom-Json
	}
	catch {
		$errors.Add("$($file.Name): invalid JSON ($($_.Exception.Message))")
		continue
	}

	if ($null -eq $data.version) {
		$errors.Add("$($file.Name): missing version")
	}

	if ($null -eq $data.entries) {
		$errors.Add("$($file.Name): entries must be a list")
		continue
	}

	$i = 0
	foreach ($entry in $data.entries) {
		$prefix = "$($file.Name)[$i]"
		if (-not $entry.id) {
			$errors.Add("$prefix`: missing id")
		}
		elseif ($seen.ContainsKey([string]$entry.id)) {
			$errors.Add("$prefix`: duplicate id '$($entry.id)' (also in $($seen[$entry.id]))")
		}
		else {
			$seen[[string]$entry.id] = $file.Name
		}

		if (-not $entry.games -or @($entry.games).Count -lt 1) {
			$errors.Add("$prefix`: games must be non-empty list")
		}

		if ($allowedLanes -notcontains $entry.lane) {
			$errors.Add("$prefix`: lane must be one of $($allowedLanes -join ', ')")
		}

		if ($allowedStatus -notcontains $entry.status) {
			$errors.Add("$prefix`: status must be one of $($allowedStatus -join ', ')")
		}

		foreach ($key in @("kind", "title")) {
			if (-not $entry.$key) {
				$errors.Add("$prefix`: missing $key")
			}
		}

		if ($entry.lane -eq "kit" -and $null -eq $entry.kit) {
			$errors.Add("$prefix`: kit lane requires kit object")
		}

		if ($entry.status -eq "ready" -and $entry.lane -eq "mesh" -and -not $entry.vmdl) {
			$errors.Add("$prefix`: mesh+ready requires non-null vmdl")
		}

		if ($entry.vmdl -and ($entry.vmdl -is [string]) -and -not $entry.vmdl.EndsWith(".vmdl")) {
			$errors.Add("$prefix`: vmdl should end with .vmdl")
		}

		$i++
	}
}

if ($errors.Count -gt 0) {
	Write-Host "Catalog validation FAILED:"
	foreach ($e in $errors) { Write-Host "  - $e" }
	exit 1
}

Write-Host "OK ($($files.Count) catalog file(s), $($seen.Count) entries)"
exit 0
