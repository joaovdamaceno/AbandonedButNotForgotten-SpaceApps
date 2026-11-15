param([ValidateSet('Scene','Tests','Web')][string]$Mode = 'Tests')
$ErrorActionPreference = 'Stop'
$surRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$surBackup = Join-Path $surRoot 'QA/Local/SUR020SettingsBackup'
New-Item -ItemType Directory -Force $surBackup | Out-Null
$surPaths = @('ProjectSettings/GraphicsSettings.asset','ProjectSettings/QualitySettings.asset','ProjectSettings/ProjectSettings.asset','ProjectSettings/PackageManagerSettings.asset','Assets/_Game/Chapters/Surveyor/Art/ScoopGenerated/ScoopGrain.mat','Assets/_Game/Settings/Web/WebLight.asset')
for ($surI=0; $surI -lt $surPaths.Count; $surI++) {
    $surSource = Join-Path $surRoot $surPaths[$surI]
    if (Test-Path -LiteralPath $surSource) { Copy-Item -LiteralPath $surSource -Destination (Join-Path $surBackup ($surI.ToString()+'.saved')) }
}
try {
    $surArgs = @('-batchmode','-buildTarget','WebGL','-projectPath',$surRoot,'-logFile',(Join-Path $PSScriptRoot ($Mode+'.log')))
    if ($Mode -eq 'Tests') { $surArgs += @('-force-d3d11','-runTests','-testPlatform','EditMode','-testFilter','PrimaryInstrument;SurveyorSoilFrameRate;SurveyorChapterTests','-testResults',(Join-Path $PSScriptRoot 'editor-results.xml')) }
    elseif ($Mode -eq 'Scene') { $surArgs += @('-nographics','-executeMethod','Abandoned.SpaceApps.EditorTools.SurveyorChapterBuilder.BuildFromCommandLine') }
    else { $surArgs += @('-nographics','-executeMethod','Abandoned.SpaceApps.EditorTools.SurveyorChapterWebBuild.BuildFromCommandLine') }
    $surProcess = Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/6000.6.7f1/Editor/Unity.exe' -ArgumentList $surArgs -WindowStyle Hidden -PassThru
    $surProcess.WaitForExit()
    if ($surProcess.ExitCode -ne 0) { throw "Unity $Mode returned $($surProcess.ExitCode). See QA/SUR-020/$Mode.log" }
}
finally {
    for ($surI=0; $surI -lt $surPaths.Count; $surI++) {
        $surSaved = Join-Path $surBackup ($surI.ToString()+'.saved')
        if (Test-Path -LiteralPath $surSaved) { Copy-Item -LiteralPath $surSaved -Destination (Join-Path $surRoot $surPaths[$surI]) }
    }
}
