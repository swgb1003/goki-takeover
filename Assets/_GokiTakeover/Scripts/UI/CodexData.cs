namespace GokiTakeover
{
    /// <summary>Encyclopedia pages shared by the Phase 5 title and stage HUD. A null id is known from the start.</summary>
    public static class CodexData
    {
        public struct Entry { public string id, name, body; public Entry(string i, string n, string b) { id = i; name = n; body = b; } }
        public static readonly string[] Tabs = { "ゴキブリ", "人間", "食料", "駆除グッズ" };
        public static readonly Entry[][] Entries =
        {
            new[] { new Entry(null, "主人公", "壁へ進み続けると張りつき、天井まで歩ける。Space で離れる。高い壁や天井ならスリッパが届かない。"),
                    new Entry(null, "仲間", "1 回収・2 探索・3 防衛・4 一斉侵攻・5 占拠・F 呼ぶ。1回目のスリッパはひっくり返るだけ、2回目でやられる。"),
                    new Entry(null, "巣", "E で開く。食料で仲間を産み、巣を広げ、強化する。閉じると記録される。周りは安全地帯。") },
            new[] { new Entry("resident", "住人", "反応は 平和 → 発見 → 警戒（罠や毒餌）→ パニック（殺虫スプレー）。発見・仲間・食料の被害・占領で恐怖度が上がる。"),
                    new Entry("fogger", "駆除業者", "3つのゲージが満タンになると呼ばれる最終防衛。燻煙剤を次々に焚く。耐えきれば人間は部屋を出ていく。") },
            new[] { new Entry(null, "パンくず  +1", "いちばん多い食料。"), new Entry("food_Rice", "米粒  +1", "小さく拾いやすい。"),
                    new Entry("food_Sweets", "お菓子  +2", "見つかりやすい場所にあることが多い。"), new Entry("food_Leftovers", "食べ残し  +3", "大きくて価値が高い。"),
                    new Entry("food_Garbage", "生ごみ  +2", "人間の動線の近くに出やすい。") },
            new[] { new Entry("slipper", "スリッパ", "橙の枠が出たら着弾前に外へ。台の上や低い壁も叩かれる。家具の下・高い壁・天井には届かない。"),
                    new Entry("sticky", "粘着シート", "踏むと大きく減速。人間が警戒すると増える。仲間は避けて歩く。"),
                    new Entry("spray", "殺虫スプレー", "パニック中の人間が使う。橙の扇の中で浴びるとダメージ。家具や壁の陰なら防げる。"),
                    new Entry("bait", "毒餌", "食料に似ているが拾うと生命力が減る。青緑の粒が目印。仲間の探索で見破れる。"),
                    new Entry("fogger", "燻煙剤", "橙の輪が出てから煙が広がる。煙は低く、天板・高い壁には届かない。業者は巣も燻すので予告が出たら外へ。") },
        };
    }
}
