# Pravi zip iz publish foldera i kači ga na GitHub Releases (prerelease, jer je beta).
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $MyInvocation.MyCommand.Path)

$verzija = (Select-String -Path '.\Bronxamp.csproj' -Pattern '<Version>([^<]+)</Version>').Matches[0].Groups[1].Value
if (-not $verzija) { throw 'Nema <Version> u Bronxamp.csproj.' }
Write-Host "Verzija: $verzija"

if (-not (Test-Path '.\publish\Bronxamp.exe')) {
    Write-Host 'Prvo pravim publish...'
    & '.\Napravi publish.bat'
    if (-not (Test-Path '.\publish\Bronxamp.exe')) { throw 'publish\Bronxamp.exe nije napravljen.' }
}

New-Item -ItemType Directory -Force '.\dist' | Out-Null
$zip = ".\dist\Bronxamp-$verzija-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Write-Host "Pakujem: $zip"
Compress-Archive -Path '.\publish\*' -DestinationPath $zip -CompressionLevel Optimal
$mb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host "Gotovo: $zip ($mb MB)"

$oznaka = "v$verzija"
$staro = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
gh release view $oznaka 2>&1 | Out-Null
$postoji = $LASTEXITCODE -eq 0
if ($postoji) {
    Write-Host "Release $oznaka postoji, dodajem fajl..."
    gh release upload $oznaka $zip --clobber
} else {
    Write-Host "Pravim release $oznaka..."
    gh release create $oznaka $zip --title "Bronxamp $verzija" --generate-notes --prerelease
}
$uspeh = $LASTEXITCODE -eq 0
$ErrorActionPreference = $staro
if (-not $uspeh) { throw 'Kačenje na GitHub nije uspelo.' }
Write-Host "Objavljeno: https://github.com/BronxSoft-021/Bronxamp/releases/tag/$oznaka"
