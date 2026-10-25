using System.Collections.Generic;
using UnityEngine;

namespace GokiTakeover
{
    /// <summary>
    /// A route drawn in the tactics view (Phase 5): floor points the chosen allies run through in order, and what
    /// they do at its end (collect the food or take the zone it ends on; otherwise hide in the nearest cover).
    /// Shared by every ally sent along it.
    /// </summary>
    public sealed class AllyRoute
    {
        public readonly List<Vector3> Points = new List<Vector3>(64);
        public FoodItem Food;
        public int Zone = -1;
        public string EndLabel => Food ? "回収" : Zone >= 0 ? "占拠" : "隠れる";
    }
}
