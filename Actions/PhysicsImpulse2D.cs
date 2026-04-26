using FrameCoreU.Events;
using Foundry.PhysicsSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Actions
{
    [System.Serializable]
    public class PhysicsImpulse2DAction : FrameAction
    {
        public override string ActionType => "Physics Impulse 2D";

        [Title("Physics")]
        [SerializeField] private PhysicsImpulse2D physicsImpulse;

        protected override void Activate()
        {
            if (physicsImpulse == null)
            {
                Debug.LogWarning("PhysicsImpulse2DAction >> No PhysicsImpulse2D assigned.");
                return;
            }

            physicsImpulse.Activate();
        }
    }
}