param([switch]$Runtime)
$ErrorActionPreference = 'Stop'
$petRoot = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $petRoot
try {
    dotnet run --project NaiwaPet.Tests/NaiwaPet.Tests.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Interaction regression tests failed.' }
    dotnet build NaiwaPet/NaiwaPet.csproj -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
    if ($Runtime) {
        $petExecutable = (Resolve-Path NaiwaPet/bin/Release/net10.0-windows/NaiwaPet.exe).Path
        $petQaPath = Join-Path $petRoot 'qa'
        $petProcess = Start-Process -FilePath $petExecutable -ArgumentList @('--self-test', ('"' + $petQaPath + '"')) -WindowStyle Hidden -Wait -PassThru
        if ($petProcess.ExitCode -ne 0) { throw "Runtime checks failed. See $petQaPath/runtime-checks.json" }
        Get-Content -LiteralPath (Join-Path $petQaPath 'runtime-checks.json')
    }
} finally { Pop-Location }
