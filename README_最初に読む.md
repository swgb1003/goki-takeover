# GOKI TAKEOVER — 開発パッケージ

## 最新版（随時更新）

- 起動: `Build/GokiTakeover/GokiTakeover.exe`。**更新のたびにこの1つを上書き**します（以前の `Build/GokiTakeover_v1.0` はここへ移動）。遊び方は同じフォルダの `遊び方.txt`（元は `docs/遊び方.txt`）。Dataフォルダ・DLLも一緒に保持してください。
- Unity: `Tools > GOKI TAKEOVER > Rebuild Phase 5 Takeover` でシーンを再生成、`Tools > GOKI TAKEOVER > Build Game (Build/GokiTakeover)` でビルド。
- 2026-10-24 更新: 人間が味方ゴキも見つけて追いかけ、スリッパで叩く（狙われた味方は逃げ出すのが少し遅れる）。調整値はステージ定義の `residentHuntsAllies`・`allyReactionSeconds`。

## 公開版 ver 1.0（2026-10-24、Phase 6）

- 現在は上の `Build/GokiTakeover` に統合。
- 以下の各Phaseの「起動」にある旧ビルドフォルダ（Phase3/4/5、BlenderModels、検証版、RenderProbe）は2026-10-24に削除済み。必要ならUnityの各メニューで再ビルドできます。
- Phase 5からの変更: 5 占拠（仲間を区域へ）、第1章チュートリアル、主人公1匹で開始・仲間は死ぬと戻らない（死体を食べて回復）、スリッパが台の上・低い壁まで届く、合成効果音。
- 詳細: [Phase 5 追加調整](docs/PHASE5_実装と検証.md#追加調整2026-10-24)、[Phase 6 実装と検証](docs/PHASE6_実装と検証.md)。

## Phase 5 占拠・ステージクリア版（2026-10-23）

- 起動: `Build/GokiTakeover_Phase5/GokiTakeover.exe`（タイトル画面から始まる）。隣のDataフォルダ・DLLも一緒に保持してください。
- Unity: `Tools > GOKI TAKEOVER > Generate Phase 5 Takeover`（`Assets/_GokiTakeover/Scenes/Phase5_Title.unity`）を開いてPlay。
- タイトル → ステージ選択 → ミッション説明 → 食料・縄張り・人間の恐怖の3ゲージを100%に → 最終防衛（燻煙剤を60秒耐える）→ 人間が退去してクリア・評価・次ステージ解放。
- 巣を閉じるたびに記録され、タイトルの「つづきから」で再開。やられても仲間がいれば後継に交代できる。
- 詳細: [Phase 5 実装と検証](docs/PHASE5_実装と検証.md)。

## Phase 4 育成・パニック版（2026-10-23）

- 起動: `Build/GokiTakeover_Phase4/GokiTakeover.exe`。隣のDataフォルダ・DLLも一緒に保持してください。
- Unity: `Tools > GOKI TAKEOVER > Generate Phase 4 Colony`（`Assets/_GokiTakeover/Scenes/Phase4_Kitchen_Colony.unity`）を開いてPlay。
- 巣の近くでEを押すと巣画面（食料で仲間を増やす・巣を拡張・強化）。仲間を9匹に増やすとクリア。
- 人間は怖がるほど「発見→警戒→パニック」と段階が上がり、毒餌・粘着シートを置き、殺虫スプレーを使う。Escでポーズ・設定・図鑑。
- 詳細: [Phase 4 実装と検証](docs/PHASE4_実装と検証.md)。

## Phase 3 壁登り・軍団モード版（2026-10-23）

- 起動: `Build/GokiTakeover_Phase3/GokiTakeover.exe`。隣のDataフォルダ・DLLも一緒に保持してください。
- Unity: `Tools > GOKI TAKEOVER > Generate Phase 3 Swarm`（`Assets/_GokiTakeover/Scenes/Phase3_Kitchen_Swarm.unity`）を開いてPlay。
- 壁へ進み続けると登れる（天井まで）。Spaceで降りる。Tabで軍団モード、1〜4で仲間に 回収／探索／防衛／一斉侵攻。
- 操作・検証結果・制限: [Phase 3 実装と検証](docs/PHASE3_実装と検証.md)。旧シーン・旧ビルドはそのまま残しています。

## Blenderモデル改善版（2026-10-23）

現在はUnityでプレイできるプロジェクトです。ゴキブリ・住人をBlenderで制作した軽量スキンモデルへ差し替えた版を追加しました。

- 起動: `Build/GokiTakeover_BlenderModels/GokiTakeover.exe`。隣のDataフォルダ・DLLも一緒に保持してください。
- Unity: `Assets/_GokiTakeover/Scenes/Phase2_Kitchen_BlenderModels.unity` を開いてPlay。
- 比較画像・負荷測定・編集方法: [Blenderモデル改善と検証](docs/Blender_モデル改善と検証.md)。
- 編集元: `ArtSource/Blender/GokiTakeover_Characters.blend`。ゲームの起動にBlenderは不要です。
- 旧Phase1/Phase2シーン・旧モデルは残しています。以下は当初の配布パッケージの案内です。

## 同梱ファイル

- `GOKI_TAKEOVER_統合開発仕様書_v1.0.md` — 全体仕様・機能・画面・軽量化・開発フェーズ・テスト。
- `GOKI_TAKEOVER_開発開始プロンプト.md` — Codex/Claude Codeに最初に貼る文章。
- `DesignReferences/01_Title.png` — タイトル画面の参考。
- `DesignReferences/02_StageSelect.png` — ステージ選択の参考。
- `DesignReferences/03_ThirdPerson.png` — 三人称操作画面の参考。
- `DesignReferences/04_SwarmTactics.png` — 軍団指揮画面の参考。
- `DesignReferences/05_NestUpgrade.png` — 巣・強化画面の参考。
- `DesignReferences/06_StageClear.png` — クリア画面の参考。

## 開発を始める手順

1. ZIPを展開する。
2. **Unityプロジェクトのルート**に `Docs` フォルダを作り、このパッケージのMDファイル2本と `DesignReferences` を `Docs/` にコピーする（既存のファイルがある場合、上書きせず中身を確認）。
3. Codex/Claude Codeで**Unityプロジェクトのルートフォルダを開く**。
4. `Docs/GOKI_TAKEOVER_開発開始プロンプト.md` の「ここからコピー」〜「ここまでコピー」をエージェントに入力する。
5. まず **Phase 0（調査）・Phase 1（キャラ自動生成・移動・軽量キッチン）だけ** 作る。
6. Unityで動作を実測し、問題が解決してからPhase 2へ進む。

## 注意事項

- 画像はデザイン資料であり、Unityで動く3Dモデルではありません。
- 今のPCで画像のような高精細な光・反射が動くことを保証するものではありません。
- 有料の3D生成AIは使用していません。初期モデルはUnity内のコード生成、今回の改善モデルはBlenderで制作しています。
- 当初のZIPは仕様・指示・参考画像のパッケージでした。現在のフォルダには実装済みUnityプロジェクトとWindowsビルドもあります。
