# Phase 1 実装とUnity確認手順

Unity 6000.0.61f1 / Windows / Built-in / 標準Input Manager。
仕様・PNG6枚は保全し、3Dモデルは全てコード生成。参考PNGをゲーム背景に貼っていない。

## Phase 1との対応

| 項目 | 実装 |
|---|---|
| A 自動生成 | 楕円胴体、頭・胸、左右甲殻、控えめな黒い目2個、Hip→Knee→Toeの6脚、各4節の長い触角2本 |
| A 再利用 | ゴキブリは2種類の低ポリメッシュ、4種類の共有マテリアル。1体1780三角形。マテリアルのインスタンス化なし |
| A 調整 | CockroachAppearance.assetのSerializeFieldで色・大きさを編集して再生成。移動速度・周期・感度等もInspectorから調整 |
| B アニメーション | Idleで触角と胴体。Walkは左前＋右中＋左後／逆グループの交互支持。Runで周期と前傾を変更。実移動量に連動 |
| B 操作 | WASD、左右Shift、マウスのTPS。Escで操作解除、クリックで再開。解除中とフォーカス喪失中は移動停止 |
| B 衝突 | 重力、床接地、壁・机脚・椅子・収納の固定Collider。カメラはSphereCastで障害物の手前へ即時退避、離れたら補間して戻る |
| C キッチン | タイル床、四壁、机、椅子2脚、冷蔵庫、カウンター、棚。主人公1匹、人間の固定マーカー1体、食料目印3か所 |
| C 仮HUD | 左上に試験目標、右上にFPSとフレーム時間、右下にHP、最下部に日本語操作説明 |
| デザイン参照 | 03_ThirdPersonを中心に、茶系甲殻、巨大な家具、低い視点、暖色照明、黒茶パネルと黄緑・橙アクセントを簡略再現 |

## 具体的な操作手順

1. Unity Hubで「追加」→「ディスクからプロジェクトを追加」。このリポジトリのルート（Assets/Packages/ProjectSettingsのあるフォルダ）を選ぶ。
2. Editor **6000.0.61f1** で開き、インポート・コンパイルが終わるまで待つ。
3. Unityメニュー **Tools > GOKI TAKEOVER > Generate Cockroach** を選ぶ。
   - Projectウィンドウで `Assets/_GokiTakeover/Prefabs/Cockroach_Player.prefab` が選択される。
   - ダブルクリックでPrefab Modeへ入り、Visual配下のBodyPivot/Shells、Legs、Antennaeを確認できる。
   - もう一度メニューを実行しても同じPrefabを更新し、現在のシーンには重複配置しない。
4. **Tools > GOKI TAKEOVER > Generate Kitchen Test Scene** を選ぶ。
   - 同梱の `Assets/_GokiTakeover/Scenes/Phase1_Kitchen_Test.unity` を開く。
   - シーンをまだ生成していない場合だけ新規生成する。
5. Gameタブの解像度メニューから **1280×720** を選択。候補がなければ「＋」→Fixed Resolution→Width 1280 / Height 720。
6. 上部の **▶ Play** を押す。Game画面を左クリックして操作を開始する。
7. **W/A/S/D** で移動、**左または右Shift** を押しながら移動でダッシュ、**マウス移動** で視点を回す。
8. 壁、冷蔵庫、机の脚に向かって歩き、突き抜けないことを確認。机の中央下を通り抜け、壁際でカメラを回して視界を確認する。
9. 停止して触角の揺れ、歩行の交互脚、ダッシュで速い周期、停止後の脚姿勢を確認する。
10. **Esc** でカーソルを解放し移動停止。Game画面を**左クリック**して再開。今回はゲーム時間を止めるポーズメニューはない。
11. **▶ Play** をもう一度押して停止。**Window > General > Console** で赤いエラーがないか確認する。

`Tab`切替、`E`回収、人間AI、HP減少・死亡は後続フェーズ。今回は仮HP 3/3と目印のみで、未実装の操作をHUDには表示していない。

## 色・大きさと再生成

- Projectで `Assets/_GokiTakeover/Data/CockroachAppearance.asset` を選び、Inspectorで **Model Size / Body Color / Shell Color / Limb Color / Eye Color** を編集する。
- **Generate Cockroach** を実行するとモデルと共有マテリアルが更新される。シーンにあるPrefabインスタンスにも反映される。
- 初期サイズ0.45、許容範囲0.25〜0.65。Controllerも生成サイズに合わせる。極端なサイズや手動のTransform拡縮には追従カメラのDistance/Pivot Offsetも調整する。
- 家具を初期状態に戻す場合は **Tools > GOKI TAKEOVER > Rebuild Generated Kitchen Scene** を実行する。**この専用シーンで行った手編集は再生成される**。必要な変更は先に別名保存またはGitで保管する。
- 生成物ラベルなしの同名アセットには上書きせず、Consoleに保護ログを出す。
- 外観設定とスクリプトを原本として保管し、生成Prefab/メッシュ/マテリアル/シーンと各.metaもGitへ含める。

