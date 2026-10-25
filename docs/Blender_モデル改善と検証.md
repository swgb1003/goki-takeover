# Blenderモデル改善と検証

実装日: 2026-10-23。対象: 既存Unity 6000.0.61f1のPhase2キッチン。

## 起動と比較

- Windows実行版: `Build/GokiTakeover_BlenderModels/GokiTakeover.exe`。DataフォルダやDLLを移動・削除しない。
- Unityシーン: `Assets/_GokiTakeover/Scenes/Phase2_Kitchen_BlenderModels.unity`。
- Editorメニュー: `Tools > GOKI TAKEOVER > Open Blender Model Kitchen`。
- [ゴキブリ・変更前](Validation/BlenderModels/roach-before.png) / [変更後](Validation/BlenderModels/roach-after.png)。
- [住人・変更前](Validation/BlenderModels/human-before.png) / [変更後](Validation/BlenderModels/human-after.png)。
- [新旧比較一覧](Validation/BlenderModels/model-comparison.png)。
- [キッチン内の描画](Validation/BlenderModels/offscreen-gameplay.png)。検証用RenderTextureのためオーバーレイUIは含まない。

比較画像はUnityの同じカメラ・照明・背景・最高詳細LODで撮影した実メッシュ。ゲーム背景への画像貼り付けではない。

## 改善内容

ゴキブリは扁平な体、重なる翅と翅脈、前胸背板、腹部の節、細い触角、関節のある6脚と脛の棘を制作した。既存の脚IK・触角の動きに新しい16ボーンのスキンを追従させている。

住人は成人の体格に合わせた胴・顎・鼻・まぶた・耳・髪、肩と袖、シャツのポケット・縫い目・ボタン、ズボンの折り目、指、スリッパを制作した。19ボーンで歩行・膝・腕振り・見下ろし・スリッパ攻撃を動かす。手付けの細密な表情アニメーションやフォトリアルな肌テクスチャは含まない。

形状と表面の質を上げる範囲で、既存の食料回収・納品・罠・人間AI・ダメージ・死亡・リトライを維持した。元シーン・旧Prefab・既存ゲームスクリプトを直接書き換えず、新シーンと新アセットへ追加している。

## モデル予算

数値は三角形数。距離に応じて3段階を切り替え、同時に複数LODを描くクロスフェードは使わない。

| モデル | LOD0（近） | LOD1（中） | LOD2（遠） | ボーン |
|---|---:|---:|---:|---:|
| ゴキブリ | 7,768 | 4,024 | 1,940 | 16 |
| 住人 | 11,748 | 5,640 | 2,340 | 19 |

各LODはSkinnedMeshRenderer 1個・マテリアル1個。両キャラで同じマテリアルを共有する。追加の高解像度テクスチャなし。色と滑らかさを頂点カラーに格納し、不透明なStandard Surface Shaderで描画する。近・中距離は最大2ボーン/頂点、遠距離は1ボーン。画面外スキン更新・スキンモーションベクトル・遠距離モデルの影を無効にした。新しい物理コライダーや常駐カメラは追加していない。

1 Renderer＝最終フレームの1 draw callという意味ではない。影やライティングのパスは別途発生する。三角形数だけで全状況の動作速度を保証するものでもない。

## 制作元と再生成

- 編集元: `ArtSource/Blender/GokiTakeover_Characters.blend`。各リグの下に3段階のメッシュがあり、通常はLOD0のみ表示。
- 汎用交換用: `ArtSource/Blender/Cockroach.fbx`、`Resident.fbx`。
- 制作スクリプト: `Tools/Blender/build_character_models.py`。
- Unity取込データ: `Assets/_GokiTakeover/Art/BlenderModels/Source/*.json`（version 1、頂点・法線・色・UV・三角形・骨・ウェイト）。
- Unityアセット生成: `Assets/_GokiTakeover/Editor/BlenderModelIntegration.cs`。

Blenderで本物のメッシュとアーマチュアを制作し、座標・法線・ウェイトをJSONへ書き出してUnityのMeshアセットへ取り込む。FBXも保存するが、ゲームはJSONから生成したネイティブアセットを使う。Unityの自動.blend変換や実行時Blender起動への依存を避けるため、.blend/FBXはAssets外に保管した。

再生成する場合はこのプロジェクトのUnity Editorを閉じ、プロジェクト直下で以下を実行する。インストール先が異なる場合はPS1内の実行ファイルパスを合わせる。

```powershell
powershell -ExecutionPolicy Bypass -File Tools/Blender/Build-GameModels.ps1
```

BlenderはCPU 2スレッドでメッシュを生成し、通常はプレビューのレイトレースを実行しない。必要な場合だけ `-RenderPreview` を追加する。Unityもワーカー2本・低いプロセス優先度で、Blender終了後に検証とビルドを行う。`-SkipBlender` は書き出し済みJSONを使う。

注意: スクリプト再実行は今回追加した制作元・書き出しファイルを再生成する。Blenderで手編集する場合は別名保存し、手編集版の書き出し工程を用意してから差し替える。

## 検証記録

- モデル構造・予算・法線方向・ウェイト・再生成・GUID保持・元シーン/Prefab保全: [59項目PASS](Validation/BlenderModels/editmode.txt)。
- 実行時スキンの有限頂点、股関節・膝の既存IK追従: [3項目PASS](Validation/BlenderModels/skin-playmode.txt)。
- Phase2回帰テスト: [33項目PASS](Validation/BlenderModels/phase2-gameplay.txt)。食料3個、納品、クリア、罠、攻撃、死亡、リトライなど。
- Development検証用と通常Windows版の両ビルド成功。検証・撮影コードは通常版のコンパイル対象外。
- 通常版は非表示ウィンドウで6秒起動し、D3D初期化・プロセス継続・例外ログなしを確認。[起動記録](Validation/BlenderModels/release-smoke.json)。対話操作を含む通常版の長時間テストは未実施。
- 既存ファイル278個のSHA-256を比較し、277個が不変、変更1個は素材履歴ドキュメントへの追記、削除0個。[保全記録](Validation/BlenderModels/preservation.json)。ルートREADMEは起動案内を更新。
- 新旧比較4枚とキッチン描画をUnityで撮影し、裏面抜けや肩の隙間、体表の黒い点を確認・修正した。

