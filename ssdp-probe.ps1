$local = [Net.IPAddress]::Parse("192.168.1.84")
$udp = New-Object System.Net.Sockets.UdpClient([Net.IPEndPoint]::new($local, 0))
$udp.MulticastLoopback = $false
try { $udp.JoinMulticastGroup([Net.IPAddress]::Parse("239.255.255.250"), $local) } catch { Write-Host "JoinMulti failed: $($_.Exception.Message)" }

$msearch = "M-SEARCH * HTTP/1.1`r`nHOST: 239.255.255.250:1900`r`nMAN: `"ssdp:discover`"`r`nMX: 2`r`nST: ssdp:all`r`n`r`n"
$bytes = [Text.Encoding]::ASCII.GetBytes($msearch)
$ep = [Net.IPEndPoint]::new([Net.IPAddress]::Parse("239.255.255.250"), 1900)
$udp.Send($bytes, $bytes.Length, $ep) | Out-Null

$udp.Client.ReceiveTimeout = 3000
$hosts = @{}
try {
    while ($true) {
        $remote = New-Object Net.IPEndPoint([Net.IPAddress]::Any, 0)
        $rec = $udp.Receive([ref]$remote)
        $t = [Text.Encoding]::ASCII.GetString($rec)
        $srv = ""
        if ($t -match "SERVER:\s*(.+)") { $srv = $matches[1].Trim() }
        $loc = ""
        if ($t -match "LOCATION:\s*(.+)") { $loc = $matches[1].Trim() }
        $key = $remote.Address.ToString()
        if (-not $hosts.ContainsKey($key)) { $hosts[$key] = @{ srv = $srv; loc = $loc } }
    }
} catch {}
$udp.Close()

Write-Host "Total unique responders: $($hosts.Count)"
foreach ($k in $hosts.Keys) {
    Write-Host "  $k"
    Write-Host "    server: $($hosts[$k].srv)"
    Write-Host "    loc:    $($hosts[$k].loc)"
}
