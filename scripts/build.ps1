$ErrorActionPreference = 'Stop'
$petRoot = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $petRoot
try {
    dotnet publish NaiwaPet/NaiwaPet.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
    if ($LASTEXITCODE -ne 0) { throw "Publish failed with exit code $LASTEXITCODE" }
    Get-FileHash -LiteralPath publish/NaiwaPet.exe -Algorithm SHA256
} finally { Pop-Location }
