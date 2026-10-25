# Phase 1 見た目の改善版

2026-10-22。ユーザーの「参考画像に近い映画的な雰囲気」という指定に合わせた、Phase 1のビジュアル改善。人間AI、回収・納品、軍団モードなどのゲーム機能は追加していない。

## 変更した見た目

- 照明: 暖色の主光・ペンダント灯、冷たい窓と環境光を組み合わせた夜のキッチン。弱い距離フォグと画面周辺の陰影で奥行きを付けた。
- 陰影: 家具・主人公の実際の影と、小さな接地影を追加。影を描くライトは1灯、カスケードは1、距離16m・Medium設定に限定。2x MSAAで輪郭を整えた。
- 室内: ティール色の枠付き扉、面取りした木の家具、真鍮の取っ手・脚先、開いた冷蔵庫と棚、窓・水栓・コンロ・食器・瓶・布・植物。
- 材質: 256×256の木目・石タイル・織物テクスチャをC#生成。マットな塗装、陶器、金属、甲殻の艶を分けた。反射プローブや画面空間反射は使っていない。
- 主人公: 厚みのある左右甲殻、滑らかな法線、翅の筋、関節、細く曲がった触角、控えめな目のハイライト。C#生成の共有メッシュを引き続き使用。
- カメラ: 主人公が画面の少し下に見える低い背後視点へ調整。壁・家具に対する既存の退避処理を維持。
- HUD: 大きい仮パネルを小さい情報表示へ整理。日本語の目標、HP、FPS、操作説明を残した。
- 人間の目印: 改善版では積み木状の人型の代わりに、住人の存在を示す固定スリッパを配置。人間AI・トラップ機能は付けていない。食料目印はパンくずの見た目に変更。
- 床の瓶・倒れたカップにも固定Colliderを追加し、主人公・カメラがすり抜ける箇所を修正。瓶の配置も床に接地させた。

## 保持したもの

元の `Phase1_Kitchen_Test.unity`、`Cockroach_Player.prefab`、旧Artデータ、旧Windows版は保持。元データのSHA-256を確認し、改善版の生成で書き換えていない。元シーンにある人型マーカーも保持。

新しいシーンは `Assets/_GokiTakeover/Scenes/Phase1_Kitchen_Polished.unity`、主人公は `Prefabs/Cockroach_Player_Polished.prefab`、素材は `Art/Polished/` に分離。色・形状・材質の暫定値は生成コードとInspectorに置いている。

標準Input Manager、WASD／Shift／マウス操作、床上移動、単一CharacterController、三人称カメラというPhase 1の範囲を維持している。Blender、有料素材、外部API、追加Unityパッケージは導入していない。

## 起動と再生成

Windows版は改善版専用フォルダ:

```text
Build/GokiTakeover_Polished/GokiTakeover.exe
```

エクスプローラーでダブルクリック。Game画面を左クリックし、WASDで移動、Shiftでダッシュ、マウスで視点、Escで操作解除、Alt+F4で終了。旧版の `Build/GokiTakeover_Phase1/` と比較できる。

Unity 6000.0.61f1でのメニュー:

1. **Tools > GOKI TAKEOVER > Generate Cinematic Kitchen**: 保存済み改善版シーンを開く。未生成なら作る。
2. **▶ Play**: 1280×720で試す。
3. **Rebuild Cinematic Kitchen**: 改善版だけを初期配置から再生成。改善版シーン・生成Prefab・生成素材の手編集は上書きされるため、必要なら先に別名保存／Gitで保管する。
4. **Build Cinematic Windows 720p**: 改善版の通常Windowsビルドを作る。Developmentの透かしを出さない構成。

共通のPlayerSettingsをビルド中だけ1280×720ウィンドウにし、終了後は復元する。全体のBuild Settingsのシーン一覧も変更しない。

## ファイル差分

既存C#の変更は4本:

| ファイル | 変更 |
|---|---|
| Scripts/Character/CockroachVisualController.cs | 脚の太さをInspector設定に。既定値は旧モデルと同じ |
| Scripts/Camera/ThirdPersonRoachCamera.cs | 初期俯角と追従構図を設定可能に。旧シーンの既定値は維持 |
| Scripts/Performance/PhaseOneSmokeTest.cs | 改善版HUDへの対応と、到着時画面キャプチャの追加 |
| Editor/PhaseOneBuild.cs | 共通ビルド処理を整理し、改善版の通常／検証ビルドを追加。旧メニューの出力先を維持 |

