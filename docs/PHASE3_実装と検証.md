# Phase 3 — 壁登り・軍団視点・仲間

2026-10-23。Unity 6000.0.61f1 / Windows / Built-in / 標準Input Manager（Phase 2と同じ）。
Blenderモデル版キッチン（`Phase2_Kitchen_BlenderModels.unity`）を複製した専用シーンに、壁・天井移動、Tabの軍団モード、仲間4〜9匹への指示を追加した。

## 起動・操作

- Windows版: `Build/GokiTakeover_Phase3/GokiTakeover.exe`（Dataフォルダ・DLLと一緒に保持）。
- Unity: **Tools > GOKI TAKEOVER > Generate Phase 3 Swarm** で `Assets/_GokiTakeover/Scenes/Phase3_Kitchen_Swarm.unity` を開き、Gameを1280×720にして **▶ Play**。

### 通常モード（三人称）

| 入力 | 動作 |
|---|---|
| WASD / Shift / マウス | 移動 / ダッシュ / 視点回転 |
| 壁へ進み続ける（約0.2秒） | 壁・食器棚に張りつく。そのままWで登り、天井まで行ける |
| Space | 壁・天井から離れて落ちる |
| E | 食料の回収・巣への納品 |
| 1 / 2 / 3 / 4 / F | 全仲間に 回収 / 探索 / 防衛 / 一斉侵攻 / 追従 |
| Tab | 軍団モードへ |
| Esc / R | ポーズ / 死亡・クリア後のやり直し |

### 軍団モード（見下ろし）

| 入力 | 動作 |
|---|---|
| 左クリック / ドラッグ / Shift+クリック | 仲間を選択 / 範囲選択 / 追加・解除 |
| Q | 全員選択 |
| 右クリック | 床: 選択中の仲間をそこへ移動（「目標」に記憶）。見つけた食料: その食料を回収 |
| 1〜4・画面下のボタン | 回収 / 探索 / 防衛 / 一斉侵攻（目標地点、未指定なら主人公の位置へ走って集結） |
| F | 選択中の仲間を主人公に追従 |
| WASD / Shift / ホイール / Space | 視点移動 / 高速 / ズーム / 選択中の仲間（なければ主人公）へ戻す |
| Tab | 通常モードへ |

軍団モード中も時間は止まらない（仕様4.3）。人間は巡回を続け、主人公はその場で停止する。

### ミッション

食料を **6個** 巣へ納品するとクリア（食料は全8個）。

- 床の3個は最初から見えている。
- 食器棚の天板に1個。仲間は登れないので主人公が登って取る。
- 4個は隠れている。仲間に **2 探索** させるか、主人公が近づくと見つかる（黄緑の目印が出る）。
- 人間は壁・天井・天板の上にいる主人公をスリッパで叩けない。床では従来どおり予兆→着弾でダメージ。

## 実装内容

### 壁・天井移動（仕様4.4）

- 主人公の当たり判定は既存の **CharacterController 1個のまま**（追加のRigidbody・コライダーなし）。半径=高さ/2の球なので、どの向きでも形が変わらない。
- 壁上ではCharacterControllerを止め、同じ球を `SphereCast` で「面に沿って滑らせる → 面に吸着」させる。角の手前で前方の面に当たれば内角（床→壁→天井、壁→床）へ乗り移り、外角（天板の縁）は吸着時の接触法線が縁を回り込むので落ちずに越えられる。
- 床に戻るのは法線が真上の面に乗った時だけ（縁の途中で通常移動に戻すと縁の上で引っかかるため）。
- 体の傾きは新設の `SurfaceAlign` ピボットだけで表現し、0.1秒程度で補間。カメラも面の法線を補間した座標系で回るので、角で視点が急に跳ばない。
- 登れる面は `ClimbableSurface` を付けた8枚だけ: 四方の壁、新設の天井、食器棚（本体・天板・台輪）。冷蔵庫・机・椅子・棚は登れない（細い脚や狭い隙間で詰まらないよう対象外にした）。
- 埋まり・落下の保険（直前の安全位置へ戻す）は壁上でも有効。
- `surfaceAlign` が未設定の旧シーンでは、従来の床専用キャラクターとまったく同じ動作。

