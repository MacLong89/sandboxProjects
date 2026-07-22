$savePath = 'C:\Program Files (x86)\Steam\steamapps\common\sbox\data\maclgames\fauna2#local\fauna2\saves\slot_2.json'
$j = Get-Content $savePath -Raw | ConvertFrom-Json
$j.Placeables | ForEach-Object { $_ | ConvertTo-Json -Compress -Depth 5 }