## Windowsビルド

1. Playを停止する。
2. **Tools > GOKI TAKEOVER > Build Windows 720p** を選ぶ。
3. Consoleにビルド成功が出たら、エクスプローラーで `Build/GokiTakeover_Phase1/GokiTakeover.exe` をダブルクリック。
4. 1280×720ウィンドウで起動する。Editorと同じ操作で30秒以上探索し、右上のFPSを確認する。
5. ウィンドウを閉じるか **Alt＋F4** で終了する。

ビルドはDevelopment / Windows x64 / Mono。専用シーンだけを指定し、グローバルなBuild Settingsのシーン一覧と一時変更したPlayerSettingsを維持・復元する。出力のBuildフォルダはGit除外。

## 自動検証の再実行

Unityを閉じた状態で、リポジトリのルートをPowerShellで開く。

```powershell
$unityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.0.61f1\Editor\Unity.exe'
$projectRoot = (Get-Location).Path
New-Item -ItemType Directory -Force Logs | Out-Null

# EditMode: 専用生成アセットを作り直す。手編集した生成物は先に保管する。
& $unityEditor -batchmode -nographics -projectPath $projectRoot `
  -executeMethod GokiTakeover.Editor.PhaseOneVerification.GenerateAndVerify `
  -logFile "$projectRoot\Logs\generation.log"

# PlayMode: Unity内の物理・アニメーション確認。終了するとUnity自身が終了する。
& $unityEditor -batchmode -nographics -projectPath $projectRoot `
  -executeMethod GokiTakeover.Editor.PhaseOneVerification.VerifyPlayMode `
  -logFile "$projectRoot\Logs\playmode.log"

# Windowsビルド
& $unityEditor -batchmode -nographics -quit -projectPath $projectRoot `
  -executeMethod GokiTakeover.Editor.PhaseOneBuild.Build `
  -logFile "$projectRoot\Logs\build.log"
```

PowerShellではGUI実行が完了前に戻る場合がある。直列で実行し、Unityプロセスの終了とログ末尾・結果txtを確認してから次を実行する。同じプロジェクトへ複数Editorを同時起動しない。

PlayModeだけならUnityメニュー **Tools > GOKI TAKEOVER > Verify Play Mode** でも実行できる。再現可能な移動入力で検証するため、試験中に手操作しない。

Developmentビルドでの自動計測:

```powershell
$projectRoot = (Get-Location).Path
& "$projectRoot\Build\GokiTakeover_Phase1\GokiTakeover.exe" `
  -screen-width 1280 -screen-height 720 -screen-fullscreen 0 `
  -goki-smoke-test -goki-output "$projectRoot\Logs" `
  -logFile "$projectRoot\Logs\player-smoke.log"
```

通常起動では自動試験コンポーネントは生成されない。明示オプション時だけ自動移動・旋回し、30秒ぶんの計測結果JSONとPNGを出力して終了する。`-batchmode` / `-nographics` はPlayer計測には付けない。

## 制限・未確認事項

- 1280×720の日本語・見た目・操作感は実際のPlayer画面で最終確認する。
- FPSは同一部屋・主人公1体の試験値として扱う。人間AI、仲間、軍団視点、複数部屋を追加した後の性能は別途測る。
- Colliderは安全側の丸いカプセル。触角の先端は装飾のため物理形状に含めない。複雑な段差・壁登り・狭すぎる通路は今回の対象外。
- Escは操作解除のみ。正式なポーズ、音、設定保存、回収・ダメージ・リトライは未実装。
- OSフォント依存。Windows以外への移植やフォント同梱は別途ライセンスを確認して行う。
- HUDは仮の標準IMGUI。将来の大規模HUDへ進む際は、日本語フォント込みのUI方式とGC負荷を実測して再検討する。

## Gitと次のPhase

`git status --short --untracked-files=all` で一覧を確認し、Git GUIの新規ファイル表示で内容を確認する。通常の `git diff` だけでは未追跡ファイルの内容は表示されない。

- 今回の追加: .gitignore、Assets/配下、Packages/、ProjectSettings/、調査・実装・検証ドキュメント。
- 開始時から未追跡のdocs・DesignReferences・READMEも、自動でコミットしていない。保管対象として確認する。
- Library、Temp、Logs、UserSettings、Buildは含めない。.metaは含める。
- ステージや共有データの手編集を保存してから再生成する。生成時にGUIDを維持する。

Phase 1の手動確認後、Phase 2として「食料3個の回収・持ち帰り・巣への納品→人間1体のRoutine/Chase/Searchと視認・遮蔽→攻撃予兆・スリッパと粘着トラップ→HP0とリトライ→3個納品で練習クリア」の順を提案する。受け入れ条件は、逃走しながら納品でき、失敗とやり直しが成立すること。
