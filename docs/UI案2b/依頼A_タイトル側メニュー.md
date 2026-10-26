# クラウド依頼A: タイトル側のメニューを案2bに作り替える

CLAUDE.md の §9（クラウドセッション）に従う。まず docs/UI案2b/README.md を読む。

## やること
Assets/_GokiTakeover/Scripts/UI/TitleMenu.cs の次の画面を案2bに作り替える。
- ステージ選択（StageSelect / StageCard / StatusRow）… docs/UI案2b/案/StageSelect.dc.html に合わせる（段ボールの壁、画鋲で留めた写真、巣の記録のレシート、選んだステージのメモ、赤ペンの丸、丸シールの「出撃」）。
- 設定（SettingsPage）・図鑑（CodexPage）… 案が無いので docs/UI案2b/案/Kit.dc.html の役割（レシート・紙テープ・丸シール・赤ペンの丸）で作る。

手本は作り替え済みのタイトル画面（TitleMenu.TitlePage、PaperKit.cs、docs/UI案2b/現状/title*.png）。今の画面は docs/UI案2b/現状/stage3-select-720p.png。

## 守ること
- 文言・表示する情報・キー操作・マウス操作・画面遷移は今と同じにする（見た目だけ変える）。
- ステージの写真は無いので、PaperKit で描ける代わり（ステージごとの色の紙、手描きの見取り図など）にする。
- 別のクラウドセッション（依頼B）が PhaseFiveHud.cs を同時に作り替える。PhaseFiveHud.cs と PaperKit.cs の既存部分は変えない。
- 新しい部品は新しいファイル Scripts/UI/PaperMenuParts.cs（static class PaperMenuParts）に置き、PaperKit の公開メソッドを使う。
- CodexViewer.cs / CodexModelPage.cs は必要なら変えてよい。
- 新しい文字は HudGlyphs.Phase5 に足す。

## 終わったら
docs/UI案2b/README.md の進み具合を更新し、作業ブランチに「WIP: 」付きでコミットして push する。手元で確認すること（見る画面、撮るスクリーンショット、未確認の点）を報告する。
