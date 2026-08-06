$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$publish = Join-Path $root 'artifacts\publish\win-x64'
$installer = Join-Path $root 'artifacts\installer'

dotnet restore (Join-Path $root 'AFMS.sln')
dotnet test (Join-Path $root 'AFMS.sln') -c Release
dotnet publish (Join-Path $root 'AFMS.App\AFMS.App.csproj') -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o $publish

# Newer SDKs clear the publish folder, which drops this loose file. The app also
# carries an embedded copy, so it still runs without it, but ship the editable one
# so the office can adjust its starter items and people.
Copy-Item (Join-Path $root 'AFMS.App\starter-data.json') $publish -Force
Write-Host "Published to $publish"

$candidates = @(
  "$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe",
  "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
)
$compiler = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) { throw 'Inno Setup 6 was not found. Install it, then run this script again.' }
New-Item -ItemType Directory -Force -Path $installer | Out-Null
& $compiler (Join-Path $root 'installer\AFMS.iss')
Write-Host "Installer created in $installer"
