# Phase 4 — 育成・パニック・駆除強化

2026-10-23。Unity 6000.0.61f1 / Windows / Built-in / 標準Input Manager。
Phase 3のシーンを複製した専用シーンに、巣での繁殖・拡張・強化、食料の種類と補充、人間の反応段階（恐怖度）とルールベースの駆除対策、殺虫スプレー・毒餌、ポーズ／設定／図鑑を追加した。

## 起動・操作

- Windows版: `Build/GokiTakeover_Phase4/GokiTakeover.exe`（Dataフォルダ・DLLと一緒に保持）。
- Unity: **Tools > GOKI TAKEOVER > Generate Phase 4 Colony** で `Assets/_GokiTakeover/Scenes/Phase4_Kitchen_Colony.unity` を開き、Gameを1280×720にして **▶ Play**。

Phase 3の操作（壁登り・Space・Tab軍団モード・1〜4指示・F追従）はそのまま。追加分:

| 入力 | 動作 |
|---|---|
| E（巣の近く・何も持っていない時） | 巣を開く（ゲームは停止）。仲間を増やす・巣を拡張・強化 |
| Esc | ポーズ（再開／設定／図鑑／やり直し）。巣画面を閉じる |

### ミッション「仲間を9匹に増やそう」

1. 開始時は仲間4匹・食料0・巣Lv1（上限6匹）。
2. 食料を巣へ運ぶと、種類に応じた量が **食料（コロニーの共通資源）** に加わる。パンくず・米粒 +1、お菓子・生ごみ +2、食べ残し +3。
3. 巣で **食料3 → 仲間1匹**。上限6匹に達したら **食料8で巣を拡張**（上限9匹）。
4. 強化（食料を消費、段階ごとに費用が上がる）:

| 強化 | 効果 | 費用（Lv1→） | 最大 |
|---|---|---|---:|
| 移動速度 | 主人公・仲間の速度 +8%/段階 | 5, 8, 11 | 3 |
| 運搬力 | 運搬中の減速を軽減（Lv2で減速なし） | 5, 8 | 2 |
| 隠密性 | 見つかるまでの時間 +20%/段階 | 6, 9, 12 | 3 |
| 繁殖力 | 増員費用 3→2 | 8 | 1 |
| 耐久力 | 生命力の上限 +1 | 6, 10 | 2 |

食料が足りない・上限・最大レベルの時は **何も消費せず理由を表示**。納品した食料は20秒後に別の場所へ補充される（床9か所・天板2か所から、主人公の近くと罠の近くを除いて選ぶ）。調査済みの区域なら目印が出る。

5. 仲間が9匹になるとクリア。数値はすべて `Data/ColonySettings.asset` で調整できる。

### 人間の反応（仕様6.2の0〜3段階）

| 段階 | きっかけ | 人間の変化 |
|---|---|---|
| 平和 | 開始時 | — |
| 発見 | 1回見つかる／恐怖度15 | 歩く速さ +10%、発見が少し速い |
| 警戒 | 3回見つかる／恐怖度35 | 発見が速い、捜索時間1.5倍、**35秒ごとに毒餌・粘着シートを交互に設置** |
| パニック | 恐怖度70 | 速さ +25%、**殺虫スプレー**、24秒ごとに設置。恐怖度50未満で「警戒」に戻る |

- 恐怖度（0〜100）は、見つかる（+12）、人間に見えた仲間の数（1匹につき毎秒+1.6）、納品（+1.5）、仲間を増やす（+4）で上がり、3秒以上何も見ないとゆっくり下がる。
- 「学習」はルールで実装（仕様6.2）。部屋を4×3の区域に分け、主人公・仲間を見た回数を記録して **最も多く見た区域の最後の目撃地点** に置く。同じ区域を続けて選ばない。置く場所は、仲間の床グリッドで通れて周囲も空いており、巣から2.6m以上、食料・他の罠から離れた所に限る（通路を塞がない）。最大4個で、古い物から片付ける。
- 罠を置きに行く間は人間の通常AIを一時停止し、主人公を見つけたら中断して戻る。
- 吹き出し（「ひっ！ ゴキブリ！」「どこ行った…？」「ここに置いとくか…」「くらえっ！」など）。設定でオフにできる。

### 駆除グッズ（仕様7）

- **毒餌**: 食料に似ているが拾う（E）と生命力-1で消える。青緑の粒が見分けの手がかり。仲間が1.6m以内に近づくか、置かれた区域を **2 探索** で調べ直すと見破られ、橙の輪と「毒餌」表示に変わる。仲間は毒餌を拾わない。
- **粘着シート（設置型）**: Phase 2の粘着シートと同じ効果。仲間の経路では高コストになり、避けて歩く。
- **殺虫スプレー**: パニック中、1.4〜3.4mの距離で追跡中の主人公に使う。0.8秒の予兆（床に橙の扇）→1.6秒噴射。扇の中で、ノズルとの間に家具・壁がなければダメージ。狙いは予兆の開始時に固定（外へ逃げれば当たらない）。扇の中の仲間もやられる。天板・高い壁の上には使わない。
- 新しい危険は共通の `HazardBase`（名前・警告文・図鑑ID・範囲判定）で実装。

