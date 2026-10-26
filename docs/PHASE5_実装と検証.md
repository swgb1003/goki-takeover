# Phase 5 — 占拠・正式ステージクリア

2026-10-23。Unity 6000.0.61f1 / Windows / Built-in / 標準Input Manager。
Phase 4のキッチンを複製した2つの新シーン（タイトル、ステージ1）で、**タイトル → ステージ選択 → ミッション説明 → 侵略（3つのゲージ） → 最終防衛 → 人間の退去・クリア → 評価・次ステージ解放** を一通り遊べるようにした。セーブ（巣での記録・つづきから）と、やられた時の後継交代も追加した。

## 起動・操作

- Windows版: `Build/GokiTakeover_Phase5/GokiTakeover.exe`（Dataフォルダ・DLLと一緒に保持）。タイトル画面から始まる。
- Unity: **Tools > GOKI TAKEOVER > Generate Phase 5 Takeover** で `Assets/_GokiTakeover/Scenes/Phase5_Title.unity` が開く。Gameを1280×720にして **▶ Play**。ステージだけ試す時は `Phase5_Stage01_Kitchen.unity` を直接Playしてもよい（ミッション説明から始まる）。

| 画面 | 操作 |
|---|---|
| タイトル | はじめる（2026-10-26 までは「ゲーム開始」）・つづきから・設定・図鑑・終了。マウス、または ↑↓ と Enter |
| ステージ選択 | ←→／クリックでステージ、Enter／「出撃」。「つづきから（巣の記録）」、図鑑・設定、Esc でタイトルへ |
| ミッション説明 | Enter／「出撃」で開始。「タイトルへ」 |
| プレイ中 | Phase 4までの操作そのまま。Esc ポーズに「ステージを最初から」「タイトルへ戻る」を追加 |
| やられた | 「仲間に後を継がせる」「巣の記録から再開」「ステージを最初から（R）」「タイトルへ」 |
| ステージクリア | 「ステージ選択へ」「リプレイ（R）」「タイトルへ」 |

## 遊び方（ステージ1「ワンルームのキッチン」）

### 3つのゲージ（仕様9.3）

ミッション欄（左上）に数字つきで表示。3つとも100%になると最終防衛が始まる。

| ゲージ | 増え方 | 目標（暫定） |
|---|---|---|
| ◆ 食料確保 | 巣へ納品した食料の価値の合計（パンくず+1〜食べ残し+3） | 30 |
| ■ 縄張り | 4区域（テーブルの下・冷蔵庫の前・流し台の前・食品棚のそば）に **2匹以上（主人公も1匹）で12秒** とどまると占領。人間が区域にいる間は進まず、人間だけがいると少しずつ戻る。占領すると旗が立ち、戻らない | 4区域 |
| ▲ 人間の恐怖 | Phase 4の恐怖度に加えた量の累計（見つかる・仲間を見られる・繁殖・納品・占領+10）。恐怖度は下がってもゲージは下がらない | 120 |

床の薄い黄緑の輪が区域。近くでは「□ テーブルの下 40% 1/2匹」のように表示され、軍団モードでは全区域が見える。仲間は右クリックの「移動」や「4 一斉侵攻」で区域へ送る。全区域・食料の取り尽くしは不要（取り逃しで詰まない）。

### 最終防衛（反応段階4「駆除業者」）

- 60秒耐える。人間はパニック状態のまま（スプレーも使う）。
- **燻煙剤**: 3秒後から13秒ごとに、主人公の足元と「仲間がいちばん多い区域」へ交互に置かれる。橙の輪で3.5秒予告 → 半径3mに9秒間、低い煙。煙の中では生命力が減り、仲間はやられる（巣で復活）。
- 煙は低い（2.4m）ので **天板・高い壁の上・巣** は安全。巣の周りには置かれない。仲間は命令より先に輪の外の床へ逃げる。
- 耐えきると人間は「もう無理！ この部屋は出ていく！」と勝手口（右の壁）へ逃げ、消えた後にクリア（反応段階5「撤退」）。

