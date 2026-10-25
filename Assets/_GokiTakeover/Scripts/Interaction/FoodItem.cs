using UnityEngine;

namespace GokiTakeover
{
    public sealed class FoodItem : MonoBehaviour
    {
        public enum FoodState { Available, Carried, Delivered }
        public FoodState State { get; private set; }
        private Transform home;
        private Vector3 homePosition;
        private Quaternion homeRotation;
        private void Awake()
        {
            home = transform.parent;
            homePosition = transform.localPosition;
            homeRotation = transform.localRotation;
        }
        public bool TryCarry(Transform anchor)
        {
            if (State != FoodState.Available || !anchor) return false;
            State = FoodState.Carried;
            transform.SetParent(anchor, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            return true;
        }
        public void Deliver()
        {
            if (State != FoodState.Carried) return;
            State = FoodState.Delivered;
            gameObject.SetActive(false);
        }
        public void ResetItem()
        {
            State = FoodState.Available;
            transform.SetParent(home, false);
            transform.SetLocalPositionAndRotation(homePosition, homeRotation);
            gameObject.SetActive(true);
        }
    }
}