### ポーズ・設定・図鑑

- 設定: マウス感度、音量（効果音は未実装）、画質 低（影・MSAAなし・ライト1）／中（従来の見た目）、人間の吹き出し、画面の揺れ。変更は即時反映して `persistentDataPath/settings.json` に保存。壊れたファイルは `.bak` に退避して初期設定で起動する。
- 図鑑: ゴキブリ／人間／食料／駆除グッズ。出会った物だけ内容が表示される（記録は設定ファイルに保存）。

## 追加・変更ファイル

パスは `Assets/_GokiTakeover/` 基準。.metaも保管対象。

| 新規ファイル | 役割 |
|---|---|
| Scripts/Colony/ColonyManager.cs | 食料資源・納品の集計・補充、巣画面、繁殖・拡張・強化、設定と図鑑の保存 |
| Scripts/Colony/ColonySettings.cs | 巣・強化・反応・スプレーの調整値 |
| Scripts/Colony/FoodValue.cs | 食料の種類と価値 |
| Scripts/HumanAI/HumanReaction.cs | 恐怖度・反応段階、目撃記録、罠と毒餌の設置、スプレー、吹き出し |
| Scripts/Hazard/HazardBase.cs / PoisonBait.cs / InsecticideSpray.cs | 危険物の共通部分、毒餌、殺虫スプレー |
| Scripts/Core/GameSettings.cs | 設定・図鑑のJSON保存（読込失敗に強い） |
| Scripts/UI/PhaseFourHud.cs / HudGlyphs.cs | Phase 4 HUD・巣画面・ポーズ／設定／図鑑、表示文字の事前読み込み |
| Scripts/Performance/PhaseFourScenarioTest.cs | Editor／明示起動したDevelopment版だけの検証・計測 |
| Editor/PhaseFourGenerator.cs / PhaseFourVerification.cs | シーン生成・ビルド、自動検証と回帰テスト |
| Scenes/Phase4_Kitchen_Colony.unity | 遊べる専用シーン |
| Data/ColonySettings.asset、Art/Meshes/Phase4_SprayFan.asset、Art/Materials/Phase4_*.mat | 調整値、スプレーの扇、食料・毒餌・スプレー缶の色 |

| 変更ファイル | 内容（既定値では旧動作を維持） |
|---|---|
| Scripts/Character/RoachMotor.cs | 強化用の速度倍率 |
| Scripts/Camera/ThirdPersonRoachCamera.cs | 感度の設定、画面揺れの倍率 |
| Scripts/Core/PhaseTwoSession.cs | 運搬速度・生命力上限の強化、外部からのクリア |
| Scripts/HumanAI/HumanAIController.cs | 一時停止、発見時間・速度・捜索時間の倍率 |
| Scripts/Hazard/StickyTrap.cs | 非アクティブ（設置待ち）の時は効果なし |
| Scripts/Swarm/AllyRoach.cs / AllyNavGrid.cs / SwarmCommander.cs | 仲間の増員・速度倍率、設置した罠の経路コスト（実行時コピーのみ変更）、区域の再調査、HUDの差し替え口 |
| Scripts/UI/PhaseThreeHud.cs | 共通インターフェースの宣言のみ |

Phase 1〜3のシーン・Prefab・Phase 2/3設定・仲間グリッドは変更していない（生成前後のSHA-256比較を自動検証に含む）。ProjectSettings・Packagesも変更なし。

## 自動検証

Unityを閉じ、プロジェクト直下のPowerShellで実行（`GenerateAndVerify` は再生成を含む）。

```powershell
$unityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.0.61f1\Editor\Unity.exe'
$projectRoot = (Get-Location).Path
New-Item -ItemType Directory -Force 'Logs/Phase4' | Out-Null
foreach ($method in 'GenerateAndVerify','VerifyPlayMode','VerifyPhaseThreeRegression','VerifyBlenderRegression','VerifyPhaseTwoRegression') {
  Start-Process $unityEditor -Wait -WindowStyle Hidden -ArgumentList @(
    '-batchmode', '-nographics', '-projectPath', ('"' + $projectRoot + '"'),
    '-executeMethod', ('GokiTakeover.Editor.PhaseFourVerification.' + $method),
    '-logFile', ('"' + $projectRoot + '\Logs\Phase4\' + $method + '.log"'))
}
Start-Process $unityEditor -Wait -WindowStyle Hidden -ArgumentList @(
  '-batchmode', '-quit', '-projectPath', ('"' + $projectRoot + '"'),
  '-executeMethod', 'GokiTakeover.Editor.PhaseFourGenerator.BuildBoth',
  '-logFile', ('"' + $projectRoot + '\Logs\Phase4\build.log"'))
```

自動検証は設定ファイルを出力フォルダ内の試験用ファイルに切り替えるため、プレイヤー本来の設定・図鑑は書き換えない。Development検証版: `Build/GokiTakeover_Phase4_Validation/GokiTakeover.exe -goki-phase4-test -goki-output <出力先>`。

## 検証結果

