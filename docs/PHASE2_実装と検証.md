# Phase 2 — 潜入・食料・人間1体

2026-10-22。Unity 6000.0.61f1 / Windows / Built-in / 標準Input Manager。
Phase 1の改善版キッチンを複製した専用シーンで、回収→納品→練習クリアと、発見→逃走→被弾→死亡→リトライを実装した。

## 起動・操作

Windows版: `Build/GokiTakeover_Phase2/GokiTakeover.exe`。

Unityでは **Tools > GOKI TAKEOVER > Generate Phase 2 Infiltration** で専用シーンを開き、Gameを1280×720にして **▶ Play**。

| 入力 | 動作 |
|---|---|
| WASD / Shift / マウス | 移動 / ダッシュ / 視点回転 |
| E | 近くのパンくずを回収。所持中は巣の近くで納品 |
| Esc | ポーズ / 再開。人間・攻撃予兆・無敵時間も停止 |
| R | 死亡・練習クリア後に最初からやり直す |
| 結果画面のボタン | マウスでもリトライ可能 |
| Alt+F4 | Windows版を終了 |

1. 黄緑の輪がある巣から出発する。初期HPは3、所持上限は1個。
2. パンくずに近づき「E パンくずを回収する」が出たらEを押す。運搬中は見た目とHUDが変わり、速度が88%になる。
3. 黄緑の巣マーカーを頼りに戻り、巣の近くでEを押して納品する。
4. 残りの2個も回収する。人間は右側の通路から巡回し、視野角・距離・遮蔽で発見する。ゲージが満ちると追跡する。
5. 見つかったら机・椅子・棚の下へ。低い家具の下は隠れ場所となり、人間は自分の背丈が入らない経路へ進入しない。見失うと最後に見た場所を捜索し、時間切れで巡回に戻る。
6. 「スリッパ注意！」と橙の輪が出たら、着弾まで0.9秒の間に円外へダッシュ。着弾位置は予兆開始時に固定し、着弾時にも家具の遮蔽を確認する。
7. 茶色の粘着シートでは速度が32%になる。シートを出れば回復する。拘束・救出・継続ダメージは今回追加していない。
8. HP0でゲームオーバー。Rまたはボタンで食料・所持・納品・HP・人間・カメラを初期状態へ戻す。巣の周辺は安全地帯。
9. パンくず3個の納品で「練習クリア」。正式な建物制圧・報酬・セーブは後続フェーズ。

ウィンドウのフォーカスを失うと自動ポーズする。戻ったらEscまたは「探索を再開」。Phase 1のEscは引き続き操作解除であり、Phase 2では時間を止めるポーズに拡張した。

## シーン・設定・再生成

- シーン: `Assets/_GokiTakeover/Scenes/Phase2_Kitchen_Infiltration.unity`
- 設定: `Assets/_GokiTakeover/Data/PhaseTwoSettings.asset`
- 人間用経路: `Assets/_GokiTakeover/Data/PhaseTwoNavigation.asset`

設定アセットでHP、運搬速度、インタラクト距離、発見距離・視野角・所要時間、捜索時間、巡回・追跡速度、攻撃範囲・予兆・クールダウン、粘着速度を調整できる。納品目標は1〜3個に限定。通常の生成では既存設定値を維持する。

**Rebuild Phase 2 Infiltration** はPhase 1改善版の保存済みシーンから専用シーンとNavMeshを作り直す。このPhase 2シーンの手編集は失われるため、必要なら先に別名保存する。元のPhase 1シーン・Prefab・Art・設定は生成時に変更しない。生成物以外の同名アセットは保護し、シーンとNavMeshのGUIDを維持する。

人間の経路は既存の物理ColliderからEditorで一度だけ生成する。追加Unityパッケージは使わず、標準AIモジュールのNavMeshを使用。人間の感知と目的地更新は10Hz、移動補間はNavMeshAgentに任せる。家具の配置を編集したらNavMeshの再生成も必要。

**Build Phase 2 Windows 720p** は通常Windows x64 / Mono版を出力する。PlayerSettingsの解像度・ウィンドウ設定はビルド後に復元し、全体のBuild Settingsのシーン一覧を変更しない。

## 追加・変更ファイル

パスは `Assets/_GokiTakeover/` 基準。各アセット・フォルダの.metaも保管対象。

