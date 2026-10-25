# Phase 1 検証結果

実施日: 2026-10-22。**コード作成だけでなく、インストール済みUnity 6000.0.61f1で生成・Play Mode・Windowsビルドを実行した。** 手動の操作感確認は未実施。

## 結果

| 検証 | 結果・証拠 |
|---|---|
| 初回Unityプロジェクト作成・起動 | 成功。`Logs/initialize.log` |
| C#コンパイル | Editor/Playerとも成功。新規のC#コンパイルエラーなし。`Logs/generation-final.log`、`Logs/playmode-final.log`、`Logs/build-final.log` |
| EditMode生成チェック | **16項目PASS**。`Logs/phase1-editmode.txt` |
| PlayModeでの実際の物理・Transformチェック | **15項目PASS**。`Logs/phase1-playmode.txt` |
| Windows x64 Developmentビルド | 成功・終了コード0。`Build/GokiTakeover_Phase1/GokiTakeover.exe` |
| 通常表示のPlayer起動 | Direct3D 11 / Intel UHD Graphicsで描画。起動・自動試験・終了まで完了。`Logs/player-final.log` |
| 1280×720描画の目視 | [最終スクリーンショット](Validation/phase1-player-720p.png)を確認。日本語HUD、3Dモデル、家具、床タイルを描画。文字欠落なし |
| 既存資料の保全 | docsの元2ファイル＋PNG6枚、計8ファイルのSHA-256が開始時と一致。READMEも編集していない |
| Editor API分離 | Scripts/配下にUnityEditor参照0件。Editor用コードはEditor/配下のみ |
| Git除外 | Library/Temp/Obj/Logs/UserSettings/Buildの除外を確認。.metaは除外されない |

## EditMode 16項目

1. キャラ再生成時にPrefabのGUIDを維持。
2. 再生成時にメッシュ・マテリアルのアセット数を維持。
3. 脚6本・触角2本の参照。
4. 目2個。
5. 甲殻2枚。
6. Tripodグループが3本ずつ。
7. 単一CharacterController・子Collider/Rigidbodyなし。
8. キャラのメッシュが2種類の共有。
9. キャラのマテリアルが4種類の共有。
10. 主人公の三角形総数が3000未満（実モデル1780）。
11. Prefab生成時に開いているシーンへルートを増やさない。
12. 生成ラベルのない既存アセットを保護。
13. 専用シーン再生成時にGUIDを維持。
14. キッチン再生成時にルート数を維持。
15. シーン内カメラが1台。
16. 主人公が1匹。

## PlayMode 15項目

UnityのPlay Mode内で、同じMotorへ再現可能な入力を渡して実施。OSのキーボード・マウス操作を模倣したテストではない。

1. 1秒の歩行距離が1.65m。
2. 重力で床に接地。
3. ダッシュで移動距離2倍。
4. 斜め移動で速度が増えない。
5. 壁へ走っても突き抜けない。
6. 机の脚への衝突。
7. 机中央下を通過可能。
8. 背後の壁でカメラを押し戻す。
9. カメラの衝突球が壁と重ならない。
10. 主人公に追従してカメラ位置を更新。
11. 歩行中にTripodの3本が接地。
12. 停止時に6本の足先が休止位置へ戻る。
13. Idle時に触角角度が変わる。
14. 脚のTransformがNaNにならない。
15. 試験シーンでは影・AAがOFF。

## 最終Windows版の30秒測定

[元の計測JSON](Validation/phase1-performance.json)。Playerの通常描画を使用し、`-batchmode` / `-nographics`は付けていない。Unity Editorを閉じてから実行。

| 条件・指標 | 結果 |
|---|---|
| OS | Windows 11 64bit / 10.0.26200 |
| CPU | Intel Core i7-10510U @ 1.80GHz |
| GPU | Intel UHD Graphics / Direct3D 11 |
| メモリ | SystemInfo取得値16182MB |
| ビルド | Unity 6000.0.61f1 / Windows x64 / Mono / Development |
| 解像度 | **1280×720** |
| シーン | キッチン1部屋、主人公1匹、固定の人間マーカー、食料目印3か所 |
| 描画設定 | Built-in、Low、影・AA・HDRなし、フレーム上限60 |
| 操作 | 自動歩行・ダッシュ、継続的なカメラ旋回 |
| 測定時間 | **30.010秒 / 1798フレーム** |
| 平均FPS | **59.91fps** |
| フレーム時間95％点 | **16.76ms** |
| 最大フレーム時間 | **19.18ms** |
| GC Allocated In Frame最大観測値 | 20484 bytes。ゼロGCではない |
| Unity Total Reserved Memory観測値 | 241478927 bytes（約230.3MiB）。OS全体やプロセスのWorking Setを示す値ではない |

起動後3秒のウォームアップと、PNG撮影中・直後0.5秒はサンプルから除外。フレーム時間はTime.unscaledDeltaTime、メモリはProfilerRecorderの値。CPU/GPU個別の実行時間やGPUメモリを測った結果ではない。

**この自動経路・1部屋・主人公1匹の30秒試験では、720p/30fpsの目標を上回った。** 手動の通常プレイ全般、長時間実行、後続フェーズでの人間AI・仲間・軍団視点を含む合格を保証するものではない。

最初に試した非表示ウィンドウ起動では黒いキャプチャになり、試験を中止した。その試行は性能結果に採用せず、通常表示で再試験。上表と同梱PNG/JSONは床タイル修正後の最終ビルドの結果のみ。

## 未実施・既知の制約

- 実際のWASD・Shift・マウス・Esc・フォーカス切替を人が操作する確認、および感度・見やすさのユーザー評価: **未実施**。
- 長時間の操作、全ての家具の角・隙間でのカメラ検証、最小／最大モデルサイズの実行確認: **未実施**。
- Unity ProfilerでのMain Thread / Render Thread / GPU詳細時間、ドローコール、長時間のGC・メモリ推移: **未実施**。
- モデルは簡易な2節脚の手続き動作。骨格アニメーション・厳密な生物学的歩行ではなく、二組の支持脚を見せるPhase 1の実装。
- 当たり判定は安全側のカプセル。触角先端は装飾として扱う。
- フォントはWindowsのOSフォントを利用。今回の実機では日本語を表示できたが、別環境での取得失敗時は警告とInspector指定で対応。
- HPは仮表示3/3。ダメージ・食料回収・納品・繁殖・人間AI・壁登り・Tab軍団視点は今回の実装範囲外。

ユーザーのUnity操作手順は [PHASE1_実装と検証.md](PHASE1_実装と検証.md)、変更一覧は [PHASE1_変更ファイル一覧.md](PHASE1_変更ファイル一覧.md) を参照。手動のPhase 1受け入れ確認後にPhase 2へ進む。
