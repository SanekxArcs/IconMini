$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'IconMini.csproj'
$output = Join-Path $PSScriptRoot 'dist\IconMini'
dotnet publish $project -c Release -p:Platform=x64 -o $output
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$check = Start-Process -FilePath (Join-Path $output 'IconMini.exe') -ArgumentList '--self-test' -WindowStyle Hidden -Wait -PassThru
if ($check.ExitCode -ne 0) { throw 'Conversion checks failed. See self-test-result.txt.' }
Compress-Archive -Path "$output\*" -DestinationPath (Join-Path $PSScriptRoot 'dist\IconMini-win-x64.zip') -Force
Write-Host "Ready: $output\IconMini.exe"