| ファイル | 役割 |
|---|---|
| Scripts/Core/PhaseTwoSettings.cs | 調整用ScriptableObject |
| Scripts/Core/PhaseTwoSession.cs | HP・無敵・ポーズ・死亡・納品数・練習クリア・リトライ |
| Scripts/Interaction/FoodItem.cs | 食料の配置・運搬・納品状態と復元 |
| Scripts/Interaction/PlayerInteractor.cs | 距離・遮蔽を確認して回収／納品、所持上限 |
| Scripts/HumanAI/HumanAIController.cs | Routine / Chase / Search / Attack、感知、予兆と着弾 |
| Scripts/HumanAI/KitchenNavigation.cs | 保存済みNavMeshの登録・解除 |
| Scripts/Hazard/StickyTrap.cs | 粘着範囲の判定 |
| Scripts/UI/PhaseTwoHud.cs | 日本語目標・所持・HP・発見ゲージ・巣方向・警告・結果・ポーズ |
| Scripts/Performance/PhaseTwoScenarioTest.cs | Editor／明示起動したDevelopment版だけのシナリオ検証と性能測定 |
| Editor/PhaseTwoGenerator.cs | 専用シーン・人間・巣・食料・危険・NavMeshの生成と専用ビルド |
| Editor/PhaseTwoVerification.cs | 生成とPlay Modeの自動検証 |
| Scenes/Phase2_Kitchen_Infiltration.unity | 遊べる専用シーン |
| Data/PhaseTwoSettings.asset / PhaseTwoNavigation.asset | 暫定パラメータ／経路データ |
| Art/Materials/Phase2_*.mat / Art/Meshes/Phase2_Ring.asset | 自作の共有材質と予兆・巣の輪 |

既存ファイルの変更は4本。

| 変更ファイル | 内容 |
|---|---|
| Scripts/Character/RoachMotor.cs | 移動許可と速度倍率を追加。既定値は旧動作を維持 |
| Scripts/Character/CockroachVisualController.cs | 回収時のお辞儀・被弾ののけぞり・死亡姿勢 |
| Scripts/Camera/ThirdPersonRoachCamera.cs | カーソル管理をPhase 2へ委譲可能に。リトライの構図復元 |
| Editor/PhaseOneBuild.cs | 共通ビルド処理を同じEditorアセンブリから再利用可能に変更 |

ほかに本書、`docs/Validation/Phase2/`の検証証拠を追加。元の仕様書・参考画像は保持。有料素材、外部サービス、音声・画像素材は追加していない。

## 自動検証の再実行

Unityを閉じ、PowerShellでこのプロジェクトを開いて実行する。複数Editorを同じプロジェクトに同時起動しない。GUIアプリの起動完了を待つためStart-Process -Waitを使用する。

```powershell
$unityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.0.61f1\Editor\Unity.exe'
$projectRoot = (Get-Location).Path
New-Item -ItemType Directory -Force 'Logs/Phase2' | Out-Null

# 再生成を含むので、手編集したPhase 2シーンは先に別名保存する。
Start-Process $unityEditor -Wait -WindowStyle Hidden -ArgumentList @(
  '-batchmode', '-nographics', '-projectPath', ('"' + $projectRoot + '"'),
  '-executeMethod', 'GokiTakeover.Editor.PhaseTwoVerification.GenerateAndVerify',
  '-logFile', ('"' + $projectRoot + '\Logs\Phase2\editmode.log"'))

Start-Process $unityEditor -Wait -WindowStyle Hidden -ArgumentList @(
  '-batchmode', '-nographics', '-projectPath', ('"' + $projectRoot + '"'),
  '-executeMethod', 'GokiTakeover.Editor.PhaseTwoVerification.VerifyPlayMode',
  '-logFile', ('"' + $projectRoot + '\Logs\Phase2\playmode.log"'))

Start-Process $unityEditor -Wait -WindowStyle Hidden -ArgumentList @(
  '-batchmode', '-quit', '-projectPath', ('"' + $projectRoot + '"'),
  '-executeMethod', 'GokiTakeover.Editor.PhaseTwoGenerator.BuildBoth',
  '-logFile', ('"' + $projectRoot + '\Logs\Phase2\build.log"'))
```

Development検証版のみ、`-goki-phase2-test -goki-output <出力先>` で同じゲーム機能を検証し、結果画面とゲーム画面のPNG、30秒の計測JSONを出力して自動終了する。Player計測は通常の画面描画を使い、`-batchmode / -nographics`は付けない。検証版は `Build/GokiTakeover_Phase2_Validation/GokiTakeover.exe`。通常版には試験ランナーを含めない。

## 実施した検証結果

