param(
	[Parameter(Mandatory=$true)][string]$Name,
	[string]$ArgsJson = "{}",
	[string]$ArgsB64 = ""
)
if ($ArgsB64 -ne "") {
	$ArgsJson = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($ArgsB64))
}
$arguments = $ArgsJson | ConvertFrom-Json
$payload = @{ jsonrpc = "2.0"; id = 1; method = "tools/call"; params = @{ name = $Name; arguments = $arguments } } | ConvertTo-Json -Depth 16 -Compress
$r = Invoke-WebRequest -Uri "http://127.0.0.1:7269/mcp" -Method POST -Body $payload -ContentType "application/json" -Headers @{ "Accept" = "application/json, text/event-stream" } -UseBasicParsing -TimeoutSec 120
$json = $r.Content | ConvertFrom-Json
if ($json.result.content) {
	foreach ($block in $json.result.content) {
		if ($block.type -eq "text") { Write-Output $block.text }
		elseif ($block.type -eq "image") {
			$path = Join-Path $env:TEMP ("mcp_img_" + (Get-Date -Format "HHmmss") + ".png")
			[IO.File]::WriteAllBytes($path, [Convert]::FromBase64String($block.data))
			Write-Output "IMAGE_SAVED: $path"
		}
		else { Write-Output ($block | ConvertTo-Json -Depth 8 -Compress) }
	}
	if ($json.result.isError) { Write-Output "[isError=true]" }
} else {
	Write-Output $r.Content
}