| 検証 | 結果 |
|---|---|
| C#コンパイル | 成功。エラー・警告なし |
| 生成・再生成 | [25項目PASS](Validation/Phase4/editmode.txt)。既存シーン・設定の保全、GUID・アセット数維持、カメラ1台、仲間9匹、食料8個すべてに種類と価値（合計13）、毒餌3・設置用粘着3（待機）、HUD差し替え、補充地点11か所（床の9か所すべてに仲間の経路） |
| Unity Play Mode | [58項目PASS](Validation/Phase4/phase4-gameplay.txt)。種類別の加算と二重加算なし、補充、巣画面で停止、繁殖・費用不足・上限・拡張・強化5種の効果・最大・費用上昇、9匹でクリア、リトライ初期化、反応段階の上昇と「警戒」への戻り、目撃区域への毒餌→粘着シート設置と制御の返却、通路を塞がない、仲間による毒餌の判別、毒餌で生命力-1、スプレーの予兆→噴射・仲間もやられる・扇の外と壁越しは無傷、仲間を見られて恐怖度上昇、設定の保存・反映・画質切替・壊れたファイルの退避、図鑑の保存 |
| 回帰（共通スクリプト変更） | Phase 3で[58項目](Validation/Phase4/regression-phase3/phase3-gameplay.txt)、Phase 2（Blenderモデル版）で[39項目](Validation/Phase4/regression-blender/phase2-gameplay.txt)、旧Phase 2版で[38項目](Validation/Phase4/regression-phase2/phase2-gameplay.txt)PASS |
| Windows x64 / Mono | 通常版・Development検証版とも成功 |
| Development Player | [同じ58項目PASS](Validation/Phase4/player/phase4-gameplay.txt)、実行時例外なし |
| 通常版の起動 | 8秒起動、プロセス継続、例外ログなし（ワーキングセット約136MiB） |
| 画面の目視 | [巣](Validation/Phase4/player/nest-720p.png)、[設定](Validation/Phase4/player/settings-720p.png)、[図鑑](Validation/Phase4/player/codex-720p.png)、[スプレー予兆](Validation/Phase4/player/spray-720p.png)、[三人称](Validation/Phase4/player/tps-720p.png)、[軍団モード](Validation/Phase4/player/swarm-720p.png)、[クリア](Validation/Phase4/player/clear-720p.png)。日本語の欠落・はみ出しなし |

### 計測中に見つけて直した問題

- **納品6個で誤ってクリアになる**: Phase 3の仲間管理が起動時にPhase 3の納品目標（6個）を設定し、Phase 4側の「納品数ではクリアしない」設定を上書きしていた。設定順を修正し、試験項目を追加した。
- **軍団モードを初めて開いた瞬間のカクつき**（最大96ms、1.5MBのGC）: IMGUIが画面を初めて描く時の文字・グリフ準備が原因と切り分けた。表示文字の事前読み込みと、起動直後に全画面を画面外へ一度描くウォームアップを追加し、最大フレーム時間は22〜29msに下がった。

### 性能（[phase4-performance.json](Validation/Phase4/player/phase4-performance.json)）

Intel Core i7-10510U / Intel UHD Graphics、Development Player、1280×720。仲間8匹、人間は「パニック」（期間中に毒餌・粘着シートを各1回設置、スプレーあり）。0-10秒 三人称で人間に近づく、10-20秒 軍団モードで全員に回収指示、20-30秒 三人称で移動。計測が途中でやり直しにならないよう、主人公の生命力上限を試験用に引き上げている（画面の「23/23」はそのため）。

| 指標 | 結果 |
|---|---:|
| 平均FPS | **59.8fps** |
| フレーム時間95%点 | **16.69ms** |
| 最大フレーム時間 | 28.73ms |
| GC Allocated In Frame最大 | 643,372 bytes（軍団モード切替の瞬間。ほかに約205KBが1回） |
| Unity Total Reserved Memory | 約299MiB |

30fps目標を満たした。GCの残りは軍団モード切替時のIMGUI・文字列処理で、毎フレームのものではない。仲間9匹＋長時間・通常版単体のFPSは未計測。

## 未確認事項・制限

- 実際のキーボード・マウス操作での難易度・テンポ（食料の補充20秒、罠の間隔、スプレーの射程）は手動確認が必要。数値は暫定で `ColonySettings` から調整できる。
- 人間は仲間を追跡・攻撃しない（スプレーの巻き添えとスリッパの着地点にいた場合のみ）。
- 「巣に入る」演出はなく、巣の近くでEを押すと画面が開く。施設の建設・フェロモン（第2資源）はPhase 5以降。
- 反応段階4（駆除業者・最終防衛）と5（撤退）、エリア支配ゲージ、セーブ／再開、タイトル画面はPhase 5。
- 効果音は未実装（設定の音量は将来用）。人間のスプレー動作は専用アニメーションではなく、缶の表示と床の扇で表現。
- UIはIMGUIの仮置き段階（装飾・アイコン画像はPhase 6）。

次の実装は **Phase 5（エリア支配ゲージ3種と最終防衛イベント、クリア／敗北／評価、セーブ・再開、タイトル→出撃→クリアの一連の流れ）**。
