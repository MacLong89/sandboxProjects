$savePath = 'C:\Program Files (x86)\Steam\steamapps\common\sbox\data\maclgames\fauna2#local\fauna2\saves\slot_2.json'
$j = Get-Content $savePath -Raw | ConvertFrom-Json
Write-Output "Habitat keys:"
$j.Habitats[0] | Get-Member -MemberType NoteProperty | ForEach-Object { Write-Output ("  " + $_.Name) }
Write-Output ""
$h = $j.Habitats[0]
Write-Output ("Habitat pos: ({0},{1})" -f $h.Position.X, $h.Position.Y)
$animals = $h.Animals
if (-not $animals) { $animals = $h.Residents }
Write-Output ("Animal count: {0}" -f @($animals).Count)
@($animals) | Select-Object -First 3 | ForEach-Object { $_ | ConvertTo-Json -Compress -Depth 5 }
Write-Output ""
Write-Output "Nearest 5 wild animals to player:"
$px = $j.OwnerPlayerPosition.X; $py = $j.OwnerPlayerPosition.Y
$j.WildAnimals | Sort-Object { [math]::Sqrt(($_.Position.X-$px)*($_.Position.X-$px) + ($_.Position.Y-$py)*($_.Position.Y-$py)) } | Select-Object -First 5 | ForEach-Object { $_ | ConvertTo-Json -Compress -Depth 5 }