### やられた時（仕様7.1）

- 仲間が1匹以上いれば **後継に交代**（仲間-1、巣から生命力満タンで再開、ゲージ・最終防衛の残り時間はそのまま）。
- 仲間がいない時は「巣の記録から再開」または最初から。

### 評価（仕様9.4）

時間（12分以内+2／18分以内+1）・発見回数（3回以下+2／8回以下+1）・生き残った仲間（8匹+2／5匹+1）・回収した食料（39以上+2／30以上+1）の合計から、後継に交代した回数を引く。**6点以上A、3点以上B、それ未満C**。式と境目は `Data/Stages/Stage01_Kitchen.asset` で調整できる。

初回クリアで **ステージ2「一軒家」が解放**（制作中のため出撃はできない）され、見た目 **「金色の甲殻」**（ステージ選択で切替）が手に入る。

### セーブ（仕様12）

- `persistentDataPath/save.json`。解放したステージ、ステージごとの記録（クリア回数・ベスト評価・ベストタイム・最多食料）、見た目、チェックポイント1つ。
- チェックポイントは **出撃時（ステージ開始）と巣を閉じた時** に保存（食料・回収合計・巣Lv・強化・仲間の数・区域の進み具合・恐怖・発見回数・時間・後継回数）。最終防衛中は保存しない。クリアすると消える。
- 再開時は全員が巣から始まる（個体の位置・置かれた罠・補充中の食料は保存しない。仕様12の「全個体位置の復元は後回し」）。
- 壊れたファイルは `.bak` に退避し、新しいセーブで起動（例外なし）。設定ファイルはPhase 4と共通。

## 追加・変更ファイル

パスは `Assets/_GokiTakeover/` 基準。.metaも保管対象。

| 新規ファイル | 役割 |
|---|---|
| Scripts/Stage/StageDefinition.cs | ステージ定義（3ゲージの目標、縄張り、最終防衛、評価式、解放・報酬） |
| Scripts/Stage/StageProgress.cs | ミッション説明→侵略→最終防衛→撤退→クリアの進行、縄張り判定、燻煙剤、後継、評価、チェックポイント |
| Scripts/Save/SaveSystem.cs | 進行セーブ（JSON、一時ファイル経由で書込、壊れたら .bak） |
| Scripts/Core/GameFlow.cs | タイトル⇔ステージのシーン遷移（Editorはパス指定で読込、ビルド設定を変更しない） |
| Scripts/Hazard/SmokeFogger.cs | 燻煙剤（`HazardBase`、予告→煙、仲間の退避先） |
| Scripts/UI/PhaseFiveHud.cs | Phase 5 HUD（Phase 4 HUDを複製して拡張: ミッション説明・3ゲージ・区域・最終防衛・やられた・クリア） |
| Scripts/UI/TitleMenu.cs / TitleBackdrop.cs / HudKit.cs / CodexData.cs | タイトル・ステージ選択・設定・図鑑、背景のゴキブリ、共通の部品、図鑑データ |
| Scripts/Performance/PhaseFiveScenarioTest.cs | Editor／明示起動したDevelopment版だけの通し検証・計測 |
| Editor/PhaseFiveGenerator.cs / PhaseFiveVerification.cs | 2シーンの生成・ビルド、自動検証と回帰テスト |
| Scenes/Phase5_Title.unity、Scenes/Phase5_Stage01_Kitchen.unity | タイトル、ステージ1 |
| Data/Stages/Stage01_Kitchen.asset・Stage02_House.asset・Stage03_Diner.asset | ステージ定義（2・3はカード表示用。制作中） |
| Art/Meshes/Phase5_Disc.asset、Art/Materials/Phase5_*.mat | 燻煙剤の予告円、煙（半透明）・缶・旗・区域の輪・勝手口 |

