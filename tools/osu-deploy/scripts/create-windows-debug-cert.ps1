param(
    [string]$OutFile = "$PSScriptRoot\..\debug-signing\osuoss-debug.pfx",
    [string]$Password = "osuoss-debug"
)

$ErrorActionPreference = "Stop"

New-Item -ItemType Directory -Force -Path (Split-Path $OutFile) | Out-Null

$cert = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject "CN=osu!oss Debug" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -KeyExportPolicy Exportable `
    -KeyUsage DigitalSignature `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3") `
    -NotAfter (Get-Date).AddYears(2)

$securePassword = ConvertTo-SecureString -String $Password -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath $OutFile -Password $securePassword | Out-Null
Remove-Item "Cert:\CurrentUser\My\$($cert.Thumbprint)"

Write-Host "Created $OutFile"
Write-Host "Set OSU_DEPLOY_WINDOWS_SIGN_PARAMS=/fd sha256 /td sha256 /f `"$OutFile`" /p $Password"
