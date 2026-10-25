using System;
using UnityEngine;

namespace GokiTakeover
{
    // The existing motor/IK/reactions remain authoritative. Only the rendered skin changes.
    [DefaultExecutionOrder(50)]
    public sealed class BlenderRoachAnimator : MonoBehaviour
    {
        [Serializable] public struct LegBinding
        {
            public Transform upper, lower, hipSource, kneeSource, toeSource;
            public Vector3 upperRestDirection, lowerRestDirection;
        }
        [SerializeField] private Transform skinSpace, body, bodySource;
        [SerializeField] private Transform[] antennaBones, antennaSources;
        [SerializeField] private Vector3[] antennaOffsets;
        [SerializeField] private LegBinding[] legs;
        private Vector3 sourceScaleRest=Vector3.one;
        private void Awake(){if(bodySource)sourceScaleRest=bodySource.localScale;}

        public void Configure(Transform space, Transform bodyBone, Transform originalBody,
            Transform[] antennae, Transform[] sources, LegBinding[] bindings)
        {
            skinSpace=space;body=bodyBone;bodySource=originalBody;
            antennaBones=antennae;antennaSources=sources;legs=bindings;
            antennaOffsets=new Vector3[antennae.Length];
            for(int i=0;i<antennae.Length;i++) antennaOffsets[i]=space.InverseTransformPoint(antennae[i].position)-space.InverseTransformPoint(sources[i].position);
        }
        private void LateUpdate()
        {
            if(!body || !bodySource) return;
            body.SetPositionAndRotation(bodySource.position,bodySource.rotation);
            // Squash/stretch reactions are authored as scale on the original body pivot.
            var k=bodySource.localScale;body.localScale=new Vector3(k.x/sourceScaleRest.x,k.y/sourceScaleRest.y,k.z/sourceScaleRest.z);
            for(int i=0;i<legs.Length;i++)
            {
                var l=legs[i];
                Vector3 up=l.kneeSource.position-l.hipSource.position;
                Vector3 low=l.toeSource.position-l.kneeSource.position;
                l.upper.SetPositionAndRotation(l.hipSource.position,
                    Quaternion.FromToRotation(skinSpace.TransformDirection(l.upperRestDirection),up)*skinSpace.rotation);
                l.lower.SetPositionAndRotation(l.kneeSource.position,
                    Quaternion.FromToRotation(skinSpace.TransformDirection(l.lowerRestDirection),low)*skinSpace.rotation);
            }
            for(int i=0;i<antennaBones.Length;i++)
                antennaBones[i].SetPositionAndRotation(antennaSources[i].position+skinSpace.TransformVector(antennaOffsets[i]),antennaSources[i].rotation);
        }
    }
}
