param([string]$UnityEditorPath)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $projectRoot
try {
    & git rev-parse --show-toplevel | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Run this script from a cloned Git repository.' }
    & git lfs version
    if ($LASTEXITCODE -ne 0) { throw 'Install Git LFS, then run this script again.' }
    & git lfs install --local
    if ($LASTEXITCODE -ne 0) { throw 'Git LFS setup failed.' }
    & git config --local pull.ff only
    & git config --local fetch.prune true

    $versionLine = Get-Content -LiteralPath 'ProjectSettings/ProjectVersion.txt' |
        Where-Object { $_ -match '^m_EditorVersion: ' } | Select-Object -First 1
    $editorVersion = $versionLine -replace '^m_EditorVersion: ', ''
    $candidates = @()
    if ($UnityEditorPath) { $candidates += $UnityEditorPath }
    $candidates += "C:/Program Files/Unity/Hub/Editor/$editorVersion/Editor/Unity.exe"
    $candidates += "C:/Program Files/Unity Hub/Editor/$editorVersion/Editor/Unity.exe"
    $runningEditor = Get-Process Unity -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty Path
    if ($runningEditor) { $candidates += $runningEditor }
    $editorExe = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
        Select-Object -First 1
    if ($editorExe) {
        $mergeTool = Join-Path (Split-Path -Parent $editorExe) 'Data/Tools/UnityYAMLMerge.exe'
        if (Test-Path -LiteralPath $mergeTool -PathType Leaf) {
            $mergeTool = $mergeTool -replace '\\', '/'
            $driver = "'" + $mergeTool + "' merge -p %O %B %A %A"
            & git config --local merge.unityyamlmerge.name 'Unity Smart Merge'
            & git config --local merge.unityyamlmerge.driver $driver
            & git config --local merge.unityyamlmerge.recursive binary
            Write-Host 'Unity Smart Merge configured.'
        }
    } else {
        Write-Host "Unity $editorVersion not found yet. Re-run after installing it, or pass -UnityEditorPath C:/path/to/Editor/Unity.exe."
    }
    Write-Host 'Git LFS is ready. Run git lfs pull, then open the project in Unity Hub.'
} finally {
    Pop-Location
}
