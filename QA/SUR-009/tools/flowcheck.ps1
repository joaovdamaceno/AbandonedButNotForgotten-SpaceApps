$unity = "C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Data"
$dotnet = "$unity\DotNetSdk\dotnet.exe"; $csc = "$unity\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll"
$sp = "C:\Users\KAUAWE~1\AppData\Local\Temp\claude\9d7992a366595832\cf1e887c-30bd-4b40-b880-6afbeef70e5f\scratchpad"
$q=[char]34
$ns = (Get-ChildItem "$unity\NetStandard\ref\2.1.0" -Filter *.dll -Recurse).FullName
$eng = (Get-ChildItem "$unity\Managed\UnityEngine" -Filter "UnityEngine*.dll").FullName
$src = @("$sp\harness\Shim.cs","$sp\harness\FlowStubs.cs","C:\www\AbandonedButNotForgotten-SpaceApps\Assets\_Game\Chapters\Surveyor\Scoop\Tests\SurveyorScoopFlowTests.cs")
$a = @("-nologo","-nostdlib","-target:library","-langversion:9.0","-warn:0","-out:$q$sp\harness\out\flow.dll$q") + ($ns | % { "-r:$q$_$q" }) + ($eng | % { "-r:$q$_$q" }) + @("-r:$q$sp\chk\Unity.InputSystem.dll$q","-r:$q$sp\chk\Abandoned.SpaceApps.dll$q") + ($src | % { "$q$_$q" })
$a | Set-Content "$sp\harness\out\flow.rsp" -Encoding UTF8
& $dotnet $csc "@$sp\harness\out\flow.rsp" 2>&1 | Out-String