### Tab切替（仕様4.3・2.3）

- カメラは既存の1台のまま。`ThirdPersonRoachCamera` と新しい `SwarmViewCamera` を同じカメラに付け、**どちらか一方だけを有効**にする。2台目のカメラや二重描画はない。
- 軍団モードでは天井を非表示、手前の壁は「影だけ落とす」設定に切替（照明は通常モードと同じ）。戻すと復元。
- マウスカーソルは軍団モード中だけ解放。ポーズ・再開との組み合わせでも正しく戻る。

### 仲間AI（仕様8）

- 状態: `Idle / FollowLeader / GoToTarget / Explore / CollectFood / ReturnToNest / DefendNest / FleeHazard / Disabled`。
- 9匹をシーンに生成済み（プール）。有効数は `Data/PhaseThreeSettings.asset` の **allyCount（4〜9、初期6）**。
- 経路は人間用NavMeshを使わず、Editorで一度だけ焼いた床グリッド（0.25m×56×48）上のA*。机の下は通れ、粘着シートは通行コスト8倍で避ける。経路の再計算は0.35秒間隔・個体ごとにずらす。仲間は物理コライダーを持たず、グリッドで家具をよける。
- 群れの動き: 主人公の後ろ・巣の周り・目標地点に2重リングの隊形、近すぎる仲間同士・主人公から離れる分離力。
- 回収: 見つけた床の食料を1匹1個ずつ予約して運び、巣で納品（1個につき1回だけ加算）。届かない食料は諦めて次へ。なくなれば巣へ戻って待機し、理由を表示（無限ループしない）。
- 探索: 調査地点8か所を分担して回り、周囲の隠れた食料を発見。
- 生存優先: スリッパの予兆範囲にいる仲間は命令を中断して逃げる（HUDに「回避」）。逃げ遅れて潰されると行動不能になり、運搬中の食料は元の場所へ戻る。8秒後に巣で復活。
- 色の違いは付けず、大きさを0.8〜0.95倍で変えた（材質1つのまま）。

### HUD（`PhaseThreeHud`、既存と同じIMGUI・パネル様式）

- 通常: 左上ミッション・納品数、左下に生命力・人間の状態と発見ゲージ・仲間数と内訳、壁面/天井移動中の表示、壁に張りつくまでのゲージ、通知。
- 軍団: 左上「軍団モード」、右上に 仲間 / 危険度 / 食料地点 / 調査数、地図上に あなた・巣・食料・粘着・人間・未調査「？」・目標、仲間ごとに状態の一文字（回収・運搬・探索・回避など）と選択枠、下部に4指示ボタン（キー番号・説明付き、カーソル時・選択中の指示・選択なしの無効状態を表示）。
- 色だけに頼らず、記号（◆◇▲■◎！？）と文字を併記。参考画像 `04_SwarmTactics.png` の構成（上部の数値、中央のアイコン、下部の4指示）に合わせた。

## 追加・変更ファイル

パスは `Assets/_GokiTakeover/` 基準。.metaも保管対象。

