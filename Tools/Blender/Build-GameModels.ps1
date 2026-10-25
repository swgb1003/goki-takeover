param([switch]$SkipBlender, [switch]$RenderPreview)
$ErrorActionPreference = 'Stop'
$gokiProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$gokiBlenderExe = 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe'
$gokiUnityExe = 'C:/Program Files/Unity/Hub/Editor/6000.0.61f1/Editor/Unity.exe'
$gokiRunningEditor = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -like ('*'+$gokiProjectRoot+'*') }
if ($gokiRunningEditor) { throw 'このプロジェクトのUnity Editorを閉じてから実行してください。' }
Push-Location -LiteralPath $gokiProjectRoot
try {
    New-Item -ItemType Directory -Force 'Logs/BlenderModels' | Out-Null
    if (-not $SkipBlender) {
        $gokiBlenderArgs = @('--background','--threads','2','--python-exit-code','1','--python',(Join-Path $PSScriptRoot 'build_character_models.py'))
        if ($RenderPreview) { $gokiBlenderArgs += @('--','--render-preview') }
        & $gokiBlenderExe @gokiBlenderArgs
        if ($LASTEXITCODE -ne 0) { throw 'Blenderでのモデル生成に失敗しました。' }
    }
    $gokiUnityArgs = @('-batchmode','-job-worker-count','2','-projectPath',('"'+$gokiProjectRoot+'"'),'-executeMethod','GokiTakeover.Editor.BlenderModelVerification.GenerateVerifyAndBuild','-logFile',('"'+$gokiProjectRoot+'\Logs\BlenderModels\verify-build-final.log"'))
    $gokiUnityProcess = Start-Process -FilePath $gokiUnityExe -ArgumentList $gokiUnityArgs -WindowStyle Hidden -PassThru
    $gokiUnityProcess.PriorityClass = 'BelowNormal'
    $gokiUnityProcess.WaitForExit()
    if ($gokiUnityProcess.ExitCode -ne 0) { throw 'Unityでの検証・ビルドに失敗しました。Logs/BlenderModelsを確認してください。' }
    Write-Output 'Complete: Build/GokiTakeover_BlenderModels/GokiTakeover.exe'
} finally { Pop-Location }
