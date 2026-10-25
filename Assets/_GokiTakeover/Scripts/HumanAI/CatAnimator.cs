using UnityEngine;

namespace GokiTakeover
{
    /// <summary>
    /// Procedural poses for the Blender cat (bones at rest have no rotation): a diagonal trot scaled by speed, a low
    /// creeping stalk with a twitching tail tip, the crouch with a wiggling rump, the stretched leap, sitting to groom
    /// and the curled-up nap with slow breathing. Reads PetCat; poses are blended so changes never snap.
    /// </summary>
    [DefaultExecutionOrder(60)]
    public sealed class CatAnimator : MonoBehaviour
    {
        [SerializeField] private PetCat cat;
        [SerializeField] private Transform pelvis, spine, chest, neck, head;
        [SerializeField] private Transform[] tail = new Transform[0];
        [SerializeField] private Transform[] upper = new Transform[4], lower = new Transform[4], paw = new Transform[4];   // FL, FR, HL, HR
        private Vector3 pelvisRest;
        private float phase, low, curl, crouch, leap, sit;

        public void Configure(PetCat owner, Transform[] bones)
        {
            cat = owner;
            Transform F(string n) => BlenderHumanAnimator.Find(bones, n);
            pelvis = F("Pelvis"); spine = F("Spine"); chest = F("Chest"); neck = F("Neck"); head = F("Head");
            tail = new[] { F("Tail1"), F("Tail2"), F("Tail3"), F("Tail4") };
            string[] legs = { "FL", "FR", "HL", "HR" };
            for (int i = 0; i < 4; i++) { upper[i] = F("Upper" + legs[i]); lower[i] = F("Lower" + legs[i]); paw[i] = F("Paw" + legs[i]); }
        }

        private void Awake() { if (pelvis) pelvisRest = pelvis.localPosition; }

        private void LateUpdate()
        {
            if (!cat || !pelvis || Time.deltaTime <= 0) return;
            float dt = Time.deltaTime;
            var state = cat.Current;
            float speed = cat.Velocity.magnitude, move = Mathf.Clamp01(speed / 1.2f);
            Blend(ref curl, state == PetCat.Phase.Nap, 2.5f, dt);
            Blend(ref low, state == PetCat.Phase.Stalk, 4f, dt);
            Blend(ref crouch, state == PetCat.Phase.Crouch, 8f, dt);
            Blend(ref leap, state == PetCat.Phase.Leap, 14f, dt);
            Blend(ref sit, state == PetCat.Phase.Recover, 4f, dt);
            phase += dt * Mathf.Lerp(4f, 10f, Mathf.Clamp01(speed / 2.4f));
            float t = Time.time;

            // Legs: a diagonal trot (FL with HR, FR with HL), folded by the stalk, crouch, sit and nap.
            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2;
                float offset = (i == 0 || i == 3) ? 0 : Mathf.PI;
                float swing = Mathf.Sin(phase + offset) * 26 * move;
                float knee = Mathf.Max(0, -Mathf.Sin(phase + offset)) * 34 * move;
                float u = -swing, l = front ? knee : -knee;
                u += front ? -18 * low - 30 * crouch - 70 * leap - 80 * curl + 10 * sit : 22 * low + 40 * crouch + 60 * leap + 70 * curl + 75 * sit;
                l += front ? 40 * low + 70 * crouch + 20 * leap + 120 * curl : -50 * low - 80 * crouch - 10 * leap - 130 * curl - 140 * sit;
                if (i == 0) u -= 70 * sit * Mathf.Clamp01(Mathf.Sin(t * 2.2f) * 2);   // grooming: the left paw goes up to the mouth
                upper[i].localRotation = Quaternion.Euler(u, 0, 0);
                lower[i].localRotation = Quaternion.Euler(l, 0, 0);
                paw[i].localRotation = Quaternion.Euler(front ? -l * 0.6f : -l * 0.4f, 0, 0);
            }
            // Body: bob in the trot, sink low to stalk, rump wiggle before the pounce, lie down to nap, sit back to groom.
            float sink = 0.28f * low + 0.38f * crouch + 0.6f * curl + 0.2f * sit;
            float breathe = curl * Mathf.Sin(t * 2.4f) * 0.015f;
            pelvis.localPosition = pelvisRest + Vector3.up * (Mathf.Abs(Mathf.Sin(phase)) * 0.03f * move - sink + breathe);
            pelvis.localRotation = Quaternion.Euler(-14 * leap + 10 * sit, Mathf.Sin(t * 17) * 9 * crouch, Mathf.Sin(phase) * 3 * move);
            spine.localRotation = Quaternion.Euler(-4 * low - 18 * sit, 0, 0);
            chest.localRotation = Quaternion.Euler(6 * low - 8 * leap - 22 * sit, 0, 0);
            // Head: watches what it hunts; tucked down asleep; bobbing while grooming.
            Quaternion look = Quaternion.identity;
            Vector3 toLook = cat.LookPoint - head.position;
            if (cat.Stalking && toLook.sqrMagnitude > 0.01f)
            {
                Vector3 local = Quaternion.Inverse(cat.transform.rotation) * toLook;
                look = Quaternion.Euler(Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -30, 40), Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -60, 60), 0);
            }
            neck.localRotation = Quaternion.Euler(18 * low + 10 * crouch + 40 * curl + 35 * sit * (0.6f + 0.4f * Mathf.Sin(t * 4.4f)), 0, 0);
            head.localRotation = look * Quaternion.Euler(20 * curl, 25 * curl, 10 * curl);
            // Tail: up and swaying when relaxed, low with a twitching tip on the hunt, wrapped round when asleep.
            for (int i = 0; i < tail.Length; i++)
            {
                float relaxed = Mathf.Sin(t * 1.6f + i * 0.7f) * (8 + i * 4);
                float twitch = i >= 2 ? Mathf.Sin(t * 14 + i) * 25 : 0;
                float x = (50 * (low + crouch) - 30 * leap) * (i == 0 ? 1 : 0.3f) + 30 * curl * (i == 0 ? 1 : 0);
                float y = relaxed * (1 - low - crouch) + twitch * (low + crouch) + 55 * curl;
                tail[i].localRotation = Quaternion.Euler(x, y, 0);
            }
        }

        private static void Blend(ref float value, bool on, float rate, float dt) => value = Mathf.MoveTowards(value, on ? 1 : 0, rate * dt);
    }
}