| 変更ファイル | 内容（既定値では旧動作を維持） |
|---|---|
| Scripts/HumanAI/HumanReaction.cs | 恐怖の累計 `FearGained`、チェックポイントからの復元 |
| Scripts/Colony/ColonyManager.cs | 仲間の数でクリアするかの切替（既定: する）、巣を閉じた通知、チェックポイントからの復元、拡張上限の文言 |
| Scripts/Core/PhaseTwoSession.cs | 後継の復帰 `Revive`、経過時間の復元 |
| Scripts/Swarm/SwarmCommander.cs | 仲間1匹を外す（後継の費用）、追加の危険から逃げる口（既定: なし） |
| Scripts/UI/HudGlyphs.cs | Phase 5画面の文字一覧を追加（Phase 4の一覧は変更なし） |
| Editor/PhaseOneBuild.cs | 複数シーンのビルド（既存の1シーンビルドはそのまま） |

Phase 1〜4のシーン・Prefab・設定・仲間グリッドは変更していない（生成前後のSHA-256比較を自動検証に含む）。ProjectSettings・Packages・ビルド設定（EditorBuildSettings）も変更なし。

## 自動検証

Unityを閉じ、プロジェクト直下のPowerShellで実行（`GenerateAndVerify` は再生成を含む）。

```powershell
$unityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.0.61f1\Editor\Unity.exe'
$projectRoot = (Get-Location).Path
New-Item -ItemType Directory -Force 'Logs/Phase5' | Out-Null
foreach ($method in 'GenerateAndVerify','VerifyPlayMode','VerifyPhaseFourRegression','VerifyPhaseThreeRegression','VerifyBlenderRegression','VerifyPhaseTwoRegression') {
  Start-Process $unityEditor -Wait -WindowStyle Hidden -ArgumentList @(
    '-batchmode', '-nographics', '-projectPath', ('"' + $projectRoot + '"'),
    '-executeMethod', ('GokiTakeover.Editor.PhaseFiveVerification.' + $method),
    '-logFile', ('"' + $projectRoot + '\Logs\Phase5\' + $method + '.log"'))
}
Start-Process $unityEditor -Wait -WindowStyle Hidden -ArgumentList @(
  '-batchmode', '-quit', '-projectPath', ('"' + $projectRoot + '"'),
  '-executeMethod', 'GokiTakeover.Editor.PhaseFiveGenerator.BuildBoth',
  '-logFile', ('"' + $projectRoot + '\Logs\Phase5\build.log"'))
```

自動検証は設定・セーブを出力フォルダ内の試験用ファイルに切り替えるため、プレイヤー本来のファイルは書き換えない。Development検証版: `Build/GokiTakeover_Phase5_Validation/GokiTakeover.exe -goki-phase5-test -goki-output <出力先>`。

## 検証結果

