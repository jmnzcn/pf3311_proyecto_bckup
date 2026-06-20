# Open Azure Support request for GPU quota (when New quota request is disabled)
$sub = "c4b9b22b-ffd8-460e-b684-5f437d4d191e"
$url = "https://portal.azure.com/#blade/Microsoft_Azure_Support/HelpAndSupportBlade/newsupportrequest"
Write-Host "=== Azure GPU quota - ticket de soporte ===" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Se abrira Help + Support, New support request"
Write-Host "2. Completar asi:"
Write-Host ""
Write-Host "   Issue type:     Service and subscription limits (quotas)"
Write-Host "   Problem type:   Quota"
Write-Host "   Subscription:   Azure-lab ($sub)"
Write-Host "   Quota type:     Compute-VM (cores-vCPUs) subscription limit increases"
Write-Host "   Region:         East US"
Write-Host "   VM series:      Standard NCASv3 T4 Family"
Write-Host "   New limit:      16 vCPUs"
Write-Host ""
Write-Host "3. En Details pegar texto de _tools/azure/support_ticket_quota_text.txt"
Write-Host ""
Start-Process $url
