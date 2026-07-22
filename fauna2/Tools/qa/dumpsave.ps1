$savePath = 'C:\Program Files (x86)\Steam\steamapps\common\sbox\data\maclgames\fauna2#local\fauna2\saves\slot_2.json'
$j = Get-Content $savePath -Raw | ConvertFrom-Json
Write-Output ("Money: {0}" -f $j.Money)
Write-Output "Placeables:"
$j.Placeables | ForEach-Object { Write-Output ("  {0} at ({1},{2})" -f $_.Kind, $_.Position.x, $_.Position.y) }
Write-Output ("Habitats: {0}  Animals: {1}  Wild: {2}" -f $j.Habitats.Count, ($j.Habitats | ForEach-Object { $_.Animals.Count } | Measure-Object -Sum).Sum, $j.WildAnimals.Count)
Write-Output ("Player at ({0},{1})" -f $j.OwnerPlayerPosition.x, $j.OwnerPlayerPosition.y)
