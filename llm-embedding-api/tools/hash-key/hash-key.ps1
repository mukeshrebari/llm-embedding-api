#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Computes the SHA-256 hex hash of an API key, for use as Auth:ApiKeys:*:KeyHash.

.DESCRIPTION
    The plaintext key itself is never stored in configuration — only this hash.
    Generate a key (e.g. a GUID or a random token), hash it here, and put the HASH
    in appsettings / Key Vault / an env var such as Auth__ApiKeys__0__KeyHash.
    Distribute the plaintext key to the consuming service out of band — it cannot be
    recovered from the hash.

.EXAMPLE
    ./hash-key.ps1
    Prompts securely for the key (not echoed to the console).

.EXAMPLE
    ./hash-key.ps1 -ApiKey "my-plaintext-key"
    Hashes the given key. Prefer the prompt form to avoid the key ending up in shell
    history.
#>
param(
    [Parameter(Mandatory = $false)]
    [string]$ApiKey
)

if (-not $ApiKey) {
    $secure = Read-Host -Prompt "API key to hash" -AsSecureString
    $bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try {
        $ApiKey = [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    } finally {
        [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}

$bytes = [System.Text.Encoding]::UTF8.GetBytes($ApiKey)
$sha256 = [System.Security.Cryptography.SHA256]::Create()
try {
    $hashBytes = $sha256.ComputeHash($bytes)
} finally {
    $sha256.Dispose()
}

$hex = ($hashBytes | ForEach-Object { $_.ToString("x2") }) -join ""
Write-Output $hex
