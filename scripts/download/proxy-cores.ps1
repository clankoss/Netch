param([Parameter(Mandatory)][string]$OutputPath)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$cores = @(
    @{
        Url = 'https://github.com/XTLS/Xray-core/releases/download/v26.11.9/Xray-windows-64.zip'
        Hash = '1262522266337b824f07beb8e1375786b437683b3890105469088f8e17e0a69d'
        Executable = 'xray.exe'
    },
    @{
        Url = 'https://github.com/SagerNet/sing-box/releases/download/v1.14.4/sing-box-1.14.4-windows-amd64.zip'
        Hash = '99185d75d314a07dbba4a130c8ad986cbeb629a07927975636b341ca61f991df'
        Executable = 'sing-box.exe'
    }
)
$output = (Resolve-Path -LiteralPath $OutputPath).Path
foreach ($core in $cores) {
    $archivePath = Join-Path $output ($core.Executable + '.zip')
    try {
        Invoke-WebRequest -Uri $core.Url -OutFile $archivePath
        if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -ne $core.Hash) {
            throw "SHA256 mismatch for $($core.Executable)"
        }
        $archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
        try {
            $entry = $archive.Entries | Where-Object Name -EQ $core.Executable | Select-Object -First 1
            if (-not $entry) { throw "Missing $($core.Executable) in release archive" }
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, (Join-Path $output $core.Executable), $true)
            $license = $archive.Entries | Where-Object Name -In @('LICENSE', 'LICENSE.txt') | Select-Object -First 1
            if ($license) {
                $licensePath = Join-Path $output ($core.Executable + '.LICENSE.txt')
                [IO.Compression.ZipFileExtensions]::ExtractToFile($license, $licensePath, $true)
            }
        }
        finally { $archive.Dispose() }
    }
    finally { Remove-Item -LiteralPath $archivePath -Force -ErrorAction SilentlyContinue }
}
