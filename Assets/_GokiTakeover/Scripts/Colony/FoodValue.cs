using UnityEngine;

namespace GokiTakeover
{
    /// <summary>Kind and colony value of a FoodItem (spec 5.1). Without it a food is a breadcrumb worth 1.</summary>
    [RequireComponent(typeof(FoodItem))]
    public sealed class FoodValue : MonoBehaviour
    {
        public enum Kind { Breadcrumb, Rice, Sweets, Leftovers, Garbage }
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
                default: return "パンくず";
            }
        }
        public static int AmountOf(FoodItem food) => food && food.TryGetComponent(out FoodValue v) ? v.Amount : 1;
        public static string NameOf(FoodItem food) => food && food.TryGetComponent(out FoodValue v) ? v.DisplayName : "パンくず";
    }
}
