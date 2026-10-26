using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using P = GokiTakeover.Editor.PolishedAssets;

namespace GokiTakeover.Editor
{
    /// <summary>
    /// Stage 3 step 2 (2026-10-26): the staff. The cook gets his hot water (HotWaterAttack) and a ladle that swats like
    /// the slipper; the landlady (おかみさん, new Blender model) swats a wide box with her tray and sprays alcohol
    /// (AlcoholSprayAttack); the part-timer (バイト, new Blender model) mops the floor wet (MopDuty, WetFloor) and sweeps
    /// roaches away with his deck brush (DeckBrushAttack). Both are copies of the cook's body and AI.
    /// </summary>
    public static partial class StageThreeGenerator
    {
        public static readonly Vector3 OkamiStart = new Vector3(19.0f, 0, -1.6f), BaitoStart = new Vector3(2.5f, 0, 12.4f);
        public const int WetPatches = 20;
        private static readonly Vector3[] OkamiStops =
        {
            new Vector3(13.5f, 0, -1.5f), new Vector3(19.0f, 0, -1.6f), new Vector3(24.5f, 0, 1.8f), new Vector3(14.6f, 0, 3.0f), new Vector3(12.6f, 0, -4.6f),
            new Vector3(24.0f, 0, -3.4f), new Vector3(6.0f, 0, 0.5f), new Vector3(50.0f, 0, 0.4f), new Vector3(56.0f, 0, -3.5f), new Vector3(47.0f, 0, -4.0f)
        };
        private static readonly Vector3[] BaitoStops =
        {
            new Vector3(-2.0f, 0, 11.5f), new Vector3(2.5f, 0, 12.6f), new Vector3(-4.5f, 0, 9.0f), new Vector3(9.5f, 0, 10.5f), new Vector3(12.0f, 0, 8.6f),
            new Vector3(3.5f, 0, 2.5f), new Vector3(-4.5f, 0, 2.4f), new Vector3(0.5f, 0, -3.8f), new Vector3(50.0f, 0, 11.5f), new Vector3(53.0f, 0, 13.0f), new Vector3(47.5f, 0, 13.4f)
        };

