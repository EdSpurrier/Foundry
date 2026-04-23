using Foundry.Common;
using Foundry.Data;
using UnityEngine;

namespace Foundry.Triggers
{
    public class RayTrigger3D : RayTriggerBase
    {
        protected override bool TryGetHit(out RaycastData hitData)
        {
            hitData = null;

            Vector3 origin = GetRayOrigin();
            Vector3 direction = GetRayDirection();

            if (!Physics.Raycast(origin, direction, out RaycastHit hit, rayDistance, detectionMask))
                return false;

            if (!detectionMask.Contains(hit.collider))
                return false;

            hitData = new RaycastData
            {
                source = gameObject,
                target = hit.collider != null ? hit.collider.gameObject : null,
                origin = origin,
                direction = direction,
                distance = hit.distance,
                point = hit.point,
                normal = hit.normal,
                collider3D = hit.collider,
                hit3D = hit,
                is2D = false
            };

            return hitData.IsValid;
        }
    }
}