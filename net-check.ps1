$avr = "192.168.1.82"

Write-Host "1) HTTP probe on port 8080:"
try {
    $r = Invoke-WebRequest "http://${avr}:8080/goform/Deviceinfo.xml" -TimeoutSec 3 -UseBasicParsing
    Write-Host "   OK ($($r.StatusCode)) len=$($r.Content.Length)"
    $m = [regex]::Match($r.Content, "<ModelName>([^<]+)</ModelName>")
    if ($m.Success) { Write-Host "   Model: $($m.Groups[1].Value)" }
} catch { Write-Host "   FAIL: $($_.Exception.Message)" }

Write-Host "2) HTTP probe on port 80:"
try {
    $r = Invoke-WebRequest "http://${avr}/goform/Deviceinfo.xml" -TimeoutSec 3 -UseBasicParsing
    Write-Host "   OK ($($r.StatusCode))"
} catch { Write-Host "   FAIL: $($_.Exception.Message)" }

Write-Host "3) Telnet port 23:"
$tcp = New-Object System.Net.Sockets.TcpClient
try {
    $ok = $tcp.ConnectAsync($avr, 23).Wait(2000)
    if ($ok -and $tcp.Connected) { Write-Host "   OPEN" } else { Write-Host "   closed" }
} catch { Write-Host "   ERR $($_.Exception.Message)" }
$tcp.Close()

Write-Host "4) Current MV status:"
try {
    $r = Invoke-WebRequest "http://${avr}:8080/goform/formMainZone_MainZoneXmlStatusLite.xml" -TimeoutSec 3 -UseBasicParsing
    Write-Host $r.Content
} catch { Write-Host "   FAIL" }
