# Сборка DccEnLayout.exe штатным csc .NET Framework (машина ничего не требует).
# Выход кладётся в Resources WPF-проекта: оттуда exe вшивается в FLOMASTER.exe
# как Resource (см. FLOMASTER.csproj) и извлекается EnLayoutService при включении опции.
$ErrorActionPreference = 'Stop'
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { throw "csc not found: $csc" }
$src  = "$PSScriptRoot\DccEnLayout.cs"
$out  = "$PSScriptRoot\..\..\FLOMASTER_CS\Resources\DccEnLayout.exe"
& $csc -nologo -target:winexe -r:System.Windows.Forms.dll -out:$out $src
if ($LASTEXITCODE -ne 0) { throw "csc failed: $LASTEXITCODE" }
$exe = Get-Item $out
Write-Host ("built: " + $exe.FullName + " (" + $exe.Length + " bytes, " + $exe.LastWriteTime + ")")
