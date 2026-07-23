# Setup Firewall Rules for Maple Guardian (Proactive Hard-Lock Architecture)
$rules = @(
    @{ Name = "MSW Block"; Program = "$env:SystemRoot\System32\ping.exe" },
    @{ Name = "NGM64 Block"; Program = "$env:SystemRoot\System32\ping.exe" },
    @{ Name = "NexonLink Block"; Program = "$env:SystemRoot\System32\ping.exe" }
)

foreach ($r in $rules) {
    $existing = Get-NetFirewallRule -DisplayName $r.Name -ErrorAction SilentlyContinue
    if (-not $existing) {
        New-NetFirewallRule -DisplayName $r.Name -Direction Outbound -Action Block -Program $r.Program -InterfaceType Wired, Wireless -Enabled True
        Write-Host "Created Proactive Hard-Lock rule: $($r.Name)" -ForegroundColor Green
    } else {
        Set-NetFirewallRule -DisplayName $r.Name -InterfaceType Wired, Wireless -Enabled True
        Write-Host "Updated rule to Proactive Hard-Lock: $($r.Name)" -ForegroundColor Yellow
    }
}
