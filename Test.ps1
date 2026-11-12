param([Parameter(Mandatory)][string]$GameDirectory)
$ErrorActionPreference='Stop'
$managed=Join-Path $GameDirectory 'Railroader_Data\Managed'
$testBuild=Join-Path $PSScriptRoot 'build\tests';$null=New-Item -ItemType Directory -Path $testBuild -Force
$sdk=& dotnet --list-sdks | Select-Object -Last 1
if($sdk -notmatch '^([^ ]+) \[(.+)\]'){throw '.NET SDK 8 or newer required.'}
$compiler=Join-Path (Join-Path $Matches[2] $Matches[1]) 'Roslyn\bincore\csc.dll'
$exe=Join-Path $testBuild 'CoreTests.dll'
$arguments=@('/nologo','/target:exe','/langversion:9','/nostdlib+','/utf8output',('/out:"'+$exe+'"'))
$dotnetRoot=Split-Path $Matches[2] -Parent
$referencePack=Get-ChildItem -LiteralPath (Join-Path $dotnetRoot 'packs\Microsoft.NETCore.App.Ref') -Directory | Where-Object Name -Like '8.0.*' | Sort-Object Name | Select-Object -Last 1
if(!$referencePack){throw '.NET 8 reference pack required.'}
Get-ChildItem -LiteralPath (Join-Path $referencePack.FullName 'ref\net8.0') -Filter '*.dll' | ForEach-Object {$arguments+='/reference:"'+$_.FullName+'"'}
$arguments+='/reference:"'+(Join-Path $managed 'Newtonsoft.Json.dll')+'"'
foreach($name in @('Domain.cs','ClientSession.cs','Archive.cs','Settings.cs','Participation.cs','Presentation.cs','ActionMenuController.cs','PerformanceTrace.cs','MessageTemplates.cs','RoleTestSession.cs')){$arguments+='"'+(Join-Path $PSScriptRoot ('src\'+$name))+'"'}
$arguments+='"'+(Join-Path $PSScriptRoot 'tests\CoreTests.cs')+'"'
$arguments+='"'+(Join-Path $PSScriptRoot 'tests\MetadataTests.cs')+'"'
$arguments+='"'+(Join-Path $PSScriptRoot 'tests\ActionMenuTests.cs')+'"'
$arguments+='"'+(Join-Path $PSScriptRoot 'tests\PresentationTests.cs')+'"'
$arguments+='"'+(Join-Path $PSScriptRoot 'tests\PerformanceTests.cs')+'"'
$arguments+='"'+(Join-Path $PSScriptRoot 'tests\RoleTestTests.cs')+'"'
$response=Join-Path $testBuild 'tests.rsp';[IO.File]::WriteAllLines($response,$arguments,[Text.UTF8Encoding]::new($false))
& dotnet exec $compiler '/noconfig' ('@'+$response);if($LASTEXITCODE -ne 0){throw 'Test compilation failed.'}
Copy-Item -LiteralPath (Join-Path $managed 'Newtonsoft.Json.dll') -Destination $testBuild -Force
$runtime=@{runtimeOptions=@{tfm='net8.0';framework=@{name='Microsoft.NETCore.App';version='8.0.0'}}}|ConvertTo-Json -Depth 5
[IO.File]::WriteAllText((Join-Path $testBuild 'CoreTests.runtimeconfig.json'),$runtime)
& dotnet $exe (Join-Path $testBuild 'artifacts') $GameDirectory $PSScriptRoot;if($LASTEXITCODE -ne 0){throw 'Core tests failed.'}
$languageExe=Join-Path $testBuild 'LanguageTests.dll'
$languageArguments=@($arguments | Where-Object {$_ -notmatch '^".*\.cs"$' -and $_ -notmatch '^/out:'})
$languageArguments+='/out:"'+$languageExe+'"'
foreach($name in @('src\Domain.cs','src\MessageTemplates.cs','src\Localization.cs','tests\LanguageDoubles.cs','tests\LanguageTests.cs')){$languageArguments+='"'+(Join-Path $PSScriptRoot $name)+'"'}
$languageResponse=Join-Path $testBuild 'language-tests.rsp';[IO.File]::WriteAllLines($languageResponse,$languageArguments,[Text.UTF8Encoding]::new($false))
& dotnet exec $compiler '/noconfig' ('@'+$languageResponse);if($LASTEXITCODE -ne 0){throw 'Language test compilation failed.'}
[IO.File]::WriteAllText((Join-Path $testBuild 'LanguageTests.runtimeconfig.json'),$runtime)
& dotnet $languageExe $PSScriptRoot;if($LASTEXITCODE -ne 0){throw 'Language regression tests failed.'}
$settingsExe=Join-Path $testBuild 'SettingsUiTests.dll'
$settingsArguments=@($arguments | Where-Object {$_ -notmatch '^".*\.cs"$' -and $_ -notmatch '^/out:'})
$settingsArguments+='/out:"'+$settingsExe+'"'
foreach($name in @('src\Domain.cs','src\ClientSession.cs','src\RoleTestSession.cs','src\MessageTemplates.cs','src\Settings.cs','src\NativeSettings.cs','src\SharedModsTab.cs','src\CommsBindings.cs','src\ActionMenuController.cs','tests\SettingsUiDoubles.cs','tests\PreferencesDoubles.cs','tests\SettingsUiTests.cs')){$settingsArguments+='"'+(Join-Path $PSScriptRoot $name)+'"'}
$settingsResponse=Join-Path $testBuild 'settings-ui-tests.rsp';[IO.File]::WriteAllLines($settingsResponse,$settingsArguments,[Text.UTF8Encoding]::new($false))
& dotnet exec $compiler '/noconfig' ('@'+$settingsResponse);if($LASTEXITCODE -ne 0){throw 'Settings UI test compilation failed.'}
[IO.File]::WriteAllText((Join-Path $testBuild 'SettingsUiTests.runtimeconfig.json'),$runtime)
& dotnet $settingsExe;if($LASTEXITCODE -ne 0){throw 'Settings UI regression tests failed.'}
$transportExe=Join-Path $testBuild 'TransportTests.dll'
$transportArguments=@($arguments | Where-Object {$_ -notmatch '^".*\.cs"$' -and $_ -notmatch '^/out:'})
$transportArguments+='/out:"'+$transportExe+'"'
foreach($name in @('src\SteamTransport.cs','src\PerformanceTrace.cs','tests\TransportDoubles.cs','tests\TransportTests.cs')){$transportArguments+='"'+(Join-Path $PSScriptRoot $name)+'"'}
$transportResponse=Join-Path $testBuild 'transport-tests.rsp';[IO.File]::WriteAllLines($transportResponse,$transportArguments,[Text.UTF8Encoding]::new($false))
& dotnet exec $compiler '/noconfig' ('@'+$transportResponse);if($LASTEXITCODE -ne 0){throw 'Transport test compilation failed.'}
[IO.File]::WriteAllText((Join-Path $testBuild 'TransportTests.runtimeconfig.json'),$runtime)
& dotnet $transportExe;if($LASTEXITCODE -ne 0){throw 'Transport regression tests failed.'}
