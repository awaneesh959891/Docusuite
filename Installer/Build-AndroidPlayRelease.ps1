param(
	[string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\android-play'),
	[string]$KeystorePath,
	[string]$KeyAlias = 'docsuiteupload',
	[string]$StorePassword,
	[string]$KeyPassword,
	[string]$DistinguishedName = 'CN=DocSuite Upload, OU=Mobile, O=DocSuite, L=City, S=State, C=IN'
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\MauiApp.csproj'
$output = [IO.Path]::GetFullPath($OutputDirectory)

New-Item -ItemType Directory -Path $output -Force | Out-Null

if ([string]::IsNullOrWhiteSpace($KeystorePath)) {
	$KeystorePath = Join-Path $output 'docsuite-upload.keystore'
}
$KeystorePath = [IO.Path]::GetFullPath($KeystorePath)
$keystoreDirectory = Split-Path $KeystorePath -Parent
New-Item -ItemType Directory -Path $keystoreDirectory -Force | Out-Null

$credentialsFile = Join-Path $output 'google-play-keystore-info.txt'

if (Test-Path $KeystorePath) {
	if (([string]::IsNullOrWhiteSpace($StorePassword) -or [string]::IsNullOrWhiteSpace($KeyPassword) -or [string]::IsNullOrWhiteSpace($KeyAlias)) -and (Test-Path $credentialsFile)) {
		$saved = @{}
		foreach ($line in Get-Content $credentialsFile) {
			if ($line -match '^(?<k>[^=]+)=(?<v>.*)$') {
				$saved[$matches['k']] = $matches['v']
			}
		}

		if ([string]::IsNullOrWhiteSpace($StorePassword) -and $saved.ContainsKey('StorePassword')) {
			$StorePassword = $saved['StorePassword']
		}
		if ([string]::IsNullOrWhiteSpace($KeyPassword) -and $saved.ContainsKey('KeyPassword')) {
			$KeyPassword = $saved['KeyPassword']
		}
		if (($KeyAlias -eq 'docsuiteupload' -or [string]::IsNullOrWhiteSpace($KeyAlias)) -and $saved.ContainsKey('KeyAlias')) {
			$KeyAlias = $saved['KeyAlias']
		}
	}

	if ([string]::IsNullOrWhiteSpace($StorePassword) -or [string]::IsNullOrWhiteSpace($KeyPassword)) {
		throw 'Keystore exists, but passwords were not provided and no saved credentials were found. Pass -StorePassword and -KeyPassword.'
	}
}
else {
	if ([string]::IsNullOrWhiteSpace($StorePassword)) {
		$StorePassword = [Convert]::ToBase64String([Guid]::NewGuid().ToByteArray())
	}
	if ([string]::IsNullOrWhiteSpace($KeyPassword)) {
		$KeyPassword = $StorePassword
	}
}

$keytool = (Get-Command keytool -ErrorAction SilentlyContinue)
if (-not $keytool) {
	throw 'keytool was not found. Install the Android JDK workload and retry.'
}

if (-not (Test-Path $KeystorePath)) {
	Write-Host 'Creating Android upload keystore...' -ForegroundColor Cyan
	& $keytool.Source -genkeypair -v `
		-keystore $KeystorePath `
		-storepass $StorePassword `
		-keypass $KeyPassword `
		-alias $KeyAlias `
		-keyalg RSA `
		-keysize 4096 `
		-validity 10000 `
		-storetype PKCS12 `
		-dname $DistinguishedName

	if ($LASTEXITCODE -ne 0) {
		throw 'Keystore creation failed.'
	}
}

$publishDirectory = Join-Path $output 'bundle'
if (Test-Path $publishDirectory) {
	Remove-Item -Recurse -Force $publishDirectory
}
New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

Write-Host 'Building Google Play release AAB...' -ForegroundColor Cyan

dotnet publish $project `
	-f net11.0-android `
	-c Release `
	-p:AndroidPackageFormat=aab `
	-p:AndroidKeyStore=true `
	-p:AndroidSigningKeyStore="$KeystorePath" `
	-p:AndroidSigningStorePass="$StorePassword" `
	-p:AndroidSigningKeyAlias="$KeyAlias" `
	-p:AndroidSigningKeyPass="$KeyPassword" `
	-o $publishDirectory

if ($LASTEXITCODE -ne 0) {
	throw 'Android release build failed.'
}

$aab = Get-ChildItem -Path $publishDirectory -Filter *.aab | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $aab) {
	throw "Build completed but no .aab file was found under $publishDirectory."
}

@(
	'IMPORTANT: Keep this file private and backed up. Without this keystore, future app updates will fail.'
	''
	"KeystorePath=$KeystorePath"
	"KeyAlias=$KeyAlias"
	"StorePassword=$StorePassword"
	"KeyPassword=$KeyPassword"
	"DistinguishedName=$DistinguishedName"
) | Set-Content -Path $credentialsFile -Encoding UTF8

Write-Host ''
Write-Host 'Google Play release bundle created successfully:' -ForegroundColor Green
Write-Host $aab.FullName
Write-Host "Keystore and credentials: $credentialsFile"
Write-Host 'Upload the .aab file to Google Play Console (Internal testing first).'
