$savePath = 'C:\Program Files (x86)\Steam\steamapps\common\sbox\data\maclgames\fauna2#local\fauna2\saves\slot_2.json'
$start = (Get-Item $savePath).LastWriteTime
Write-Output ("Current save time: {0:HH:mm:ss}" -f $start)
$deadline = (Get-Date).AddSeconds(120)
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 2
    $t = (Get-Item $savePath).LastWriteTime
    if ($t -gt $start) {
        Write-Output ("New save at {0:HH:mm:ss}" -f $t)
        exit 0
    }
}
Write-Output "Timed out waiting for save"
exit 1
