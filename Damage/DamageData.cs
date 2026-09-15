using UnityEngine;

namespace Foundry.Damage
{
    public enum DamageType
    {
        Generic,
        Bullet,
        Explosion,
        Impact,
        Fire
    }

    [System.Serializable]
    public class DamageData
    {
        public int amount;
        public DamageType damageType = DamageType.Generic;

        public GameObject source;
        public GameObject instigator;
        public GameObject target;

        public Vector3 point;
        public Vector3 direction;
        public Vector3 normal;

        public float force;
    }
}