| 検証 | 結果 |
|---|---|
| C#コンパイル | 成功。エラー・警告なし |
| 生成・再生成 | [22項目PASS](Validation/Phase5/editmode.txt)。旧シーン・設定の保全、GUID・アセット数維持、カメラ1台、仲間9匹、燻煙剤3・旗4（待機）、巣から全区域へ仲間の経路、勝手口前のNavMesh、タイトルにゲーム処理が残っていない |
| Unity Play Mode | [45項目PASS](Validation/Phase5/phase5-gameplay.txt)。タイトル→ステージ選択（未解放は出撃不可）→出撃→ミッション説明→開始時チェックポイント、食料・縄張り（2匹で進む／人間がいると止まる／旗）・恐怖の各ゲージ、巣を閉じて記録、3ゲージで最終防衛、燻煙剤（足元へ予告→煙、予告中は無傷、煙で生命力と仲間、輪の外と天板は安全、巣は覆わない、仲間は輪の外へ逃げる）、後継交代、耐えきって人間が退去→クリア、評価の記録、次ステージと見た目の解放、リプレイ、タイトル→つづきからで復元、仲間なしでは記録から再開、クリア後のステージ選択、壊れたセーブの退避 |
| 回帰（共通スクリプト変更） | Phase 4で[58項目](Validation/Phase5/regression-phase4/phase4-gameplay.txt)、Phase 3で[58項目](Validation/Phase5/regression-phase3/phase3-gameplay.txt)、Phase 2（Blenderモデル版）で[39項目](Validation/Phase5/regression-blender/phase2-gameplay.txt)、旧Phase 2版で[38項目](Validation/Phase5/regression-phase2/phase2-gameplay.txt)PASS |
| Windows x64 / Mono | 通常版・Development検証版とも成功（タイトル＋ステージの2シーン） |
| Development Player | [同じ45項目PASS](Validation/Phase5/player/phase5-gameplay.txt)、実行時例外なし |
| 通常版の起動 | 10秒起動、プロセス継続、例外ログなし（ワーキングセット約204MiB） |
| 画面の目視 | [タイトル](Validation/Phase5/player/title-720p.png)、[ステージ選択](Validation/Phase5/player/stageselect-720p.png)・[クリア後](Validation/Phase5/player/stageselect-unlocked-720p.png)、[ミッション説明](Validation/Phase5/player/briefing-720p.png)、[三人称](Validation/Phase5/player/tps-720p.png)、[燻煙剤の予告](Validation/Phase5/player/fogger-720p.png)・[煙](Validation/Phase5/player/smoke-720p.png)、[軍団モード](Validation/Phase5/player/swarm-720p.png)、[巣](Validation/Phase5/player/nest-720p.png)、[やられた](Validation/Phase5/player/defeat-720p.png)、[クリア](Validation/Phase5/player/clear-720p.png)。日本語の欠落・はみ出しなし |

### 目視で見つけて直した問題

- 燻煙剤の缶が主人公より大きかった → 人間との縮尺に合わせて約半分に。
- 軍団モード左上の最終防衛ヒントが枠からはみ出した → 文言を短縮。
- タイトルの主人公が画面下で切れ、ロゴが細かった → 主人公の位置を調整、ロゴに影と重ね描きで太さを出した。
- やられた画面・ステージ詳細の余白 → パネルを詰め、詳細に危険の一覧を追加。

### 性能（[phase5-performance.json](Validation/Phase5/player/phase5-performance.json)）

Intel Core i7-10510U / Intel UHD Graphics、Development Player、1280×720。最終防衛中（燻煙剤3回・人間はパニック）、仲間8匹。0-10秒 三人称で移動、10-20秒 軍団モードで防衛指示、20-30秒 三人称で移動。生命力は試験用に引き上げている。

| 指標 | 結果 |
|---|---:|
| 平均FPS | **59.9fps** |
| フレーム時間95%点 | **16.77ms** |
| 最大フレーム時間 | 27.53ms |
| GC Allocated In Frame最大 | 769,884 bytes（軍団モード切替の瞬間、1回のみ） |
| Unity Total Reserved Memory | 約421MiB |

30fps目標を満たした。予約メモリがPhase 4（約299MiB）より多いのは、検証でタイトル⇔ステージを4回読み直した後に計測しているため（通常版の起動直後のワーキングセットは約204MiB）。長時間プレイ・通常版単体での計測は未実施。

## 未確認事項・制限

- 実際のキーボード・マウスでの難易度・テンポ（各ゲージの目標、占領12秒、燻煙剤の間隔・半径、評価の境目）は手動確認が必要。数値は暫定で `Data/Stages/Stage01_Kitchen.asset` から調整できる。
- 「駆除業者」は別キャラクターではなく、同じ住人が燻煙剤を焚く演出（反応段階4）。燻煙剤は人間が歩いて置くのではなく、その場に現れる。
- ステージ2「一軒家」・3「飲食店」はカードと解放状態のみ（中身は未制作）。仕様9.4の報酬のうち「ジャンプ強化」は未実装で、見た目の色違いのみ。
- 再開時は全員が巣から始まり、置かれた罠・毒餌や補充待ちの食料は初期状態に戻る。
- 人間の退去は勝手口まで歩いて消えるだけで、ドアの開閉アニメーションはない。効果音は未実装。
- UIはIMGUIの仮置き段階。参考画像のブラシ風装飾・アイコン画像はPhase 6。
- Gitへのコミットはしていない。

