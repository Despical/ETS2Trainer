<#
ETS2Trainer
Copyright (C) 2026 Despical
SPDX-License-Identifier: GPL-3.0-or-later

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program. If not, see <https://www.gnu.org/licenses/>.
#>

[CmdletBinding()]
param(
    [string]$PublishDirectory = (Join-Path $PSScriptRoot '../dist'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../artifacts'),
    [string]$CompilerPath
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$publish = [IO.Path]::GetFullPath($PublishDirectory)
$output = [IO.Path]::GetFullPath($OutputDirectory)
$version = ([xml](Get-Content -LiteralPath (Join-Path $root 'src/ETS2Trainer/ETS2Trainer.csproj'))).Project.PropertyGroup.Version
if (-not (Test-Path -LiteralPath (Join-Path $publish 'ETS2Trainer.exe'))) {
    throw 'Publish the application with build.ps1 before building Setup.'
}
if ((Get-Item -LiteralPath (Join-Path $publish 'ETS2Trainer.exe')).VersionInfo.ProductVersion.Split('+')[0] -ne $version) {
    throw 'The published executable does not match the project version.'
}

if (-not $CompilerPath) {
    $toolDirectory = Join-Path $root 'artifacts/build-tools'
    $compilerDirectory = Join-Path $toolDirectory 'inno-7.1.0'
    $CompilerPath = Join-Path $compilerDirectory 'ISCC.exe'
    if (-not (Test-Path -LiteralPath $CompilerPath)) {
        New-Item -ItemType Directory -Path $toolDirectory -Force | Out-Null
        $download = Join-Path $toolDirectory 'innosetup-7.1.0-x64.exe'
        $expectedHash = '0362a383ed217d4c4239b5933866dd96d3eb2102737da92f80f6057a4b40df2f'
        if (-not (Test-Path -LiteralPath $download) -or
            (Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expectedHash) {
            Invoke-WebRequest -Uri 'https://github.com/jrsoftware/issrc/releases/download/is-7_1_0/innosetup-7.1.0-x64.exe' -OutFile $download
        }
        if ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expectedHash) {
            throw 'Inno Setup compiler checksum mismatch.'
        }
        $signature = Get-AuthenticodeSignature -LiteralPath $download
        if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notlike '*Pyrsys B.V.*') {
            throw 'The Inno Setup compiler signature could not be verified.'
        }
        $process = Start-Process -FilePath $download -ArgumentList @(
            '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/CURRENTUSER', '/PORTABLE=1', '/NOICONS', '/TASKS=',
            ('/DIR="' + $compilerDirectory + '"')
        ) -WindowStyle Hidden -Wait -PassThru
        if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $CompilerPath)) {
            throw "Inno Setup compiler extraction failed (exit code $($process.ExitCode))."
        }
    }
}

New-Item -ItemType Directory -Path $output -Force | Out-Null
& $CompilerPath ('/DSourceDir=' + $publish) ('/DAppVersion=' + $version) ('/O' + $output) (Join-Path $PSScriptRoot 'ETS2Trainer.iss')
if ($LASTEXITCODE -ne 0) { throw 'Setup compilation failed.' }
Write-Host (Join-Path $output "ETS2Trainer-$version-Setup-x64.exe")