追加したコード:

```text
Assets/_GokiTakeover/
  Editor/PolishedAssets.cs
  Editor/PolishedCockroachGenerator.cs
  Editor/PolishedKitchenGenerator.cs
  Editor/PolishedVerification.cs
  Scripts/Core/PolishedPresentation.cs
  Scripts/UI/PolishedHud.cs
  Art/Polished/ContactShadow.shader
```

ほかに `Art/Polished/{Meshes,Materials,Textures}`、改善版Prefab・シーン、各.meta、検証画像・JSONを追加した。新規テクスチャとメッシュはこのプロジェクトの生成コードによる自作データで、第三者素材の追加はない。Windowsの日本語OSフォントを実行時に利用し、フォントファイルの再配布は行わない。

## 検証方法・制約

生成テストは再生成の重複・GUID、元のシーン／Prefab保全、モデル構造、カメラ数、影ライト数を確認。Play Modeは移動、ダッシュ、床・壁・机脚、低いカメラの衝突と品質設定を確認する。

自動検証のEntry Point:

```text
GokiTakeover.Editor.PolishedVerification.GenerateAndVerify
GokiTakeover.Editor.PolishedVerification.VerifyPlayMode
GokiTakeover.Editor.PhaseOneBuild.BuildPolishedValidation
GokiTakeover.Editor.PhaseOneBuild.BuildPolished
```

Play Modeの品質設定検証はDirect3D 11有効のEditorで行う。Unity 6はカスケード数0を1に正規化するため、コードでは1を明示する。

性能はDevelopment検証版を通常表示で起動し、`-goki-smoke-test` を指定して30秒の歩行・ダッシュ・カメラ旋回を計測。起動直後とPNG読み出し中は除外する。通常ビルドと検証ビルドは同じシーン・描画設定を使用し、検証版だけにDevelopment用計測処理を含める。

光・反射・描画密度はコンセプト画像を完全に再現したものではない。対象PCでの軽さを優先し、フルGI、SSAO、SSR、ポストエフェクトの重い構成は導入していない。手動操作の感触・長時間試験、将来のAI追加後の性能は別途確認する。

