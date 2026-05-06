using Foundry.Data;
using Foundry.Triggers;
using FrameCoreU.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Examples.Scripts
{
    public class BreakableObject : MonoBehaviour, IImpactReceiver
    {
        [SerializeField] private float breakForce = 10f;

        [Title("Events")]
        [SerializeField] protected FrameCoreEvent onBreak;

        public void OnImpact(ImpactData impactData)
        {
            if (impactData == null)
                return;

            Debug.Log(
                $"{name} impacted | Force: {impactData.force} | " +
                $"Source: {(impactData.source != null ? impactData.source.name : "None")} | " +
                $"Point: {impactData.point} | Is2D: {impactData.is2D}"
            );

            if (impactData.force < breakForce)
                return;

            Debug.Log($"{name} broke from impact!");

            onBreak?.Activate();
        }
    }
}