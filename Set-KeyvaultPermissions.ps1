<#
.SYNOPSIS
    Allow an Service Principal acess to Key, Secret and Certificate metadata to all Keyvaults in a subscription
    Requires az cli to be installed (https://aka.ms/azcli) and user to be logged in
#>
[CmdletBinding()]
param(
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]
    $Subscription,
    
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
    if ($kv -eq "expiration-scanner") {
        continue
    }
    Write-Output "Assigning List permission to service principal $($ServicePrincipalObjectId) on KeyVault $($kv)"
    if (!$WhatIf.IsPresent) {
        az keyvault set-policy --subscription $Subscription  --key-permissions "list" --certificate-permissions "list" --secret-permissions "list" --object-id $ServicePrincipalObjectId -n $kv
    }
}
