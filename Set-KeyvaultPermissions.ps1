<#
Allow an Service Principal acess to Secret and Certificate metadata to all Keyvaults in a subscription
#>
[CmdletBinding()]
param(
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]
    $Subscription,
    
    # Location
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]
    $ServicePrincipalObjectId,
    
    [Parameter()]
    [switch] $WhatIf

)

$kvlist = az keyvault list --subscription $Subscription --query "[].{name: name}" -o tsv
$keyvaults = $kvlist.Split([Environment]::NewLine)


foreach ($kv in $keyvaults) {
    Write-Output "Assigning List Permissions to Service Principal $($ServicePrincipalObjectId) on KeyVault $($kv)"
    if (!$WhatIf.IsPresent) {
        az keyvault set-policy --subscription $Subscription --certificate-permissions "list" --secret-permissions "list" --object-id $ServicePrincipalObjectId -n $kv
    }
}