次の実装は **Phase 6（参考デザインに近づけたUI演出・効果音・簡易アニメーション、Profilerでの詳細計測と画質設定の最適化、操作性調整、README・操作説明・権利確認、公開用ビルド）**。

## 追加調整（2026-10-24）

ユーザー要望による仕様変更。ステージ1（`Phase5_Stage01_Kitchen.unity`）を再生成した（Phase 5シーンは更新、Phase 1〜4のシーン・設定は変更なし）。

### 変更内容

| 項目 | 内容 |
|---|---|
| 占拠命令 | **5 占拠**（軍団モードのボタン5・通常モードのキー5、軍団モードで区域を**右クリック**でも可）。選んだ仲間を2匹ずつ組にして、まだ占領していない近い区域へ送り、区域の中心近くにとどまらせる。1匹だけなら一番近い区域へ行き「あと1匹必要 — あなたも区域に入ろう」と案内。占領が終わると、その仲間は次の区域へ自動で向かう |
| 占拠ヒント | 画面下の案内に「5 占拠 — 仲間が区域へ向かう」「あと1匹 — 5 占拠／F 追従で仲間を呼ぶ」「占領中… 40%」などを状況に応じて表示。ミッション説明・操作一覧・ポーズに5 占拠を追記 |
| チュートリアル | 第1章の初回だけ、7段階のガイド（移動 → 食料を拾う → 巣へ運ぶ → 仲間を増やす → Tab 軍団モード → 5 占拠 → 区域を占領）。押すキーをキー表示で示し、押す場所（命令ボタン5・「仲間を増やす」）に点滅枠と「ここをクリック！」、行き先（食料・巣・区域）に「▼ ここ！」を表示。状態で進むので、再開時は済んだ段階を飛ばす。ポーズの「チュートリアルを飛ばす」で終了でき、終わると `save.json` の `tutorialDone` に記録 |
| スリッパの届く範囲 | テーブルの天板・食器棚の天板、壁の低い所（床から3.6まで）にいる主人公も叩かれる。予告の橙の枠は叩く面（天板・壁）に表示。高い壁（3.6より上）・天井・冷蔵庫の上・家具の裏面は安全。天板を叩いても、そばの床の仲間には当たらない |
| 開始時の仲間 | **0匹（主人公1匹だけ）**。食料で繁殖して増やす |
| 仲間の死 | スリッパ・スプレー・燻煙剤でやられた仲間は**復活しない**。死体が25秒残り、主人公がそばで **E** を押すと食べて**生命力 +1**（満タンの時は食べない）。死体は仲間の数・後継・評価に数えない |

調整値は `Data/Stages/Stage01_Kitchen.asset` の「仲間・人間（Phase 5 調整）」: `startAllies`（0）、`allyCorpseSeconds`（25）、`corpseHeal`（1）、`slipperReach`（3.6）、`tutorial`。

スリッパの高さ3.6について: この部屋は人間（身長4.1）に対して天板が2.8〜2.9、壁が5.4、冷蔵庫の上が4.4の縮尺で、仕様相談時の「約1.8m」は人間の背丈に対する実寸の目安。ゲーム内では人間の肩の高さ（3.6）にした。

### 変更ファイル

