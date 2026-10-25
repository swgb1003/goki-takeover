using UnityEngine;

namespace GokiTakeover
{
    /// <summary>
    /// The final defence's professional: swaps the resident's skinned meshes (three LODs) for the exterminator's
    /// (white coverall, respirator, pump sprayer). Both were authored on the same skeleton, so the bones, the
    /// animation and the held slipper carry over unchanged. StageProgress shows it for the final defence.
    /// </summary>
    public sealed class ExterminatorLook : MonoBehaviour
    {
        [SerializeField] private SkinnedMeshRenderer[] renderers;
        [SerializeField] private Mesh[] residentMeshes, exterminatorMeshes;
        public bool Showing { get; private set; }

        public void Configure(SkinnedMeshRenderer[] skins, Mesh[] resident, Mesh[] exterminator)
        { renderers = skins; residentMeshes = resident; exterminatorMeshes = exterminator; }

        public void Show(bool exterminator)
        {
            Showing = exterminator;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i]) renderers[i].sharedMesh = exterminator ? exterminatorMeshes[i] : residentMeshes[i];
        }
    }
}
