using UnityEngine;

namespace GokiTakeover
{
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private PhaseTwoSession session;
        [SerializeField] private FoodItem[] foods;
        [SerializeField] private Transform carryAnchor;
        public FoodItem Carried { get; private set; }
        public void Configure(PhaseTwoSession game, FoodItem[] items, Transform anchor)
        { session = game; foods = items; carryAnchor = anchor; }
        private bool Reachable(Vector3 point, float range)
        {
            Vector3 origin = transform.position + Vector3.up * 0.15f;
            Vector3 target = point + Vector3.up * 0.15f;
            return Vector3.Distance(origin, target) <= range &&
                !Physics.Linecast(origin, target, ~(1 << 2), QueryTriggerInteraction.Ignore);
        }
        public bool AtNest => session && !session.NestSmoked && (session.HasNestArea ? session.InNest(transform.position) : Reachable(session.Nest.position, 1.15f));
        public FoodItem NearbyFood
        {
            get
            {
                FoodItem nearest = null;
                float range = session.Settings.interactionRange;
                foreach (var food in foods)
                    if (food && food.State == FoodItem.FoodState.Available && Reachable(food.transform.position, range))
                    { nearest = food; range = Vector3.Distance(transform.position, food.transform.position); }
                return nearest;
            }
        }
        private void Update()
        {
            if (session.CanControl && Input.GetKeyDown(KeyCode.E)) TryInteract();
        }
        public bool TryInteract()
        {
            if (!session.IsRunning) return false;
            if (Carried)
            {
                if (!AtNest) return false;
                Carried.Deliver();
                Carried = null;
                session.RecordDelivery();
                return true;
            }
            var food = NearbyFood;
            if (!food || !food.TryCarry(carryAnchor)) return false;
            Carried = food;
            GetComponentInChildren<CockroachVisualController>().PlayPickup();
            return true;
        }
        public void ResetInventory()
        {
            Carried = null;
            foreach (var food in foods) food.ResetItem();
        }
    }
}