| ファイル | 内容（既定値では旧動作を維持） |
|---|---|
| Scripts/Stage/StageTutorial.cs（新規） | 第1章チュートリアルの進行 |
| Scripts/Stage/StageDefinition.cs | 上記の調整値 |
| Scripts/Stage/StageProgress.cs | 占拠先の区域（`ICaptureZones`）、死体を食べる、各ルールの適用 |
| Scripts/Swarm/SwarmCommander.cs | 占拠命令と振り分け、右クリックで区域、死体、仲間0匹開始（`AllyLimitOverride` の既定を -1 に） |
| Scripts/Swarm/AllyRoach.cs | 占拠命令、復活しない死と死体 |
| Scripts/HumanAI/HumanAIController.cs・BlenderHumanAnimator.cs | 天板・壁を叩く（狙う面・予告の向き・当たり判定・叩く姿勢） |
| Scripts/Core/PhaseTwoSession.cs | 届く高さ、回復 |
| Scripts/Colony/ColonyManager.cs | 開始時の仲間数の上書き |
| Scripts/Save/SaveSystem.cs | `tutorialDone` |
| Scripts/UI/PhaseFiveHud.cs・HudGlyphs.cs | 命令ボタン5つ、ヒント、チュートリアル表示、死体の表示、文字一覧 |
| Editor/PhaseFiveGenerator.cs・PhaseFiveVerification.cs、Scripts/Performance/PhaseFiveScenarioTest.cs | 生成・検証 |

### 検証結果

| 検証 | 結果 |
|---|---|
| C#コンパイル | 成功。エラー・警告なし |
| 生成・再生成 | [24項目PASS](Validation/Phase5/editmode.txt)（チュートリアル、ステージ1の新しい調整値を追加） |
| Unity Play Mode | [79項目PASS](Validation/Phase5/phase5-gameplay.txt)。追加分: 仲間0匹で開始、チュートリアル7段階の進行と終了の記録、仲間がいない時の案内、5 占拠（1匹→最寄り区域・2匹ずつ2区域に分かれる・歩いて区域にとどまる・1匹では進まず主人公が入ると進む・占領後は次の区域へ）、天板と壁の低い所を叩かれる・天板を叩いてもそばの床の仲間は無事・高い壁と天井は届かない、やられた仲間は復活せず死体が残る、死体を食べて生命力+1 |
| 回帰 | Phase 4 [58項目](Validation/Phase5/regression-phase4/phase4-gameplay.txt)、Phase 3 [58項目](Validation/Phase5/regression-phase3/phase3-gameplay.txt)、Phase 2 Blender版 [39項目](Validation/Phase5/regression-blender/phase2-gameplay.txt)、旧Phase 2 [38項目](Validation/Phase5/regression-phase2/phase2-gameplay.txt) PASS（以前と同数） |
| Windows x64 | 通常版・Development検証版とも成功。Development Playerで同じ79項目PASS |
| 画面の目視 | [チュートリアル（通常）](Validation/Phase5/player/tutorial-720p.png)、[チュートリアル（軍団・5 占拠）](Validation/Phase5/player/tutorial-swarm-720p.png)、[チュートリアル（巣）](Validation/Phase5/player/tutorial-nest-720p.png)、[天板を叩く](Validation/Phase5/player/slipper-table-720p.png)、[壁を叩く](Validation/Phase5/player/slipper-wall-720p.png)、[死体を食べる案内](Validation/Phase5/player/smoke-720p.png)、[ミッション説明](Validation/Phase5/player/briefing-720p.png)。チュートリアル枠の本文とキー表示の重なりを見つけて修正済み |
| 性能 | 4回計測。1回は平均59.9fps・最大26ms（以前と同等）。他の3回は平均53〜56fps、ゲームの状態と関係ない時点で100〜880msの引っかかりが散発（計測中に他のアプリが動いていた）。新機能が原因という証拠はないが、他のアプリを閉じた状態での再計測は未実施 |

### 未確認・制限（追加分）

- 壁に張りついている時は視点が壁に近く、壁面の予告枠が細く見える。
- 実際のキーボード・マウスでの難易度（仲間0匹開始＋仲間が戻らない場合の序盤の厳しさ、届く高さ3.6、死体25秒）は手動で確認が必要。
