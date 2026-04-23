using UnityEngine;

namespace Foundry.Data
{
    [System.Serializable]
    public class ImpactData
    {
        public GameObject source;
        public GameObject target;
        public float force;

        public Vector3 point;
        public Vector3 normal;

        public Collider collider3D;
        public Collision collision3D;

        public Collider2D collider2D;
        public Collision2D collision2D;

        public bool is2D;

        public bool IsValid => target != null;
    }
}
