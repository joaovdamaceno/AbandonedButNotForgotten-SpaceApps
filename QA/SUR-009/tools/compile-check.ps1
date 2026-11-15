param(
  [string]$Stage = "all"
)
$ErrorActionPreference = "Stop"
$unity = "C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Data"
$dotnet = "$unity\DotNetSdk\dotnet.exe"
$csc = "$unity\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll"
$repo = "C:\www\AbandonedButNotForgotten-SpaceApps"
$out = "C:\Users\KAUAWE~1\AppData\Local\Temp\claude\9d7992a366595832\cf1e887c-30bd-4b40-b880-6afbeef70e5f\scratchpad\chk"
New-Item -ItemType Directory -Force $out | Out-Null
$netstandard = (Get-ChildItem "$unity\NetStandard\ref\2.1.0" -Filter *.dll -Recurse | Select-Object -First 400).FullName
$engineDir = "$unity\Managed\UnityEngine"
$engine = (Get-ChildItem $engineDir -Filter "UnityEngine*.dll").FullName
$ui = (Get-ChildItem "$unity\Resources\PackageManager\ProjectTemplates\libcache\com.unity.template.urp-blank-17.2.6\ScriptAssemblies" -Filter "UnityEngine.UI.dll").FullName
$common = @("-nologo","-nostdlib","-noconfig","-target:library","-langversion:9.0","-nullable:disable","-warn:0","-nowarn:0618,0649,0169,0414,0219,8632","-define:UNITY_2023_1_OR_NEWER,UNITY_6000_0_OR_NEWER,UNITY_6000_6_OR_NEWER,ENABLE_INPUT_SYSTEM,UNITY_WEBGL_NO,DEBUG")
function Run($rspName, $argsList) {
  $rsp = "$out\$rspName.rsp"
  $argsList | Set-Content -Path $rsp -Encoding UTF8
  & $dotnet $csc "@$rsp" 2>&1 | Out-String
}
if ($Stage -eq "input" -or $Stage -eq "all") {
  $is = (Get-ChildItem "$repo\Library\PackageCache\com.unity.inputsystem@*\InputSystem" -Filter *.cs -Recurse | Where-Object { $_.FullName -notmatch "\\(Editor|InputForUI)\\" }).FullName
  $q = [char]34
  $a = @($common) + @("-unsafe","-out:$q$out\Unity.InputSystem.dll$q") + ($netstandard | ForEach-Object { "-r:$q$_$q" }) + ($engine | ForEach-Object { "-r:$q$_$q" }) + @("-r:$q$ui$q") + ($is | ForEach-Object { "$q$_$q" })
  Run "input" $a
}
if ($Stage -eq "game" -or $Stage -eq "all") {
  $src = (Get-ChildItem "$repo\Assets\_Game" -Filter *.cs -Recurse | Where-Object { $_.FullName -notmatch "\\(Editor|Tests|Diagnostics|Examples)\\" }).FullName
  $q = [char]34
  $a = @($common) + @("-out:$q$out\Abandoned.SpaceApps.dll$q") + ($netstandard | ForEach-Object { "-r:$q$_$q" }) + ($engine | ForEach-Object { "-r:$q$_$q" }) + @("-r:$q$ui$q","-r:$q$out\Unity.InputSystem.dll$q") + ($src | ForEach-Object { "$q$_$q" })
  Run "game" $a
}
if ($Stage -eq "editor") {
  $q = [char]34
  $ed = (Get-ChildItem "$unity\Managed" -Filter "UnityEditor*.dll").FullName
  $src = @("$repo\Assets\_Game\Chapters\Surveyor\Art\Editor\SurveyorSiteGeometry.cs","$repo\Assets\_Game\Chapters\Surveyor\Scoop\Editor\SurveyorScoopBuilder.cs") + (Get-ChildItem "$repo\Assets\_Game\Chapters\Surveyor\Scoop\Editor" -Filter *.cs).FullName | Select-Object -Unique
  $a = @($common) + @("-out:$q$out\ed.dll$q") + ($netstandard | ForEach-Object { "-r:$q$_$q" }) + ($engine | ForEach-Object { "-r:$q$_$q" }) + ($ed | ForEach-Object { "-r:$q$_$q" }) + @("-r:$q$out\Abandoned.SpaceApps.dll$q","-r:$q$out\Unity.InputSystem.dll$q") + ($src | ForEach-Object { "$q$_$q" })
  Run "editor" $a
}
