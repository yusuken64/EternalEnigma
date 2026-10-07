$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$playerPath = Join-Path $projectRoot 'Builds/AuditVerification/EternalEnigma.exe'
$evidenceRoot = Join-Path $projectRoot 'Docs/Art/Previews/Diorama/Verification'
$logPath = Join-Path $evidenceRoot 'WindowsPlayer.log'
if (-not (Test-Path -LiteralPath $playerPath)) { throw 'Build the Windows development player first.' }
[IO.Directory]::CreateDirectory($evidenceRoot) | Out-Null
$started = [DateTime]::UtcNow
$playerProcess = Start-Process -FilePath $playerPath -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru -ArgumentList @(
    '--diorama-validation', '-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '800',
    '-logFile', ('"' + $logPath + '"'))
Write-Output "Windows validation process: $($playerProcess.Id)"
try {
    while (-not $playerProcess.WaitForExit(30000)) {
        Write-Output 'Windows validation is running.'
        if ([DateTime]::UtcNow - $started -gt [TimeSpan]::FromMinutes(12)) {
            throw 'Windows validation timed out; inspect WindowsPlayer.log.'
        }
    }
    $reportLine = Get-Content -LiteralPath $logPath | Where-Object { $_.StartsWith('DIORAMA_PLAYER_JSON ') } | Select-Object -Last 1
    if (-not $reportLine) { throw "Player exited without a report (exit $($playerProcess.ExitCode)). Inspect WindowsPlayer.log." }
    $json = $reportLine.Substring('DIORAMA_PLAYER_JSON '.Length)
    [IO.File]::WriteAllText((Join-Path $evidenceRoot 'WindowsPlayerValidation.json'), $json, [Text.UTF8Encoding]::new($false))
    $report = $json | ConvertFrom-Json
    $companyRoot = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'AppData/LocalLow/JuicyChickenGames'
    $screenshots = @(Get-ChildItem -LiteralPath $companyRoot -Recurse -File -Filter 'DioramaPlayer_*.png' |
        Where-Object { $_.LastWriteTimeUtc -ge $started })
    foreach ($screenshot in $screenshots) {
        Copy-Item -LiteralPath $screenshot.FullName -Destination (Join-Path $evidenceRoot ($screenshot.Name.Replace('DioramaPlayer_', 'Windows_')))
    }
    $report.samples | Format-Table scene, medianMs, p95Ms, triangles, renderers, materials, errorMaterials
    if ($playerProcess.ExitCode -ne 0 -or $report.errors.Count -ne 0 -or $report.samples.Count -ne 3 -or
        @($report.samples | Where-Object { $_.errorMaterials -ne 0 }).Count -ne 0 -or $screenshots.Count -ne 3) {
        throw 'Windows player validation failed; inspect the saved report and log.'
    }
    Write-Output 'Windows player validation passed; three scene captures and the raw report are saved.'
}
finally {
    if (-not $playerProcess.HasExited) { $playerProcess.Kill() }
    $playerProcess.Dispose()
}
