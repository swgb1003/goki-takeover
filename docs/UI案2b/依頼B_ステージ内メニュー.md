# クラウド依頼B: ステージ内のメニュー画面を案2bに作り替える

CLAUDE.md の §9（クラウドセッション）に従う。まず docs/UI案2b/README.md を読む。

## やること
Assets/_GokiTakeover/Scripts/UI/PhaseFiveHud.cs の次の画面を案2bに作り替える（ステージ1〜3共通）。
- 出撃前説明（Briefing / Objective）… docs/UI案2b/案/Briefing.dc.html に合わせる（方眼の作戦ノート、人間の写真カード、危険の付箋、よく使うキーのテープ、丸シールの「出撃！」）。
- ポーズ（Pause）とその中の設定（SettingsPage）・図鑑（CodexPage）
- クリア（Clear / ResultRow）・失敗（Defeat）
- 巣の画面（NestScreen / UpgradeRow / ActionCard）

案が無い画面は docs/UI案2b/案/Kit.dc.html の役割（レシート＝目標・記録、紙テープ＝ボタン、丸シール＝いちばん大事な決定、赤ペンの丸＝選択中、橙のガムテープ＝危険だけ）で作る。
手本は作り替え済みのタイトル画面（TitleMenu.TitlePage、PaperKit.cs、docs/UI案2b/現状/title*.png）。今の画面は docs/UI案2b/現状/stage3-briefing-720p.png と stage3-clear-720p.png。

## 守ること
- 文言・表示する情報・キー操作・マウス操作・画面遷移・timeScale の扱いは今と同じにする（見た目だけ変える）。テストが探す文言やボタンを消さない（Scripts/Performance/*ScenarioTest.cs を grep して確かめる）。
- 人間の写真は無いので、PaperKit で描ける代わり（名前と道具を書いた紙のカードなど）にする。人数・名前はステージの定義から取る（ステージ1〜3で人数が違う）。
- プレイ中のHUD（Normal、MissionPanel、ReactionPanel、Tactics、Banner、GuidePanel、マーカー類）は後の依頼で作り替えるので、今回は変えない。
- 別のクラウドセッション（依頼A）が TitleMenu.cs を同時に作り替える。TitleMenu.cs・CodexViewer.cs・CodexModelPage.cs と PaperKit.cs の既存部分は変えない。
- 新しい部品は新しいファイル Scripts/UI/PaperHudParts.cs（static class PaperHudParts）に置き、PaperKit の公開メソッドを使う。
- 新しい文字は HudGlyphs.Phase5 に足す。

## 終わったら
docs/UI案2b/README.md の進み具合を更新し、作業ブランチに「WIP: 」付きでコミットして push する。手元で確認すること（見る画面、撮るスクリーンショット、未確認の点）を報告する。
