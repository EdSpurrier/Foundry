using Foundry.Common;
using Foundry.Data;
using UnityEngine;

namespace Foundry.Triggers
{
    public class RayTrigger2D : RayTriggerBase
    {
        protected override bool TryGetHit(out RaycastData hitData)
        {
            hitData = null;

            Vector3 origin3D = GetRayOrigin();
            Vector3 direction3D = GetRayDirection();

            Vector2 origin = origin3D;
            Vector2 direction = ((Vector2)direction3D).normalized;

            RaycastHit2D hit = Physics2D.Raycast(origin, direction, rayDistance, detectionMask);

            if (!hit)
                return false;

            if (!detectionMask.Contains(hit.collider))
                return false;

            hitData = new RaycastData
            {
                source = gameObject,
                target = hit.collider != null ? hit.collider.gameObject : null,
                origin = origin3D,
                direction = direction3D.normalized,
                distance = hit.distance,
                point = hit.point,
                normal = hit.normal,
                collider2D = hit.collider,
                hit2D = hit,
                is2D = true
            };

            return hitData.IsValid;
        }
    }
}