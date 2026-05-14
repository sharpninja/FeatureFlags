[CmdletBinding()]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $BuildArguments
)

$ErrorActionPreference = 'Stop'
$buildProject = Join-Path $PSScriptRoot 'build/Build.csproj'
dotnet run --project $buildProject -- @BuildArguments
exit $LASTEXITCODE
