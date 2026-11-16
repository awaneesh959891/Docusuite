$ErrorActionPreference = 'Stop'
$package = Get-AppxPackage -Name 'DocSuite.PdfUtility'
if (-not $package) {
	Write-Host 'DocSuite is not installed.'
	exit 0
}

Remove-AppxPackage -Package $package.PackageFullName
Write-Host 'DocSuite was uninstalled. Local app data may remain in the user profile.' -ForegroundColor Green
