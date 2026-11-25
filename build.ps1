param (
	[Parameter()]
	[ValidateSet('Debug', 'Release')]
	[string]
	$Configuration = 'Release',

	[Parameter()]
	[ValidateNotNullOrEmpty()]
	[string]
	$OutputPath = 'release',

	[Parameter()]
	[bool]
	$SelfContained = $False,

	[Parameter()]
	[bool]
	$PublishReadyToRun = $False,

	[Parameter()]
	[bool]
	$PublishSingleFile = $False
)

$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'scripts/download.ps1') -OutputPath $OutputPath

Write-Host "Building $Configuration to $OutputPath"
dotnet publish `
	-c $Configuration `
	-r "win-x64" `
	-p:Platform="x64" `
	-p:PublishSingleFile=$PublishSingleFile `
	-p:SelfContained=$SelfContained `
	-p:PublishTrimmed=false `
	-p:PublishReadyToRun=$PublishReadyToRun `
	-o $OutputPath `
	Netch\Netch.csproj

if ($lastExitCode) { exit $lastExitCode } 
exit 0
