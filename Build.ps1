param([Parameter(Mandatory)][string]$GameDirectory,[string]$OutputDirectory=(Join-Path $PSScriptRoot 'build'),[switch]$Package)
$ErrorActionPreference='Stop'
$managed=Join-Path $GameDirectory 'Railroader_Data\Managed'
$sdk=& dotnet --list-sdks | Select-Object -Last 1
if($sdk -notmatch '^([^ ]+) \[(.+)\]'){throw '.NET SDK 8 or newer required.'}
$compiler=Join-Path (Join-Path $Matches[2] $Matches[1]) 'Roslyn\bincore\csc.dll'
$null=New-Item -ItemType Directory -Path $OutputDirectory -Force
$pack=Join-Path $PSScriptRoot 'EnusDE-Communications'
$dll=Join-Path $OutputDirectory 'EnusDE.Communications.dll'
$references=@('mscorlib.dll','System.dll','System.Core.dll','netstandard.dll','Railroader.Modding.dll','Railroader.dll','Core.dll','Markroader.dll','Newtonsoft.Json.dll','KeyValue.Runtime.dll','AssetPack.Common.dll','UnityEngine.CoreModule.dll','UnityEngine.UIModule.dll','UnityEngine.UI.dll','Unity.TextMeshPro.dll','UnityEngine.TextRenderingModule.dll','Unity.InputSystem.dll','UnityEngine.AudioModule.dll','UnityEngine.SharedInternalsModule.dll','com.rlabrecque.steamworks.net.dll')
$argsList=@('/nologo','/target:library','/langversion:9','/nostdlib+','/optimize+','/deterministic+','/utf8output',('/out:"'+$dll+'"'))
foreach($name in $references){$path=Join-Path $managed $name;if(!(Test-Path -LiteralPath $path)){throw "Missing game reference: $name"};$argsList+='/reference:"'+$path+'"'}
$harmony=Join-Path $PSScriptRoot 'dependencies\Harmony\0Harmony.dll'
$argsList+='/reference:"'+$harmony+'"'
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | Sort-Object Name | ForEach-Object {$argsList+='"'+$_.FullName+'"'}
$response=Join-Path $OutputDirectory 'compiler.rsp';[IO.File]::WriteAllLines($response,$argsList,[Text.UTF8Encoding]::new($false))
& dotnet exec $compiler '/noconfig' ('@'+$response)
if($LASTEXITCODE -ne 0){throw 'Mod compilation failed.'}
if(!$Package){Get-Item -LiteralPath $dll | Select-Object FullName,Length;return}
$manifest=Get-Content -LiteralPath (Join-Path $pack 'Info.json') -Raw | ConvertFrom-Json
$staging=Join-Path $OutputDirectory ('stage-'+[Guid]::NewGuid().ToString('N'))
$modStage=Join-Path $staging 'EnusDE-Communications';$null=New-Item -ItemType Directory -Path $modStage -Force
Copy-Item -LiteralPath (Join-Path $pack 'Info.json') -Destination $modStage
Copy-Item -LiteralPath (Join-Path $pack 'Languages') -Destination $modStage -Recurse
Copy-Item -LiteralPath $dll,$harmony -Destination $modStage
$modDocuments=@('README.md','BUILDING.md','PERFORMANCE.md','MESSAGE-BUILDER.md','ROLE-TEST.md','LICENSE','THIRD-PARTY-NOTICES.txt')
foreach($name in $modDocuments){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $modStage}
$binaryZip=Join-Path $OutputDirectory ('EnusDE-Communications-v'+$manifest.version+'.zip');Compress-Archive -LiteralPath $modStage -DestinationPath $binaryZip -Force
$sourceStage=Join-Path $staging 'EnusDE-Communications-Source';$null=New-Item -ItemType Directory -Path $sourceStage -Force
foreach($name in @('src','EnusDE-Communications')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $sourceStage -Recurse}
$dependencyStage=Join-Path $sourceStage 'dependencies';$null=New-Item -ItemType Directory -Path $dependencyStage -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'dependencies\Harmony') -Destination $dependencyStage -Recurse
$testStage=Join-Path $sourceStage 'tests';$null=New-Item -ItemType Directory -Path $testStage -Force
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'tests') -File | Where-Object Extension -In @('.cs','.ps1') | Copy-Item -Destination $testStage
foreach($name in ($modDocuments+@('Build.ps1','Test.ps1','Test-Native.ps1','.gitignore','.gitattributes'))){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $sourceStage}
$sourceZip=Join-Path $OutputDirectory ('EnusDE-Communications-Source-v'+$manifest.version+'.zip');Compress-Archive -LiteralPath $sourceStage -DestinationPath $sourceZip -Force
Get-Item -LiteralPath $binaryZip,$sourceZip | Select-Object Name,Length