| 新規ファイル | 役割 |
|---|---|
| Scripts/Character/ClimbableSurface.cs | 登れる面の目印 |
| Scripts/Camera/SwarmViewCamera.cs | 見下ろし視点（同じカメラの別コントローラー） |
| Scripts/Swarm/AllyNavGrid.cs | 仲間用の床グリッドとA*（割り当て再利用） |
| Scripts/Swarm/AllyRoach.cs | 仲間1匹の状態機械・移動 |
| Scripts/Swarm/SwarmCommander.cs | Tab切替、選択、指示、発見・探索、つぶれ判定、リセット |
| Scripts/UI/PhaseThreeHud.cs | 通常／軍団の日本語HUD |
| Scripts/Core/PhaseThreeSettings.cs | 仲間数・速度・納品目標などの調整値 |
| Scripts/Performance/PhaseThreeScenarioTest.cs | Editor／明示起動したDevelopment版だけの検証・計測 |
| Editor/PhaseThreeGenerator.cs | 専用シーン・天井・食料・仲間・グリッドの生成と専用ビルド |
| Editor/PhaseThreeVerification.cs | 生成・Play Mode・Phase 2回帰の自動検証 |
| Scenes/Phase3_Kitchen_Swarm.unity | 遊べる専用シーン |
| Data/PhaseThreeSettings.asset / PhaseThreeAllyGrid.asset | 調整値 / 焼いた床グリッド |

| 変更ファイル（4本） | 内容（既定値では旧動作を維持） |
|---|---|
| Scripts/Character/RoachMotor.cs | 壁・天井移動、Spaceで離脱、面の法線・足元の公開 |
| Scripts/Camera/ThirdPersonRoachCamera.cs | 面の法線に合わせた回転座標系（床では従来と同一） |
| Scripts/Core/PhaseTwoSession.cs | 納品目標の上書き、軍団モード用のカーソル解放、リセット通知、叩ける位置か（床のみ）の判定 |
| Scripts/HumanAI/HumanAIController.cs | 床にいない主人公には攻撃しない。机下の遮蔽判定を床の高さに限定（天井を遮蔽と誤認しない） |

元のPhase 1/2シーン、Blenderモデル版シーン、Prefab、Art、Phase 2設定、ProjectSettings、Packagesは変更していない（作業前のSHA-256と比較し、変更はこの4本のみ）。

## 自動検証

Unityを閉じ、プロジェクト直下のPowerShellで実行（`Generate…`は再生成を含むため、手編集したPhase 3シーンは先に別名保存）。

```powershell
$unityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.0.61f1\Editor\Unity.exe'
$projectRoot = (Get-Location).Path
New-Item -ItemType Directory -Force 'Logs/Phase3' | Out-Null
foreach ($method in 'GenerateAndVerify','VerifyPlayMode','VerifyPhaseTwoRegression','VerifyBlenderRegression') {
  Start-Process $unityEditor -Wait -WindowStyle Hidden -ArgumentList @(
    '-batchmode', '-nographics', '-projectPath', ('"' + $projectRoot + '"'),
    '-executeMethod', ('GokiTakeover.Editor.PhaseThreeVerification.' + $method),
    '-logFile', ('"' + $projectRoot + '\Logs\Phase3\' + $method + '.log"'))
}
Start-Process $unityEditor -Wait -WindowStyle Hidden -ArgumentList @(
  '-batchmode', '-quit', '-projectPath', ('"' + $projectRoot + '"'),
  '-executeMethod', 'GokiTakeover.Editor.PhaseThreeGenerator.BuildBoth',
  '-logFile', ('"' + $projectRoot + '\Logs\Phase3\build.log"'))
```

Development検証版: `Build/GokiTakeover_Phase3_Validation/GokiTakeover.exe -goki-phase3-test -goki-output <出力先>`。同じ機能試験のあと30秒計測し、PNG・JSONを書き出して終了する。通常版には試験コードを含めない。

## 検証結果

