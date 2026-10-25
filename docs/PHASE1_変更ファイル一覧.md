# Phase 1 追加ファイル一覧

開始時にUnityプロジェクトは存在しなかったため、以下は全て新規追加。既存README・仕様書・プロンプト・PNG6枚は変更しない。Gitのステージング・コミットは行っていない。

## コード（全13ファイル）

```text
Assets/_GokiTakeover/
  Editor/
    GeneratedAssets.cs
    CockroachGeneratorEditor.cs
    KitchenStageGenerator.cs
    PhaseOneBuild.cs
    PhaseOneVerification.cs
  Scripts/
    Character/
      CockroachAppearance.cs
      CockroachVisualController.cs
      RoachMotor.cs
    Camera/
      ThirdPersonRoachCamera.cs
    Core/
      GokiInput.cs
      PhaseOneSession.cs
    UI/
      PhaseOneHud.cs
    Performance/
      PhaseOneSmokeTest.cs
```

## 生成データ

```text
Assets/_GokiTakeover/
  Art/Meshes/
    RoachSphere.asset
    RoachRod.asset
  Art/Materials/
    RoachBody.mat
    RoachShell.mat
    RoachLimbs.mat
    RoachEyes.mat
    KitchenWall.mat
    KitchenWood.mat
    KitchenOak.mat
    KitchenCream.mat
    KitchenDark.mat
    KitchenRed.mat
    KitchenTileA.mat
    KitchenTileB.mat
    MarkerLime.mat
    MarkerBread.mat
    MarkerHuman.mat
  Data/
    CockroachAppearance.asset
  Prefabs/
    Cockroach_Player.prefab
  Scenes/
    Phase1_Kitchen_Test.unity
```

各コード・アセットの `.meta`、フォルダの `.meta`、`Assets/_GokiTakeover.meta` も追加。GUIDと生成物ラベルの保持に必要なためGitに含める。

## 新規Unityプロジェクト基盤

```text
.gitignore
Packages/manifest.json
Packages/packages-lock.json
ProjectSettings/
  AudioManager.asset
  ClusterInputManager.asset
  DynamicsManager.asset
  EditorBuildSettings.asset
  EditorSettings.asset
  GraphicsSettings.asset
  InputManager.asset
  MemorySettings.asset
  MultiplayerManager.asset
  NavMeshAreas.asset
  Physics2DSettings.asset
  PresetManager.asset
  ProjectSettings.asset
  ProjectVersion.txt
  QualitySettings.asset
  SceneTemplateSettings.json
  TagManager.asset
  TimeManager.asset
  UnityConnectSettings.asset
  VersionControlSettings.asset
  VFXManager.asset
```

ProjectSettingsはUnityの空プロジェクト生成による標準設定。そこからPlayerSettingsの名前・1280×720・ウィンドウ、およびQualitySettingsのWindows既定Lowを設定した。レンダーパイプラインはBuilt-in、入力方式は標準Input Manager。

## ドキュメント・検証証拠

```text
docs/PHASE0_調査.md
docs/PHASE1_実装と検証.md
docs/PHASE1_変更ファイル一覧.md
docs/PHASE1_検証結果.md
docs/THIRD_PARTY_ASSETS.md
docs/Validation/phase1-player-720p.png
docs/Validation/phase1-performance.json
```

ビルド成果物 `Build/GokiTakeover_Phase1/`、実行ログ `Logs/`、Library/Temp/UserSettingsはローカル検証用でGit除外。
