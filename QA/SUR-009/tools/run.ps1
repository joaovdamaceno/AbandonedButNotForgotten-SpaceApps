$unity = "C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Data"
$dotnet = "$unity\DotNetSdk\dotnet.exe"; $csc = "$unity\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll"
$sp = "C:\Users\KAUAWE~1\AppData\Local\Temp\claude\9d7992a366595832\cf1e887c-30bd-4b40-b880-6afbeef70e5f\scratchpad"
$out = "$sp\harness\out"; New-Item -ItemType Directory -Force $out | Out-Null
$q=[char]34
$ns = (Get-ChildItem "$unity\NetStandard\ref\2.1.0" -Filter *.dll -Recurse).FullName
$eng = (Get-ChildItem "$unity\Managed\UnityEngine" -Filter "UnityEngine*.dll").FullName
$srcs = @("$sp\harness\Shim.cs","$sp\harness\Program.cs","C:\www\AbandonedButNotForgotten-SpaceApps\Assets\_Game\Chapters\Surveyor\Scoop\Tests\SurveyorScoopMathTests.cs","C:\www\AbandonedButNotForgotten-SpaceApps\Assets\_Game\Chapters\Surveyor\Scripts\SurveyorScoop.cs")
# Dependências mínimas do runtime real (Core) para o SurveyorScoop compilar junto.
$game = (Get-ChildItem "C:\www\AbandonedButNotForgotten-SpaceApps\Assets\_Game" -Filter *.cs -Recurse | Where-Object { $_.FullName -notmatch '[\\/](Editor|Tests|Diagnostics|Examples|Chapters)[\\/]' }).FullName
$all = $srcs + $game
$a = @("-nologo","-nostdlib","-target:exe","-langversion:9.0","-warn:0","-define:ENABLE_INPUT_SYSTEM","-out:$q$out\h.dll$q") + ($ns | % { "-r:$q$_$q" }) + ($eng | % { "-r:$q$_$q" }) + @("-r:$q$sp\chk\Unity.InputSystem.dll$q") + ($all | % { "$q$_$q" })
$a | Set-Content "$out\h.rsp" -Encoding UTF8
& $dotnet $csc "@$out\h.rsp" 2>&1 | Out-String
'{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}' | Set-Content "$out\h.runtimeconfig.json"
Copy-Item "$sp\chk\Unity.InputSystem.dll" $out -Force
foreach ($d in $eng) { Copy-Item $d $out -Force }
& $dotnet "$out\h.dll" 2>&1 | Out-String
