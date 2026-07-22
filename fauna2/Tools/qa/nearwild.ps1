$savePath = 'C:\Program Files (x86)\Steam\steamapps\common\sbox\data\maclgames\fauna2#local\fauna2\saves\slot_2.json'
$j = Get-Content $savePath -Raw | ConvertFrom-Json
$px = $j.OwnerPlayerPosition.X; $py = $j.OwnerPlayerPosition.Y
Write-Output ("Player at ({0:F0},{1:F0})  SavedAtUnix: {2}" -f $px, $py, $j.SavedAtUnix)
$j.WildAnimals | ForEach-Object {
    $d = [math]::Sqrt(($_.Position.X-$px)*($_.Position.X-$px) + ($_.Position.Y-$py)*($_.Position.Y-$py))
    [PSCustomObject]@{ Species = $_.SpeciesId; X = [math]::Round($_.Position.X); Y = [math]::Round($_.Position.Y); Dist = [math]::Round($d) }
} | Sort-Object Dist | Select-Object -First 6 | Format-Table -AutoSize
