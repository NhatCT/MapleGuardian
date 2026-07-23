# Setup Firewall Rules for Maple Guardian
$rules = @(
    @{ Name = "MSW Block"; Program = "$env:SystemRoot\System32\ping.exe" },
    @{ Name = "NGM64 Block"; Program = "$env:SystemRoot\System32\ping.exe" },
    @{ Name = "NexonLink Block"; Program = "$env:SystemRoot\System32\ping.exe" }
)

foreach ($r in $rules) {
    $existing = Get-NetFirewallRule -DisplayName $r.Name -ErrorAction SilentlyContinue
    if (-not $existing) {
        New-NetFirewallRule -DisplayName $r.Name -Direction Outbound -Action Block -Program $r.Program -Enabled False
        Write-Host "Created rule: $($r.Name)" -ForegroundColor Green
    } else {
        Write-Host "Rule already exists: $($r.Name)" -ForegroundColor Yellow
    }
}