| 検証 | 結果 |
|---|---|
| Editor / PlayerのC#コンパイル | 成功。新規C#コンパイルエラーなし |
| 専用シーンの生成・再生成 | [12項目PASS](Validation/Phase2/editmode.txt)。元データ保全、GUID・アセット数維持、配置数、巡回経路の接続、机下への人間の侵入制限 |
| Unity Play Mode | [33項目PASS](Validation/Phase2/editor-playmode.txt)。回収・二重加算防止・容量・納品・クリア・復元、粘着・視認・遮蔽・追跡・捜索・実時間移動、予兆・回避・被弾・無敵・ポーズ・死亡・リトライ |
| 元のPhase 1回帰 | [15項目PASS](Validation/Phase2/baseline-regression.txt)。共通の移動・アニメーション・カメラの既定動作を維持 |
| 改善版Phase 1回帰 | [12項目PASS](Validation/Phase2/polished-regression.txt)。改善モデル・家具・瓶・カップの衝突と描画設定 |
| Windows x64 / Monoビルド | 通常版とDevelopment検証版とも成功。別フォルダへ出力 |
| 最終通常版の起動 | ウィンドウ生成・入力初期化・起動から正常終了まで確認。新規実行例外・初期NavMesh警告なし。通常版単体のFPS測定・画面撮影は未実施 |
| 最終Development Player | [同じ33項目PASS](Validation/Phase2/phase2-gameplay.txt)。起動から検証・描画・計測・自動終了まで完了。新規実行例外なし、初期NavMesh警告も解消 |
| 画面の目視 | [開始地点](Validation/Phase2/arrival-720p.png)、[攻撃予兆](Validation/Phase2/attack-720p.png)、[練習クリア](Validation/Phase2/clear-720p.png)、[死亡](Validation/Phase2/death-720p.png)、[移動中](Validation/Phase2/gameplay-720p.png)を実際のPlayer描画で確認。日本語の欠落なし |
| 既存資料・アセット保全 | 作業前のSHA-256との比較で、既存の変更は表に示したC#4本のみ。元シーン・Prefab・Art・ProjectSettings・Packages・元ドキュメント・参考画像を維持 |

根拠ログは `Logs/Phase2/editmode-final.log`、`playmode-startup-final.log`、`baseline-regression.log`、`polished-regression.log`、`build-delivery.log`、`player-final.log`。ログとBuild/LibraryはGit除外、検証用PNG・JSON・txtは本書の隣に保管した。開始時点からリポジトリは未追跡ファイルの状態であり、今回もコミットしていない。

### 最終ビルドの性能計測

[最終計測JSON](Validation/Phase2/phase2-performance.json)。Intel Core i7-10510U / Intel UHD Graphics / Unity 6000.0.61f1。Editorを終了し、Development Playerを通常描画して計測した。起動後3秒とPNG撮影をサンプルから除外。

| 指標 | 最終結果 |
|---|---:|
| 解像度 | 1280×720 |
| 測定時間・フレーム数 | 30.0014秒 / 1798フレーム |
| 平均FPS | **59.93fps** |
| フレーム時間95%点 | **16.68ms** |
| 最大フレーム時間 | **30.32ms** |
| GC Allocated In Frame最大観測値 | 20184 bytes |
| Unity Total Reserved Memory観測値 | 289626079 bytes（約276.2MiB） |

**今回の経路では720p/30fpsの目標を満たした。** 人間1体の巡回を有効にしたキッチンで、主人公が巣周辺を往復し、カメラを旋回する30秒の試験。全食料の回収経路や追跡中の長時間性能を示すものではない。フレーム時間はTime.unscaledDeltaTime、メモリはProfilerRecorderによる値で、OSのWorking Set・GPU時間の計測ではない。通常版そのもののFPSや、前フェーズで観測した単発スパイク全般の解消を保証しない。

画面の「練習クリア・0秒」は位置を直接変更する機能試験の結果であり、実操作で0秒クリアした記録ではない。PNGのDevelopment表示は検証版だけに表示される。

## 未確認事項・制限

- キーボード・マウスの実操作、マウス感度、全ルートを人間から逃げながら完走する難易度、長時間プレイは手動確認が必要。
- 自動機能試験は再現性のため一部で主人公の位置を直接変更し、同じインタラクション・感知・攻撃・ダメージ処理を呼ぶ。OSのキー入力を再現した試験ではない。
- 家具下の安全性を読み取りやすくするため、通常の視線に加えて主人公の上方3.3m以内の家具を遮蔽として扱う。厳密な人間の視覚シミュレーションではない。
- 人間は簡易な自作モデル。捜索は最後に見た場所へ向かって一定時間待つ最小実装。音への反応・複数人・恐怖・対策設置は後続フェーズ。
- UIは標準IMGUI・WindowsのOS日本語フォントを継続利用。ゼロGCではなく、フォントファイルは同梱していない。
- 今回のクリアは練習ミッションのみ。壁登り・軍団視点・仲間はPhase 3、繁殖・強化はPhase 4以降。

次の実装は、今回の操作感と難易度の確認後に **Phase 3（壁登り・Tab軍団視点・仲間4〜9匹）**。

API確認資料: [Unity 6 NavMeshBuilder](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/ai/navmeshbuilder)。保存済み経路を使い、毎フレームのNavMesh生成は行わない。