        private static void BuildStaff(Transform stageRoot, PhaseTwoSession session, SwarmCommander commander, HumanAIController chef)
        {
            var warning = AssetDatabase.LoadAssetAtPath<Material>(GeneratedAssets.Root + "/Art/Materials/Phase2_Warning.mat");
            var fanMaterial = AssetDatabase.LoadAssetAtPath<Material>(GeneratedAssets.Root + "/Art/Materials/Stage02_VacuumFan.mat");
            if (!warning || !fanMaterial) throw new InvalidOperationException("予告の材質がありません（ステージ2を先に生成）");
            var fx = GeneratedAssets.Empty("DinerAttacks", stageRoot, Vector3.zero);

            // Copies of the cook first (before he gets his own attack), then each one's own look and tools.
            var okami = CloneStaff(chef, "Human_Okami", OkamiStart, 160, "Okami");
            var baito = CloneStaff(chef, "Human_Baito", BaitoStart, 200, "Baito");
            chef.ConfigureWeapon("お玉", 0, 0, false);   // the ladle: a slipper's footprint
            okami.ConfigureMember("おかみさん", true, 3.45f, 1.05f);
            okami.ConfigureWeapon("お盆", 1.0f, 0.22f, true);   // a wide box: 2.56 across, 1.0 along with the margin
            baito.ConfigureMember("バイト", false, 3.6f, 1.15f);
            foreach (var h in new[] { chef, okami, baito }) EditorUtility.SetDirty(h);
            SetPatrol(okami, OkamiStops, "OkamiStop_");
            SetPatrol(baito, BaitoStops, "BaitoStop_");
            session.ConfigureFamily(new[] { okami, baito });
            EditorUtility.SetDirty(session);

            // The landlady's tray strikes a box: her warning is an orange rectangle instead of the slipper's ring.
            var trayLink = new SerializedObject(okami);
            var ring = (Transform)trayLink.FindProperty("warning").objectReferenceValue;
            var trayBox = RectWarning("TrayWarning", ring.parent, fanMaterial, warning);
            UnityEngine.Object.DestroyImmediate(ring.gameObject);
            trayLink.FindProperty("warning").objectReferenceValue = trayBox.transform;
            trayLink.ApplyModifiedPropertiesWithoutUndo();
            trayBox.gameObject.SetActive(false);

            // The cook's hot water: an orange strip, then the water running down it.
            var hot = chef.gameObject.AddComponent<HotWaterAttack>();
            hot.Configure(session, commander, chef, chef.GetComponentInChildren<BlenderHumanAnimator>(), "chef");
            var water = P.Part("HotWater", fx, P.BoxMesh, Translucent("Stage03_HotWater", new Color(0.55f, 0.75f, 0.95f, 0.6f), new Color(0.12f, 0.2f, 0.3f)), Vector3.zero, Vector3.one, false, false);
            water.GetComponent<MeshRenderer>().receiveShadows = false;
            water.gameObject.SetActive(false);
            hot.ConfigureVisuals(RectWarning("HotWaterWarning", fx, fanMaterial, warning), water);

            // The landlady's alcohol spray: the vacuum's fan on the floor, a white mist cone.
            var spray = okami.gameObject.AddComponent<AlcoholSprayAttack>();
            spray.Configure(session, commander, okami, okami.GetComponentInChildren<BlenderHumanAnimator>(), "okami");
            var fan = P.Part("SprayFan", fx, AssetDatabase.LoadAssetAtPath<Mesh>(StageTwoGenerator.FanPath), fanMaterial, Vector3.zero, Vector3.one, false, false);
            P.Part("Edge", fan, AssetDatabase.LoadAssetAtPath<Mesh>(StageTwoGenerator.FanEdgePath), warning, new Vector3(0, 0.004f, 0), Vector3.one, false, false).GetComponent<MeshRenderer>().receiveShadows = false;
            var mist = P.Part("AlcoholMist", fx, AssetDatabase.LoadAssetAtPath<Mesh>(PhaseFiveGenerator.ConePath), Translucent("Stage03_AlcoholMist", new Color(0.92f, 0.96f, 1f, 0.32f), new Color(0.2f, 0.22f, 0.25f)), Vector3.zero, Vector3.one, false, false);
            foreach (var t in new[] { fan, mist }) { t.GetComponent<MeshRenderer>().receiveShadows = false; t.gameObject.SetActive(false); }
            spray.ConfigureVisuals(fan, mist);

            // The part-timer: the deck brush's box, the mop and the wet floor it leaves.
            var brush = baito.gameObject.AddComponent<DeckBrushAttack>();
            brush.Configure(session, commander, baito, baito.GetComponentInChildren<BlenderHumanAnimator>(), "baito");
            brush.ConfigureVisuals(RectWarning("BrushWarning", fx, fanMaterial, warning));
            var wet = stageRoot.gameObject.AddComponent<WetFloor>();
            wet.Configure(session, WetPatchPool(fx));
            baito.gameObject.AddComponent<MopDuty>().Configure(session, baito, baito.GetComponentInChildren<BlenderHumanAnimator>(), wet, brush);
            foreach (var t in fx.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;   // never in the way of sight lines
        }

        /// <summary>A copy of the cook (skeleton, animator, AI) wearing another actor's meshes, with its own slipper ring.</summary>
        private static HumanAIController CloneStaff(HumanAIController chef, string name, Vector3 at, float yaw, string actor)
        {
            var copy = UnityEngine.Object.Instantiate(chef.gameObject, chef.transform.parent);
            copy.name = name;
            copy.transform.SetPositionAndRotation(at, Quaternion.Euler(0, yaw, 0));
            var can = copy.transform.Find("SprayCan");
            if (can) UnityEngine.Object.DestroyImmediate(can.gameObject);   // the insecticide stays the cook's
            var chefMeshes = FamilyModelIntegration.Import("Chef");
            var meshes = FamilyModelIntegration.Import(actor);
            var skins = copy.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => Array.IndexOf(chefMeshes, r.sharedMesh) >= 0).ToArray();
            if (skins.Length != 3) throw new InvalidOperationException(name + ": 店主のスキン（3段階）が見つかりません");
            foreach (var r in skins) { r.sharedMesh = meshes[Array.IndexOf(chefMeshes, r.sharedMesh)]; EditorUtility.SetDirty(r); }
            var member = copy.GetComponent<HumanAIController>();
            var link = new SerializedObject(member);
            var ring = (Transform)link.FindProperty("warning").objectReferenceValue;
            var own = UnityEngine.Object.Instantiate(ring.gameObject, ring.parent);
            own.name = "SlipperAttackWarning_" + actor;
            own.SetActive(false);
            link.FindProperty("warning").objectReferenceValue = own.transform;
            link.FindProperty("patrolPoints").arraySize = 0;   // its own stops follow (never move the cook's)
            link.ApplyModifiedPropertiesWithoutUndo();
            var animator = copy.GetComponentInChildren<BlenderHumanAnimator>();
            var leftHand = (Transform)new SerializedObject(animator).FindProperty("leftHand").objectReferenceValue;
            animator.ConfigureLance(leftHand.InverseTransformPoint(skins[0].transform.parent.TransformPoint(FamilyModelIntegration.ToolTip(actor))));
            EditorUtility.SetDirty(animator);
            return member;
        }

