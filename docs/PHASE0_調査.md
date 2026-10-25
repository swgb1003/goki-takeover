# Phase 0 調査・採用構成

調査日: 2026-10-22。実装基準は `docs/GOKI_TAKEOVER_統合開発仕様書_v1.0.md` 全文。

## 開始時の状態

| 項目 | 結果 |
|---|---|
| 開始ディレクトリ | `C:/Users/swgb1/orca/projects/ゴキ` |
| Unityプロジェクト | 未作成。Assets、Packages、ProjectSettings、C#、シーン、既存入力設定なし |
| 既存データ | docsの仕様・開始プロンプト、DesignReferencesのPNG6枚、README |
| Git | master / `2c7cb15 Initial commit`。既存資料は未追跡。削除・移動・自動コミットをしない |
| AGENTS.md | 作業フォルダ内および祖先に該当なし |
| インストール済みUnity | 6000.0.61f1、6000.2.10f1 |
| 採用Unity | 6000.0.61f1 / revision `74a0adb02c31`。バッチ起動・ライセンス確認成功 |
| 実機CPU | Intel Core i7-10510U @ 1.80GHz（WMI取得値） |
| 実機GPU | Intel UHD Graphics（WMI取得値） |
| 実機RAM | OS認識 約15.8GiB。調査時空き約6GiB。性能保証値ではない |

## 採用した最小構成

- Unityを既存ルートで `-createProject` 起動し、新規の標準設定を生成。既存資料は保持。
- URPを第一候補として検討したが、既存パイプラインもパッケージもないため、追加依存ゼロで開始できるBuilt-inをPhase 1に採用。URP導入・移行は実測後に判断する。HDRPは使わない。
- 入力は標準Input Managerだけを採用。`activeInputHandler: 0`。WASDとShiftはKeyCode、マウスは標準のMouse X/Yを使用。Input Systemとの混在なし。
- PackagesはUnity同梱の標準モジュールだけ。Unityの空プロジェクト作成で自動追加されたMultiplayer Centerは、新規manifestから外した。外部パッケージ、Cinemachine、NavMesh、TMP、購入アセットは追加していない。
- 専用メニューは `Assets/_GokiTakeover/Editor/`、ゲーム内コードは `Scripts/`。独自フレームワークや将来フェーズの空クラスは作らない。
- モデルは自作低ポリ球・6角柱を各1メッシュ共有し、部品のTransformを動かす。静的家具は標準Cubeを共有。単一CharacterController＋固定BoxCollider。動的Rigidbodyなし。
- 仮HUDは標準IMGUI。日本語OSフォントを一度だけ取得し、数値表示は0.25秒周期。配布フォントの追加なし。
- 新規プロジェクト設定: Windows既定画質Low、1280×720、ウィンドウ、入力は旧方式。試験シーンでは影・AA・HDRをOFF、Directional Light 1灯。フレームレート上限60は計測のための上限で、達成を意味しない。
- シーン設定を有効にするPhaseOneSessionは退場時に元の品質値へ戻す。
- Windowsビルドメニューは専用シーンだけを指定。全体のBuild Settingsのシーン一覧を変更しない。

## 追加ファイルと責務

| 場所 | 内容 |
|---|---|
| Scripts/Character | 外観データ、脚・触角アニメーション、移動 |
| Scripts/Camera | 障害物を考慮した三人称カメラ |
| Scripts/Core | 標準入力、専用シーンの低負荷設定 |
| Scripts/UI | 日本語のHP・FPS・操作HUD |
| Scripts/Performance | Developmentビルド限定・明示オプション時だけ動く自動試験 |
| Editor | 共有アセット、Prefab、キッチン生成、Windowsビルド、自動検証 |
| Art/Materials・Meshes | 生成した共有メッシュ・マテリアル |
| Data | 色・大きさを編集するCockroachAppearance |
| Prefabs | Cockroach_Player.prefab |
| Scenes | Phase1_Kitchen_Test.unity |
| ルート.gitignore | Library/Temp/Obj/Logs/UserSettings/Build等を除外 |

## 衝突・破壊リスクと対処

- 開始時に既存シーン・設定・アセットは存在せず、移行対象はない。元の資料はSHA-256で保全確認する。
- 名前空間を `GokiTakeover` / `GokiTakeover.Editor` に限定。追加アセットは専用フォルダに限定する。
- 再生成は `GokiTakeover.Generated` ラベルを持つ対象だけを更新する。ラベルなしの同一パスに既存アセットがある場合、ログを出して停止する。
- キャラ生成はPreview Scene内で行う。開いているシーンにオブジェクトを増やさない。
- キッチン通常メニューは保存済み専用シーンを開く。作り直すときだけ明示的なRebuildメニューを使う。手編集した専用シーンは、Rebuild前にGitコミットまたは別名保存する。
- 未保存シーンがある場合はUnity標準の保存確認を行う。生成メニューはPlay中は無効。
- 入力用に新規レイヤーやタグを定義しない。主人公は既存のIgnore Raycastレイヤー、カメラはMainCameraタグを使う。カメラの障害物判定だけが主人公を除外し、通常の物理衝突は維持する。
- 日本語フォントはWindowsのYu Gothic/Meiryoを実行時使用。フォント未取得時は警告する。他OSへの配布は今回の対象外。

## 各フェーズの受け入れ条件を確認

| Phase | 仕様書の完了条件 | 今回の扱い |
|---|---|---|
| 0 | エラーなく開く、安全な追加、Git差分確認 | 新規構成を作成・Unityバッチコンパイル確認 |
| 1 | メニュー生成・Prefab、Idle/Walk/Run、追従と衝突、Consoleエラーなし、可能なら720pビルド | 今回実装・検証対象。手動の操作感確認はユーザー実施待ち |
| 2 | 人間から逃れつつ3個納品、失敗・再試行 | 未着手 |
| 3 | 壁登り、両視点の状態維持、仲間回収命令、カメラ等の重複なし | 未着手 |
| 4 | 回収→増員→侵略の循環、人間対策の強化 | 未着手 |
| 5 | タイトル→出撃→最終防衛→クリア→解放 | 未着手 |
| 6 | 対象PCで720p/30fpsを継続測定、1ステージ完走 | 未着手。Phase 1の短時間計測で完成版の合格とはしない |

## APIの確認資料

- [CharacterController.Move](https://docs.unity3d.com/ja/current/ScriptReference/CharacterController.Move.html): 衝突制限付き移動。重力はMotor側で加算。
- [Physics.SphereCast](https://docs.unity3d.com/ja/current/ScriptReference/Physics.SphereCast.html): カメラの幅を考慮した障害物判定。
