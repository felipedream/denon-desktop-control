$tcp = New-Object System.Net.Sockets.TcpClient
$tcp.Connect("192.168.1.82", 23)
$s = $tcp.GetStream()
$w = New-Object System.IO.StreamWriter($s)
$w.AutoFlush = $true

# The Denon GEQ uses HTTP AppCommand. Try telnet first with various formats.
$cmds = @(
    "PSGEQ BAND1 ?"
    "PSGEQ BAND2 ?"
    "PSGEQ BAND3 ?"
    "PSGEQ BAND4 ?"
    "PSGEQ BAND5 ?"
    "PSGEQ BAND6 ?"
    "PSGEQ BAND7 ?"
    "PSGEQ BAND8 ?"
    "PSGEQ BAND9 ?"
)
foreach ($c in $cmds) {
    $w.Write("$c`r")
    Start-Sleep -Milliseconds 200
}
Start-Sleep -Seconds 3
$buf = New-Object byte[] 16384
$all = ""
while ($s.DataAvailable) {
    $n = $s.Read($buf, 0, 16384)
    $all += [Text.Encoding]::ASCII.GetString($buf, 0, $n)
    Start-Sleep -Milliseconds 100
}
$tcp.Close()

Write-Host "=== RAW ==="
$all -split "`r" | ForEach-Object { if ($_.Trim()) { Write-Host $_.Trim() } }
