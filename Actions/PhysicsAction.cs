
using Foundry.PhysicsSystem;
using FrameCoreU.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Actions
{
    [System.Serializable]
    public class PhysicsAction : FrameAction
    {
        public override string ActionType => "Physics";
        
        [Title("Physics")]
        [SerializeField, InlineEditor(InlineEditorObjectFieldModes.Boxed)] 
        private MonoBehaviour physicsBehaviour;

        private IPhysicsAction physicsAction;

        protected override void Activate()
        {
            if (physicsBehaviour == null)
            {
                LogWarning("PhysicsAction >> No physicsBehaviour assigned.");
                return;
            }

            physicsAction ??= physicsBehaviour as IPhysicsAction;

            if (physicsAction == null)
            {
                LogWarning($"{physicsBehaviour.name} does not implement IPhysicsAction.");
                return;
            }

            physicsAction.Activate();
        }
    }
}