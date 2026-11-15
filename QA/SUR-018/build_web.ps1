$ErrorActionPreference = 'Stop'
$surRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$surUnityPath = 'C:/Program Files/Unity/Hub/Editor/6000.6.7f1/Editor/Unity.exe'
$surBackup = Join-Path $surRoot 'QA/Local/SUR018SettingsBackup'
New-Item -ItemType Directory -Force $surBackup | Out-Null
$surSettings = @('GraphicsSettings.asset','QualitySettings.asset','ProjectSettings.asset','PackageManagerSettings.asset')
foreach ($surFile in $surSettings) {
    $surSource = Join-Path $surRoot ('ProjectSettings/' + $surFile)
    if (Test-Path -LiteralPath $surSource) { Copy-Item -LiteralPath $surSource -Destination (Join-Path $surBackup $surFile) }
}
try {
    $surArgs = @('-batchmode','-nographics','-buildTarget','WebGL','-projectPath',$surRoot,'-executeMethod','Abandoned.SpaceApps.EditorTools.SurveyorChapterWebBuild.BuildFromCommandLine','-logFile',(Join-Path $surRoot 'QA/SUR-018/build-web.log'))
    $surBuild = Start-Process -FilePath $surUnityPath -ArgumentList $surArgs -WindowStyle Hidden -PassThru
    $surBuild.WaitForExit()
    if ($surBuild.ExitCode -ne 0) { throw "Unity Web build exit $($surBuild.ExitCode). See QA/SUR-018/build-web.log" }
}
finally {
    foreach ($surFile in $surSettings) {
        $surSaved = Join-Path $surBackup $surFile
        if (Test-Path -LiteralPath $surSaved) { Copy-Item -LiteralPath $surSaved -Destination (Join-Path $surRoot ('ProjectSettings/' + $surFile)) }
    }
}