        /// <summary>An orange rectangle (translucent fill, solid outline) for RectTelegraph; inactive, no colliders or shadows.</summary>
        private static RectTelegraph RectWarning(string name, Transform parent, Material fill, Material edge)
        {
            var root = GeneratedAssets.Empty(name, parent, Vector3.zero);
            var body = P.Part("Fill", root, P.BoxMesh, fill, Vector3.zero, new Vector3(1, 0.01f, 1), false, false);
            var edges = new Transform[4];
            for (int i = 0; i < 4; i++) edges[i] = P.Part("Edge", root, P.BoxMesh, edge, Vector3.zero, Vector3.one, false, false);
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true)) r.receiveShadows = false;
            var telegraph = root.gameObject.AddComponent<RectTelegraph>();
            telegraph.Configure(body, edges);
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
            root.gameObject.SetActive(false);
            return telegraph;
        }

        /// <summary>The wet patches: a glossy blue disc with a light edge each, WetFloor.Radius round.</summary>
        private static Transform[] WetPatchPool(Transform fx)
        {
            var disc = AssetDatabase.LoadAssetAtPath<Mesh>(PhaseFiveGenerator.DiscPath);
            var ringMesh = AssetDatabase.LoadAssetAtPath<Mesh>(GeneratedAssets.Root + "/Art/Meshes/Phase2_Ring.asset");
            var water = Translucent("Stage03_Wet", new Color(0.30f, 0.50f, 0.78f, 0.45f), new Color(0.04f, 0.08f, 0.14f));
            water.SetFloat("_Glossiness", 0.95f);
            var rim = GeneratedAssets.Material("Stage03_WetEdge", new Color(0.36f, 0.62f, 0.9f));
            rim.EnableKeyword("_EMISSION"); rim.SetColor("_EmissionColor", new Color(0.05f, 0.1f, 0.16f));
            var root = GeneratedAssets.Empty("WetFloor", fx, Vector3.zero);
            var pool = new Transform[WetPatches];
            float d = WetFloor.Radius * 2;
            for (int i = 0; i < pool.Length; i++)
            {
                var patch = GeneratedAssets.Empty("WetPatch_" + i, root, Vector3.zero);
                P.Part("Water", patch, disc, water, Vector3.zero, new Vector3(d, 1, d), false, false).GetComponent<MeshRenderer>().receiveShadows = false;
                P.Part("Edge", patch, ringMesh, rim, new Vector3(0, 0.002f, 0), new Vector3(d, 1, d), false, false).GetComponent<MeshRenderer>().receiveShadows = false;
                patch.gameObject.SetActive(false);
                pool[i] = patch;
            }
            return pool;
        }

        private static Material Translucent(string name, Color color, Color emission)
        {
            var m = GeneratedAssets.Material(name, color);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_Mode", 2);
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON"); m.EnableKeyword("_ALPHABLEND_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission);
            m.renderQueue = (int)RenderQueue.Transparent;
            m.enableInstancing = false;
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
