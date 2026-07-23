# Setup Firewall Rules for Maple Guardian (Proactive Hard-Lock Architecture)
# Blocks ONLY game processes — does NOT affect other network traffic
$rules = @(
    @{ Name = "MSW Block";   Program = "MaplePlanet" },
    @{ Name = "NGM64 Block"; Program = "MaplePlanet" },
    @{ Name = "NexonLink Block"; Program = "MaplePlanet" }
)

foreach ($r in $rules) {
    $existing = Get-NetFirewallRule -DisplayName $r.Name -ErrorAction SilentlyContinue
    if (-not $existing) {
        New-NetFirewallRule `
            -DisplayName $r.Name `
            -Direction Outbound `
            -Action Block `
            -Program $r.Program `
            -Enabled False `
            -Description "Blocks $($r.Program) traffic when VPN disconnects. Created by Maple Guardian."
        Write-Host "Created rule: $($r.Name) (blocks $($r.Program))" -ForegroundColor Green
    } else {
        Write-Host "Rule already exists: $($r.Name)" -ForegroundColor Yellow
    }
}
