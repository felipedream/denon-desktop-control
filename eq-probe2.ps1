$tcp = New-Object System.Net.Sockets.TcpClient
$tcp.Connect("192.168.1.82", 23)
$s = $tcp.GetStream()
$bytes = [Text.Encoding]::ASCII.GetBytes("PSGEQ ON`rPSGEQ?`rPS?`r")
$s.Write($bytes, 0, $bytes.Length)
Start-Sleep -Seconds 3
$buf = New-Object byte[] 32768
$all = ""
while ($s.DataAvailable) {
    $n = $s.Read($buf, 0, 32768)
    $all += [Text.Encoding]::ASCII.GetString($buf, 0, $n)
    Start-Sleep -Milliseconds 50
}
$tcp.Close()
$lines = $all -split "`r" | ForEach-Object { $_.Trim() } | Where-Object { $_ -match "^PS" }
$lines | Sort-Object -Unique | ForEach-Object { Write-Host $_ }
