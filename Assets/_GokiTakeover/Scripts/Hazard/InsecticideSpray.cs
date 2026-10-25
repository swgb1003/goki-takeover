using UnityEngine;

namespace GokiTakeover
{
    /// <summary>
    /// The resident's spray: a fan in front of the nozzle. The fan is shown on the floor in the warning
    /// colour during the windup, then as mist while it hurts. Furniture between nozzle and target blocks it.
    /// </summary>
    public sealed class InsecticideSpray : HazardBase
    {
        [SerializeField] private MeshRenderer fan;
        [SerializeField] private Material warningMaterial, mistMaterial;
        [SerializeField] private GameObject can;
        private Vector3 nozzle, direction = Vector3.forward;
        private float range = 3.8f, halfAngle = 24f;
        public bool Spraying { get; private set; }
        public bool Telegraphing { get; private set; }
        public Vector3 Nozzle => nozzle;
        public Vector3 Direction => direction;
        public override string DisplayName => "殺虫スプレー";
        public override string Warning => "殺虫スプレー！  物陰に隠れるか範囲の外へ";
        public override string CodexId => "spray";
        public override bool IsArmed => Spraying;

        public void Configure(MeshRenderer floorFan, Material warning, Material mist, GameObject sprayCan)
        { fan = floorFan; warningMaterial = warning; mistMaterial = mist; can = sprayCan; }

        public void Telegraph(Vector3 origin, Vector3 flatDirection, float length, float angle)
        {
            nozzle = origin; direction = flatDirection.normalized; range = length; halfAngle = angle;
            Telegraphing = true; Spraying = false;
            fan.sharedMaterial = warningMaterial;
            fan.transform.SetPositionAndRotation(new Vector3(origin.x, 0.03f, origin.z), Quaternion.LookRotation(direction));
            fan.transform.localScale = new Vector3(length, 1, length);
            fan.gameObject.SetActive(true);
            if (can) can.SetActive(true);
        }
        public void Fire() { Telegraphing = false; Spraying = true; fan.sharedMaterial = mistMaterial; }
        public void Stop()
        {
            Telegraphing = Spraying = false;
            if (fan) fan.gameObject.SetActive(false);
            if (can) can.SetActive(false);
        }

        /// <summary>Inside the fan (flat angle and range), regardless of cover.</summary>
        public override bool Contains(Vector3 position) => InFan(position);
        public bool InFan(Vector3 position)
        {
            Vector3 d = position - nozzle; d.y = 0;
            return d.magnitude <= range && Vector3.Angle(direction, d) <= halfAngle;
        }
        /// <summary>Inside the fan and nothing solid between the nozzle and the target.</summary>
        public bool Hits(Vector3 target, Transform ignore)
        {
            if (!InFan(target)) return false;
            if (!Physics.Linecast(nozzle, target, out RaycastHit hit, ~(1 << 2), QueryTriggerInteraction.Ignore)) return true;
            return ignore && hit.transform.IsChildOf(ignore);
        }
    }
}
