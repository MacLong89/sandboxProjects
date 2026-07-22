$savePath = 'C:\Program Files (x86)\Steam\steamapps\common\sbox\data\maclgames\fauna2#local\fauna2\saves\slot_2.json'
$j = Get-Content $savePath -Raw | ConvertFrom-Json
Write-Output "Top-level keys:"
$j | Get-Member -MemberType NoteProperty | ForEach-Object { Write-Output ("  " + $_.Name) }
Write-Output ""
if ($j.Animals) {
    Write-Output ("Animals: {0}" -f @($j.Animals).Count)
    @($j.Animals) | ForEach-Object { $_ | ConvertTo-Json -Compress -Depth 5 }
}