負荷測定の数値・条件は [phase2-performance.json](Validation/BlenderModels/phase2-performance.json) と [process-measurement.json](Validation/BlenderModels/process-measurement.json) に保存。計測はこのPC（Core i7-10510U / Intel UHD Graphics / メモリ16GB）上のDevelopment実行ファイル、60fps上限、1280×720。

非表示ウィンドウで実描画を止めないよう、専用検証コードが毎フレームCamera.Renderで1280×720のRenderTextureへD3D11描画した。30秒の巣周辺ループと人間巡回を測定している。OSへのウィンドウ提示・オーバーレイUIを含む通常プレイのfpsではなく、全ルートや大群時の性能も測っていない。元Phase2の測定とは条件が違うため、fpsの厳密な新旧比較には使わない。

最終実行の結果: 平均56.25fps、95パーセンタイル17.09ms、最大363.54ms、Unity予約メモリ約262.4MiB。機能テストを含む約49秒間のプロセス監視では最大ワーキングセット286.6MiB。一時的な大きいフレーム時間があるため「常時60fps達成」とは判定していない。GPU使用率・温度・電力は計測していない。

使用Skill: なし。今回は3D形状・リグ・Unity描画連携が対象で、UI/HUDの設計・改修は行っていない。利用可能一覧にUnity専用Skillはなかったため、Blender/Unityの実機と既存実装を確認して作業した。

## 追加修正（2026-10-23 午後）: 住人のリアル化・壁際の閉じ込め・スリッパ攻撃

### 住人モデルを作り直し
- `Tools/Blender/resident_model.py` を新設。プリミティブの寄せ集めではなく、頭部は輪郭ロフト＋ガウス状の彫り込み（眼窩・まぶた・鼻・唇・頬骨・顎）、体は重ねた形状をボクセルリメッシュで一体化→スムーズ→デシメートで3LOD化。
- 肌・髪・眼球・シャツ・ズボン・足・スリッパを別シェルにして、袖口・裾・生え際が実物の重ね着のようにくっきり出るようにした。成人の実寸比（頭身・肩幅・関節高さ）に合わせた。
- 色・艶に加えて環境遮蔽（脇・指の間・耳裏などの陰り）を頂点カラーへ焼き込み。テクスチャ追加なし、ボーン19本・Renderer1個・マテリアル1個は従来どおり。
- 参考画像: [顔](Validation/RealisticResident/resident-face-blender.png) / [全身](Validation/RealisticResident/resident-body-blender.png)（Blender EEVEE）。ゴキブリのモデルは変更なし（Cockroach.jsonのハッシュ一致を確認）。

### 壁際で暗黒空間に閉じ込められる不具合
- 原因: 人間のカプセルコライダーがNavMeshAgentで移動してゴキブリに重なると、CharacterControllerの重なり解消（PhysX）が壁を無視して押し出し、厚さ0.24の壁や家具の内部へ抜けていた。修正を無効化した状態で回帰テスト「壁際で人間に押し込まれても壁を抜けない」が失敗することを確認済み。
- 修正（`RoachMotor`）: 人間との物理衝突を無効化し、押し出しは自前で `Move()` 経由で行う（壁で止まる）。さらに家具内部への埋まり・床下落下を検出したら直前の安全位置へ戻す。

### スリッパで叩き潰すモーションと被弾条件
- 住人は標的へ向き直り踏み込んでしゃがみ、スリッパを頭上へ振りかぶって振り下ろし、靴底を標的へ平らに叩きつけてから持ち上げる（`BlenderHumanAnimator` が2ボーンIKで腕・脚・手首を解く）。[連続画像](Validation/RealisticResident/slipper-slam-sequence.png) / [側面](Validation/RealisticResident/slipper-slam-side.png)。
- ダメージは靴底が着地した瞬間に、靴底の範囲（幅0.145×長さ0.39＋余裕0.28の楕円）の下にいた時だけ。予兆中・振りかぶり中・スリッパの横ではダメージなし。予兆の橙色の枠も靴底範囲の形と向きに変更。潰された瞬間にゴキブリが平たく潰れ、カメラが揺れる。家具の下は従来どおり遮蔽される。
- 調整値は `PhaseTwoSettings`（attackRecover, strikeDistance, slipperHalfWidth/Length, crushMargin）。旧attackRadiusは廃止。

### 検証
- 編集時チェック59項目PASS、スキン3項目PASS、Phase2回帰テスト39項目PASS（[記録](Validation/RealisticResident/phase2-gameplay.txt)）。追加項目: 着地前は無傷、横0.6では無傷、着地の瞬間のみHP-1、靴底が標的上で下向き、壁際押し込みで壁を抜けない・自力で脱出可、家具内部/床下から安全位置へ復帰。旧Phase2シーンでも同テストPASS。
- 通常版・検証版ビルド成功。通常版を8秒起動し例外ログなし。対話操作による長時間プレイ・fps再計測は未実施。

## 戻し方

旧版で遊ぶ場合は `Assets/_GokiTakeover/Scenes/Phase2_Kitchen_Infiltration.unity`、または従来のPhase2ビルドを使用する。新しいモデルは別シーン・別ビルドで追加されているため、既存シーンを上書き復元する必要はない。
