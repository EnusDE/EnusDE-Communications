param(
    [Parameter(Mandatory)][string]$GameDirectory,
    [string]$TranslationModDirectory
)
$ErrorActionPreference='Stop'
if(Get-Process Railroader -ErrorAction SilentlyContinue){throw 'Close Railroader before running the isolated smoke test.'}
if(!(Get-Process steam -ErrorAction SilentlyContinue)){throw 'Start Steam and sign in before running the isolated native test.'}
& (Join-Path $PSScriptRoot 'Build.ps1') -GameDirectory $GameDirectory
$smoke=Join-Path $PSScriptRoot ('build\native-smoke-'+[Guid]::NewGuid().ToString('N'))
$profile=Join-Path $smoke 'home';$mods=Join-Path $profile 'Mods';$mod=Join-Path $mods 'EnusDE-Communications';$companion=Join-Path $mods 'NativeSmoke'
$null=New-Item -ItemType Directory -Path $mod,$companion -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'EnusDE-Communications\Info.json'),(Join-Path $PSScriptRoot 'build\EnusDE.Communications.dll'),(Join-Path $PSScriptRoot 'dependencies\Harmony\0Harmony.dll') -Destination $mod
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'EnusDE-Communications\Languages') -Destination $mod -Recurse
$manifest=Get-Content -LiteralPath (Join-Path $mod 'Info.json') -Raw | ConvertFrom-Json
$enabled=@(@{identifier=$manifest.identifier;version=$manifest.version;enabled=$true;scope=1},@{identifier='enusde/communications-smoke';version=$manifest.version;enabled=$true;scope=1})
if($TranslationModDirectory){
    $translation=Get-Content -LiteralPath (Join-Path $TranslationModDirectory 'Info.json') -Raw | ConvertFrom-Json
    if($translation.identifier -ne 'enusde/german-translation'){throw 'Supply the native EnusDE Translation mod folder.'}
    Copy-Item -LiteralPath $TranslationModDirectory -Destination (Join-Path $mods 'EnusDE-Translation-German') -Recurse
    $enabled+=@{identifier=$translation.identifier;version=$translation.version;enabled=$true;scope=1}
}
$lines=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'build\compiler.rsp') | Where-Object {$_ -notmatch '^".*\.cs"$' -and $_ -notmatch '^/out:'}
$lines+='/out:"'+(Join-Path $companion 'NativeSmoke.dll')+'"';$lines+='/reference:"'+(Join-Path $mod 'EnusDE.Communications.dll')+'"';$lines+='"'+(Join-Path $PSScriptRoot 'tests\NativeSmoke.cs')+'"'
$response=Join-Path $smoke 'smoke.rsp';[IO.File]::WriteAllLines($response,$lines)
$sdk=& dotnet --list-sdks | Select-Object -Last 1;if($sdk -notmatch '^([^ ]+) \[(.+)\]'){throw '.NET SDK required.'}
$compiler=Join-Path (Join-Path $Matches[2] $Matches[1]) 'Roslyn\bincore\csc.dll'
& dotnet exec $compiler '/noconfig' ('@'+$response);if($LASTEXITCODE -ne 0){throw 'Native smoke companion failed to compile.'}
@{identifier='enusde/communications-smoke';version=$manifest.version;name='Communications isolated smoke test';scope='optional';assemblies=@('NativeSmoke.dll');dependencies=@{required=@('enusde/communications =='+$manifest.version)}}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $companion 'Info.json')
@{version=1;mods=$enabled;clientMods=@();settings=@{'mods.allow-dll'=1;'prefs.version'='0.6.3';'http.server.enabled'=0}}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $profile 'Preferences.json')
$log=Join-Path $smoke 'Player.log'
$process=Start-Process -FilePath (Join-Path $GameDirectory 'Railroader.exe') -WorkingDirectory $GameDirectory -ArgumentList @('-batchmode','-nographics','-homedir',('"'+$profile+'"'),'-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
$deadline=[DateTime]::UtcNow.AddSeconds(90)
while(!$process.HasExited -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 500;$process.Refresh()}
if(!$process.HasExited){$process.Kill();throw "Isolated game startup timed out. Log: $log"}
$text=if(Test-Path -LiteralPath $log){Get-Content -LiteralPath $log -Raw}else{''}
if(([regex]::Matches($text,'NATIVE-SMOKE PASS:')).Count -ne 2){throw "Native smoke did not finish. Inspect $log; this is not a passed test."}
Write-Output "Native smoke passed. Log: $log"