Unity品質APIの確認資料: [QualitySettings](https://docs.unity3d.com/cn/6000.0/ScriptReference/QualitySettings.html)、[Light.shadowResolution](https://docs.unity3d.com/cn/6000.0/ScriptReference/Light-shadowResolution.html)。

## 実施した検証の結果

最終モデルは**6498三角形**。頭・胴体・目・6脚・2触角は共有メッシュを再利用し、左右の翅カバーと翅の筋もC#で生成している。全アセットの.metaを含めて保持する。

| 検証 | 結果 |
|---|---|
| Unity Editor/PlayerのC#・シェーダーコンパイル | 成功。新規コンパイルエラーなし |
| 改善版生成・再生成 | 10項目PASS。重複なし、GUID維持、元シーン／Prefabの保全、部位・モデル予算・カメラ・ライトの上限を確認 |
| 改善版Play Mode / Direct3D 11 | 12項目PASS。歩行・ダッシュ・接地・壁・机脚・机下通過・床の瓶・倒れたカップ・カメラ衝突・停止姿勢・影とAAを確認 |
| 元シーンのPlay Mode回帰確認 | 15項目PASS。共通Runtimeの拡張後も旧モデルの動作を維持 |
| Windows x64 / Mono / Development | 改善版の計測用ビルド成功 |
| Windows x64 / Mono / 通常ビルド | 改善版の通常ビルド成功。旧版出力先を変更していない |
| 通常ビルドの起動確認 | 最終版でDirect3D 11の初期化・ウィンドウ起動・終了を確認。新規例外なし。通常版の画面キャプチャは最前面チェックによりスキップし、別ウィンドウを撮影していない |
| 元データ保全 | 元シーン・Prefab・Art・ProjectSettings・Packages・参考画像・元ドキュメントのSHA-256一致。変更した既存コードは差分表の4本のみ |

根拠ログは `Logs/polished-generation-props.log`、`polished-playmode-props-final.log`、`polished-baseline-regression.log`、`polished-build-final.log`。LogsはGit除外。

### 最終モデルの性能計測1回目

同じ実機のIntel Core i7-10510U / Intel UHD Graphics / RAM約16GB / Windows 11、1280×720ウィンドウ、60fps上限、Developmentビルドで実施。Unity Editorは終了してから計測した。

| 指標 | 1回目 |
|---|---:|
| 測定時間・フレーム数 | 30.0076秒 / 1785フレーム |
| 平均FPS | **59.49fps** |
| フレーム時間95％点 | **16.76ms** |
| 最大フレーム時間 | **108.01ms** |
| GC Allocated In Frame最大観測値 | 17844 bytes |
| Unity Total Reserved Memory観測値 | 287899811 bytes（約274.6MiB） |

[元の計測JSON](Validation/Polished/performance-01.json)。メモリ値はUnityの予約領域で、OS全体やWorking Setではない。CPU/GPUの個別実行時間を測ったものではない。

平均・95％点では720p/30fps目標を上回ったが、**単発108msのフレームがあり、全フレームで30fps以上とは言えない**。原因はこの測定では特定していない。通常ビルドそのもののFPS、手動操作、長時間プレイ、後続AI追加時の性能は未測定。比較用の旧版平均値と新モデルの値を取り違えない。

同一の最終ビルドで再測定した2回目は、30.0126秒・1789フレーム、平均**59.61fps**、95％点**16.71ms**、最大**101.63ms**、GC最大17788 bytes、Unity予約メモリ287899811 bytesだった。[2回目の元JSON](Validation/Polished/performance-02.json)。再試験でも100ms前後のスパイクを観測したため、既知の未解決項目として残す。平均FPSだけを根拠に、引っかかりがないとは判定しない。

1・2回目のあと、床の瓶と倒れたカップへのCollider追加・接地位置修正を行った。キャラ、照明、描画設定はそのまま維持した。**小物衝突修正を含めた最終シーン**で再ビルド・再測定した値は以下。

| 指標 | 最終ビルド |
|---|---:|
| 測定時間・フレーム数 | 30.0051秒 / 1787フレーム |
| 平均FPS | **59.56fps** |
| フレーム時間95％点 | **16.75ms** |
| 最大フレーム時間 | **55.78ms** |
| GC Allocated In Frame最大観測値 | 17732 bytes |
| Unity Total Reserved Memory観測値 | 287898971 bytes |

[最終計測JSON](Validation/Polished/performance-final.json)。この測定でも33.3msを超えるフレームがあり、スパイクの解消を確認したとは扱わない。計測したのは最終シーンのDevelopment検証版であり、通常ビルド自体のFPS測定ではない。

### 実際の画面

- [到着時の改善版](Validation/Polished/arrival-720p.png)
- [移動中の改善版](Validation/Polished/gameplay-720p.png)
- [比較用の旧版](Validation/phase1-player-720p.png)

上記改善版2枚は計測用Developmentビルドの実際の描画。通常ビルドはDevelopment用の透かしを表示しない。コンセプト画像を合成したプレビューではない。

## 未完了と次のフェーズ

- 手動のキー入力・マウス感度・全ての家具の隙間・長時間の操作感確認はユーザー実施待ち。
- 単発フレームスパイクの原因特定、CPU/GPU詳細プロファイルは未完了。
- 甲殻・家具・照明は軽量なスタイライズ表現。コンセプトPNGと同等の高精細な反射・物理照明を達成したという主張はしない。
- Phase 2は未着手。見た目とPhase 1操作の確認後、食料3個の回収・巣納品、人間1体、スリッパ攻撃・粘着トラップ、HP0とリトライへ進む。
- Gitのステージング／コミットは行っていない。`git status --short --untracked-files=all` で新しいC#・シーン・素材・.meta・本書・検証画像とJSONを確認する。未追跡の新規ファイルは通常の `git diff` には表示されない。Build/Logs/Library/Tempは含めない。
