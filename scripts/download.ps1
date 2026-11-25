param([Parameter(Mandatory)][string]$OutputPath)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
$output = (Resolve-Path -LiteralPath $OutputPath).Path
$archive = Join-Path $output 'netch-1.9.0-assets.7z'
try {
    Invoke-WebRequest -Uri 'https://github.com/netchx/netch/releases/download/1.9.0/Netch.7z' -OutFile $archive
    $expectedHash = '29f413f49edb4048f2cc650959f2addaf788410e05a987b941cf88fdfc813c41'
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expectedHash) {
        throw 'Netch 1.9.0 archive SHA256 mismatch'
    }

    tar.exe --exclude=Netch/bin/Trojan.exe -xf $archive -C $output --strip-components 1 Netch/bin
    if ($LASTEXITCODE -ne 0) { throw 'Could not extract Netch 1.9.0 assets' }

    & (Join-Path $PSScriptRoot 'download/proxy-cores.ps1') -OutputPath (Join-Path $output 'bin')
}
finally {
    Remove-Item -LiteralPath $archive -Force -ErrorAction SilentlyContinue
}
