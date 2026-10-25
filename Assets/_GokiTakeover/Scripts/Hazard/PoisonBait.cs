using UnityEngine;

namespace GokiTakeover
{
    /// <summary>
    /// Looks like food. Taking it (E) costs one HP and uses it up. Allies sniff it out: once an ally passes
    /// close by, or its area is scouted again, it is identified and marked as a hazard.
    /// The small teal pellets on it are the visual tell for an attentive player.
    /// </summary>
    public sealed class PoisonBait : HazardBase
    {
        [SerializeField] private GameObject identifiedMarker;
        public bool Identified { get; private set; }
        public int ScoutIndex { get; set; } = -1;
        public override string DisplayName => Identified ? "毒餌" : "食料？";
        public override string Warning => "毒餌！  拾うと生命力が減る";
        public override string CodexId => "bait";
        public void Configure(GameObject marker) { identifiedMarker = marker; }
        public override bool Contains(Vector3 position)
        {
            Vector3 d = position - transform.position; d.y = 0;
            return IsArmed && d.sqrMagnitude < 0.85f * 0.85f;
        }
        public void Arm(Vector3 position, int scoutIndex)
        {
            transform.position = position;
            ScoutIndex = scoutIndex;
            Identified = false;
            gameObject.SetActive(true);
            if (identifiedMarker) identifiedMarker.SetActive(false);
        }
        public void Identify()
        {
            Identified = true;
            if (identifiedMarker) identifiedMarker.SetActive(true);
        }
        public void Disarm() { gameObject.SetActive(false); Identified = false; }
    }
}
