# Open Azure Portal pre-filled for NCASv3 GPU quota request (16 vCPUs, East US)
# Use when CLI quota API is throttled or unavailable.
$sub = "c4b9b22b-ffd8-460e-b684-5f437d4d191e"
$params = @{
    subscriptionId = $sub
    command        = "openQuotaApprovalBlade"
    quotas         = @(
        @{
            location     = "eastus"
            providerId   = "Microsoft.Compute"
            resourceName = "Standard NCASv3_T4 Family"
            quotaRequest = @{
                properties = @{
                    limit = 16
                    unit  = "Count"
                    name  = @{ value = "Standard NCASv3_T4 Family" }
                }
            }
        }
    )
} | ConvertTo-Json -Compress -Depth 6
$encoded = [uri]::EscapeDataString($params)
$url = "https://portal.azure.com/#view/Microsoft_Azure_Capacity/UsageAndQuota.ReactView/Parameters/$encoded"
Write-Host "Opening Azure quota request (pre-filled to 16 vCPUs)..."
Write-Host $url
Start-Process $url
