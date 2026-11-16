$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$certificate = Join-Path $root 'DocSuite.cer'
$package = Get-ChildItem -Path (Join-Path $root 'package') -Recurse -Filter *.msix | Select-Object -First 1

if (-not (Test-Path $certificate)) {
	throw 'DocSuite.cer is missing. Keep the complete installer folder together.'
}
if (-not $package) {
	throw 'The DocSuite MSIX package is missing. Keep the complete installer folder together.'
}

Write-Host 'DocSuite Installer' -ForegroundColor Cyan
Write-Host 'This development package uses a locally generated certificate.'
Write-Host 'Only install it if you trust the person who gave it to you.' -ForegroundColor Yellow
Write-Host ''

Import-Certificate -FilePath $certificate -CertStoreLocation Cert:\CurrentUser\TrustedPeople | Out-Null
Add-AppxPackage -Path $package.FullName -ForceApplicationShutdown

Write-Host ''
Write-Host 'DocSuite was installed successfully.' -ForegroundColor Green
Write-Host 'Open DocSuite from the Windows Start menu.'
Read-Host 'Press Enter to close'
