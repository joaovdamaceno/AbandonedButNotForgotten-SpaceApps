param(
  [Parameter(Mandatory = $true)][string]$Method,
  [Parameter(Mandatory = $true)][string]$LogName,
  [string[]]$Extra = @()
)
# Executa um metodo do Editor em batch e grava o log em QA/SUR-009/logs. Feche o Unity antes.
$project = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Unity.exe'
$logs = Join-Path $project 'QA\SUR-009\logs'
New-Item -ItemType Directory -Force $logs | Out-Null
$log = Join-Path $logs $LogName
$args = @('-batchmode', '-nographics', '-projectPath', $project, '-executeMethod', $Method, '-logFile', $log) + $Extra
$p = Start-Process -FilePath $unity -ArgumentList $args -PassThru -Wait
"exit $($p.ExitCode)" | Out-File (Join-Path $logs ($LogName + '.exit')) -Encoding ascii
Write-Host "exit $($p.ExitCode) log $log"
