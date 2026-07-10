# Rebuild aimbox while s&box editor is CLOSED.
# If s&box is open, DLLs in .vs/output stay locked and components show as Missing.

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "Rebuilding aimbox game + editor..." -ForegroundColor Cyan
dotnet build "$root\Code\aimbox.csproj" --no-incremental
dotnet build "$root\Editor\aimbox.editor.csproj" --no-incremental

$dll = "C:\Program Files (x86)\Steam\steamapps\common\sbox\.vs\output\aimbox.dll"
if ( Test-Path $dll ) {
    $info = Get-Item $dll
    Write-Host "OK: $($info.FullName)" -ForegroundColor Green
    Write-Host "    $($info.LastWriteTime)  $($info.Length) bytes"
} else {
    Write-Host "WARN: aimbox.dll not found at expected output path." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Open the aimbox project in s&box editor"
Write-Host "2. Wait for compile to finish in the console"
Write-Host "3. If components still show Missing, close/reopen minimal.scene"
Write-Host "4. Press Play"
