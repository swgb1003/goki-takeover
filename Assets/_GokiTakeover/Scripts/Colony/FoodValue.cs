using UnityEngine;

namespace GokiTakeover
{
    /// <summary>Kind and colony value of a FoodItem (spec 5.1). Without it a food is a breadcrumb worth 1.</summary>
    [RequireComponent(typeof(FoodItem))]
    public sealed class FoodValue : MonoBehaviour
    {
        // Stage 3 (町中華, 2026-10-26) adds ten diner foods after the first five (their values are unchanged).
        public enum Kind { Breadcrumb, Rice, Sweets, Leftovers, Garbage, Tenkasu, Noodles, FriedRice, Gyoza, Karaage, SesameBall, DinerLeftovers, Flour, Chashu, Shumai }
        public const int FirstDinerKind = (int)Kind.Tenkasu;
        public static bool IsDiner(Kind kind) => (int)kind >= FirstDinerKind;
        [SerializeField] private Kind kind;
        public Kind FoodKind => kind;
        public int Amount => AmountOf(kind);
        public string DisplayName => NameOf(kind);
        public void Configure(Kind value) { kind = value; }

        public static int AmountOf(Kind kind)
        {
            switch (kind)
            {
                case Kind.Sweets: case Kind.Garbage: return 2;
                case Kind.Leftovers: return 3;
                case Kind.Gyoza: case Kind.Karaage: case Kind.SesameBall: return 2;
                case Kind.DinerLeftovers: return 3;
                case Kind.Chashu: return 4;
                case Kind.Shumai: return 5;
                default: return 1;
            }
        }
        public static string NameOf(Kind kind)
        {
            switch (kind)
            {
                case Kind.Rice: return "米粒";
                case Kind.Sweets: return "お菓子";
                case Kind.Leftovers: return "食べ残し";
                case Kind.Garbage: return "生ごみ";
                case Kind.Tenkasu: return "天かす";
                case Kind.Noodles: return "麺の切れ端";
                case Kind.FriedRice: return "チャーハン";
                case Kind.Gyoza: return "餃子";
                case Kind.Karaage: return "唐揚げ";
                case Kind.SesameBall: return "ごま団子";
                case Kind.DinerLeftovers: return "客の食べ残し";
                case Kind.Flour: return "小麦粉";
                case Kind.Chashu: return "チャーシュー";
                case Kind.Shumai: return "焼売のせいろ";
                default: return "パンくず";
            }
        }
        public static int AmountOf(FoodItem food) => food && food.TryGetComponent(out FoodValue v) ? v.Amount : 1;
        public static string NameOf(FoodItem food) => food && food.TryGetComponent(out FoodValue v) ? v.DisplayName : "パンくず";
    }
}
