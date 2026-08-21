[CmdletBinding()]
param(
    [string]$UnityPath = 'D:\UnityEditors\6000.0.36f1\Editor\Unity.exe',
    [string]$ProjectPath = (Join-Path $PSScriptRoot '..\UnityTimelineBuilder')
)

$ErrorActionPreference = 'Stop'
$ProjectPath = [IO.Path]::GetFullPath($ProjectPath)
$OutputRelativePath = 'Assets/UnityTimelineBuilder/BatchAcceptanceTemp'
$OutputPath = Join-Path $ProjectPath ($OutputRelativePath -replace '/', '\\')
$InputPath = Join-Path $OutputPath 'batch-acceptance.csv'
$LogRoot = Join-Path ([IO.Path]::GetTempPath()) 'unity-timeline-builder-cli-acceptance'
$Method = 'Hidano.UnityTimelineBuilder.Editor.TimelineBuilderCli.Build'
$Results = [Collections.Generic.List[object]]::new()

function Invoke-UnityCase {
    param(
        [string]$Name,
        [string[]]$Arguments,
        [int]$ExpectedExitCode,
        [string[]]$RequiredLogPatterns
    )

    $logPath = Join-Path $LogRoot ($Name + '.log')
    $commandArguments = @('-batchmode', '-automated', '-projectPath', $ProjectPath,
        '-executeMethod', $Method, '-logFile', $logPath) + $Arguments
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $UnityPath
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.Arguments = (($commandArguments | ForEach-Object {
        '"' + $_.Replace('"', '\\"') + '"'
    }) -join ' ')
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    if (-not $process.Start()) {
        throw "Could not start Unity for $Name."
    }
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    $process.WaitForExit()
    $stdout.GetAwaiter().GetResult() | Write-Verbose
    $stderr.GetAwaiter().GetResult() | Write-Verbose
    $exitCode = $process.ExitCode
    $process.Dispose()
    if ($exitCode -ne $ExpectedExitCode) {
        throw "$Name returned exit code $exitCode; expected $ExpectedExitCode."
    }
    if (-not (Test-Path -LiteralPath $logPath)) {
        throw "$Name did not produce a Unity log."
    }
    $log = Get-Content -Raw -LiteralPath $logPath
    foreach ($pattern in $RequiredLogPatterns) {
        if ($log -notmatch $pattern) {
            throw "$Name log does not contain required pattern: $pattern"
        }
    }
    $Results.Add([pscustomobject]@{ Name = $Name; ExitCode = $exitCode })
}

try {
    if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
        throw "Unity Editor was not found: $UnityPath"
    }
    if (-not (Test-Path -LiteralPath $ProjectPath -PathType Container)) {
        throw "Unity project was not found: $ProjectPath"
    }

    New-Item -ItemType Directory -Force -Path $OutputPath, $LogRoot | Out-Null
    Set-Content -LiteralPath $InputPath -Encoding utf8 -Value @(
        'trackType,trackName,clipName,startTime,clipIn,duration,resourcePath'
        'Scene,BatchAcceptanceScene,,,,,'
    )

    Invoke-UnityCase -Name 'success' -ExpectedExitCode 0 -Arguments @(
        '-sheetPath', $InputPath,
        '-outputDir', $OutputRelativePath,
        '-assetName', 'batch-acceptance'
    ) -RequiredLogPatterns @(
        '\[UnityTimelineBuilder\].*TimelineAsset: Assets/UnityTimelineBuilder/BatchAcceptanceTemp/batch-acceptance\.playable',
        '\[UnityTimelineBuilder\].*Prefab: Assets/UnityTimelineBuilder/BatchAcceptanceTemp/batch-acceptance\.prefab',
        '\[UnityTimelineBuilder\].*Scene: Assets/UnityTimelineBuilder/BatchAcceptanceTemp/BatchAcceptanceScene\.unity'
    )

    Invoke-UnityCase -Name 'build-failure' -ExpectedExitCode 1 -Arguments @(
        '-sheetPath', (Join-Path $OutputRelativePath 'missing-sheet.csv'),
        '-outputDir', $OutputRelativePath
    ) -RequiredLogPatterns @(
        '\[UnityTimelineBuilder\].*SheetNotFound',
        '\[UnityTimelineBuilder\].*missing-sheet\.csv'
    )

    Invoke-UnityCase -Name 'argument-failure' -ExpectedExitCode 2 -Arguments @(
        '-sheetPath', $InputPath
    ) -RequiredLogPatterns @(
        '\[UnityTimelineBuilder\].*'
    )

    $playable = Join-Path $OutputPath 'batch-acceptance.playable'
    $prefab = Join-Path $OutputPath 'batch-acceptance.prefab'
    $scene = Join-Path $OutputPath 'BatchAcceptanceScene.unity'
    if (-not (Test-Path -LiteralPath $playable) -or
        -not (Test-Path -LiteralPath $prefab) -or
        -not (Test-Path -LiteralPath $scene)) {
        throw 'The successful batch run did not create the expected TimelineAsset, Prefab, and Scene.'
    }
    if ($Results.Count -ne 3) {
        throw 'Not all CLI acceptance cases were executed.'
    }
    Write-Output 'CLI batch acceptance verification passed: success=0, build-failure=1, argument-failure=2.'
}
finally {
    if (Test-Path -LiteralPath $OutputPath) {
        Remove-Item -LiteralPath $OutputPath -Recurse -Force
    }
    if (Test-Path -LiteralPath $LogRoot) {
        Remove-Item -LiteralPath $LogRoot -Recurse -Force
    }
}
