using UnityEngine;

namespace Foundry.Data
{
    [System.Serializable]
    public class RaycastData
    {
        public GameObject source;
        public GameObject target;

        public Vector3 origin;
        public Vector3 direction;
        public float distance;

        public Vector3 point;
        public Vector3 normal;

        public Collider collider3D;
        public RaycastHit hit3D;

        public Collider2D collider2D;
        public RaycastHit2D hit2D;

        public bool is2D;

        public bool IsValid => target != null;
    }
}