using UnityEngine;
using UnityEngine.AI;

namespace GokiTakeover
{
    [DefaultExecutionOrder(-300)]
    public sealed class KitchenNavigation : MonoBehaviour
    {
        [SerializeField] private NavMeshData data;
        private NavMeshDataInstance instance;
        public void Configure(NavMeshData navigation) { data = navigation; }
        private void OnEnable() { if (data) instance = NavMesh.AddNavMeshData(data); }
        private void OnDisable() { if (instance.valid) instance.Remove(); }
    }
}
