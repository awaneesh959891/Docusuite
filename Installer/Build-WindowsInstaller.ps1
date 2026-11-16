param(
	[string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\windows-installer')
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\MauiApp.csproj'
$output = [IO.Path]::GetFullPath($OutputDirectory)
$subject = 'CN=DocSuite Development'

Write-Host 'Building DocSuite Windows installer...' -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $output | Out-Null

$certificate = Get-ChildItem Cert:\CurrentUser\My | Where-Object {
	$_.Subject -eq $subject -and $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date).AddDays(30)
} | Sort-Object NotAfter -Descending | Select-Object -First 1

if (-not $certificate) {
	Write-Host 'Creating a local DocSuite code-signing certificate...'
	$certificate = New-SelfSignedCertificate `
		-Type CodeSigningCert `
		-Subject $subject `
		-FriendlyName 'DocSuite Development Installer' `
		-CertStoreLocation 'Cert:\CurrentUser\My' `
		-KeyAlgorithm RSA `
		-KeyLength 3072 `
		-HashAlgorithm SHA256 `
		-KeyExportPolicy Exportable `
		-NotAfter (Get-Date).AddYears(3)
}

$publicCertificate = Join-Path $output 'DocSuite.cer'
Export-Certificate -Cert $certificate -FilePath $publicCertificate -Force | Out-Null

$publishDirectory = Join-Path $output 'package'
if (Test-Path $publishDirectory) {
	Remove-Item -Recurse -Force $publishDirectory
}
New-Item -ItemType Directory -Force -Path $publishDirectory | Out-Null

dotnet publish $project `
	-f net11.0-windows10.0.19041.0 `
	-c Release `
	-p:RuntimeIdentifier=win-x64 `
	-p:PackageCertificateThumbprint=$($certificate.Thumbprint) `
	-p:AppxPackageSigningEnabled=true `
	-p:UapAppxPackageBuildMode=SideloadOnly `
	-p:AppxPackageDir="$publishDirectory\"

if ($LASTEXITCODE -ne 0) {
	throw 'The Windows package build failed.'
}

$msix = Get-ChildItem -Path $publishDirectory -Recurse -Filter *.msix | Select-Object -First 1
if (-not $msix) {
	throw "Build completed but no MSIX file was found under $publishDirectory."
}

Copy-Item (Join-Path $PSScriptRoot 'Install-DocSuite.ps1') $output -Force
Copy-Item (Join-Path $PSScriptRoot 'Install-DocSuite.cmd') $output -Force
Copy-Item (Join-Path $PSScriptRoot 'Uninstall-DocSuite.ps1') $output -Force
Copy-Item (Join-Path $PSScriptRoot 'Privacy-Policy.txt') $output -Force
Copy-Item (Join-Path $PSScriptRoot 'Terms-and-Disclaimer.txt') $output -Force

$zip = Join-Path (Split-Path $output -Parent) 'DocSuite-Windows-Installer-1.0.0.zip'
if (Test-Path $zip) {
    Remove-Item -Force $zip
}
Compress-Archive -Path (Join-Path $output '*') -DestinationPath $zip -CompressionLevel Optimal

Write-Host ''
Write-Host 'Installer created successfully:' -ForegroundColor Green
Write-Host $msix.FullName
Write-Host "Distribution ZIP: $zip"
Write-Host 'Extract the ZIP on another Windows PC and double-click Install-DocSuite.cmd.'