| 検証 | 結果 |
|---|---|
| C#コンパイル | 成功。エラー・警告なし |
| 生成・再生成 | [33項目PASS](Validation/Phase3/editmode.txt)。既存シーン・Prefab・設定の保全、GUID・アセット数維持、カメラ1台、仲間9匹（コライダーなし）、食料8個、登れる面8枚、グリッド（机下○・机の脚×・棚の中×・粘着は高コスト）、全食料・調査地点への経路、経路探索1回0.27ms |
| Unity Play Mode | [58項目PASS](Validation/Phase3/phase3-gameplay.txt)。床→壁→天井、天井歩行、Space離脱、壁を下って着地、冷蔵庫は登らない、食器棚→天板→高所食料→降りて納品、壁上は叩かれない、壁上でリトライ、Tab 11回連続で状態維持・複製なし、天井・壁の表示切替、選択・右クリック移動・食料指定、仲間の回収・納品の一回加算、高所食料を狙わない、食料切れで帰巣、探索で隠し食料4個発見、目標6個でクリア、到達不能目標の諦め、回避優先、つぶれ→8秒後復活、リトライ初期化 |
| Phase 2回帰 | 共通スクリプト変更後もBlenderモデル版で[39項目](Validation/Phase3/regression-blender/phase2-gameplay.txt)、旧Phase 2版で[38項目](Validation/Phase3/regression-phase2/phase2-gameplay.txt)PASS（旧版は叩きつけモデルがないため1項目少ない） |
| Windows x64 / Mono | 通常版・Development検証版とも成功 |
| Development Player | [同じ58項目PASS](Validation/Phase3/player/phase3-gameplay.txt)、実行時例外なし |
| 通常版の起動 | 8秒起動、D3D11初期化、プロセス継続、例外ログなし（ワーキングセット約135MiB） |
| 画面の目視 | [壁面](Validation/Phase3/player/wall-720p.png)、[天井](Validation/Phase3/player/ceiling-720p.png)、[軍団モード](Validation/Phase3/player/swarm-720p.png)、[仲間と移動](Validation/Phase3/player/allies-720p.png)、[クリア](Validation/Phase3/player/clear-720p.png)。日本語の欠落・はみ出しなし |

### 性能（[phase3-performance.json](Validation/Phase3/player/phase3-performance.json)）

Intel Core i7-10510U / Intel UHD Graphics、Development Player、1280×720、仲間6匹、人間1体巡回。0-10秒 三人称で壁→天井、10-20秒 軍団モードで全員に回収指示、20-30秒 三人称で床を移動（仲間は回収継続）。撮影直後のフレームは除外。

| 指標 | 結果 |
|---|---:|
| 平均FPS（全体 / 三人称 / 軍団） | **59.5 / 59.7 / 59.7fps** |
| フレーム時間95%点 | **16.87ms** |
| 最大フレーム時間 | 50.99ms |
| GC Allocated In Frame最大 | 62,378 bytes |
| Unity Total Reserved Memory | 約281MiB |

30fps目標は満たした。単発の51msがあり「常時60fps」ではない。GCはIMGUI HUDの文字列生成が主（Phase 2と同じ方式）。仲間9匹・長時間・通常版のFPSは未計測。

画面の「0秒」「クリックして操作を再開」は自動試験（位置の直接変更・カーソル非ロック）によるもので、実操作の記録ではない。

## 未確認事項・制限

- キーボード・マウスでの実操作（壁登りの操作感、天井での逆さま視点の酔いやすさ、軍団モードのクリック精度）は手動確認が必要。自動試験はOSの入力を再現していない。
- 壁際を走ると0.2秒押し続けた時に登り始める。意図せず登る場合は `RoachMotor` の climbEngageSeconds を長くする。
- 天井の逆さま視点は「面に合わせてカメラが回る」方式。酔う場合は、天井だけカメラを正立させる方式への変更を検討。
- 仲間は床専用（仕様どおり壁登りなし）。高所の食料は主人公が運ぶ。
- 人間は仲間を発見・追跡しない。仲間がつぶれるのは主人公を狙ったスリッパの下にいた時だけ。
- 仲間の見た目は主人公と同じモデルの大きさ違い（色違いはなし）。
- UIは仮置き段階（仕様10.1の「仮置き→操作検証→装飾」）。ブラシ風の装飾・アイコン画像はPhase 6。
- 繁殖・強化・パニック・殺虫スプレー・毒餌はPhase 4。

次の実装は、今回の操作感の確認後に **Phase 4（巣での繁殖・強化、発見度／恐怖度、殺虫スプレー・毒餌、設定／図鑑／ポーズ画面）**。
