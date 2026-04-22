using UnityEngine;

namespace Foundry.Common
{
    public static class LayerMaskExtensions
    {
        public static bool ContainsLayer(this LayerMask mask, int layer)
        {
            return (mask.value & (1 << layer)) != 0;
        }

        public static bool Contains(this LayerMask mask, GameObject target)
        {
            return target != null && mask.ContainsLayer(target.layer);
        }

        public static bool Contains(this LayerMask mask, Component target)
        {
            return target != null && mask.ContainsLayer(target.gameObject.layer);
        }
    }
}