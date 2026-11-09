param([string]$DotnetRoot = $env:DOTNET_ROOT)
$ErrorActionPreference = 'Stop'
$output = Join-Path $PSScriptRoot 'src/Apps/KeyAsio/bin/Release/net10.0'
$executable = Join-Path $output 'KeyASIO.exe'
if (-not (Test-Path -LiteralPath $executable)) {
    throw 'Build src/Apps/KeyAsio/KeyAsio.csproj in Release configuration first.'
}
if ($DotnetRoot) {
    if (-not (Test-Path -LiteralPath (Join-Path $DotnetRoot 'dotnet.exe'))) {
        throw 'DotnetRoot must point to a .NET SDK/runtime directory.'
    }
    $env:DOTNET_ROOT = $DotnetRoot
    $env:DOTNET_ROOT_X64 = $DotnetRoot
}
Start-Process -FilePath $executable -WorkingDirectory $output
