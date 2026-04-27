using Foundry.Transformers;
using FrameCoreU.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Actions
{
    [System.Serializable]
    public class TransformerAction : FrameAction
    {
        public override string ActionType => "Transformer";
        
        [Title("Transformer")]
        [SerializeField, InlineEditor(InlineEditorObjectFieldModes.Boxed)] 
        private MonoBehaviour transformerBehaviour;

        private ITransformerAction transformerAction;

        [Title("Action State Update")]
        [SerializeField] private bool activate = true;
        
        protected override void Activate()
        {
            if (transformerBehaviour == null)
            {
                Debug.LogWarning("TransformerAction >> No transformerBehaviour assigned.");
                return;
            }

            transformerAction ??= transformerBehaviour as ITransformerAction;

            if (transformerAction == null)
            {
                Debug.LogWarning($"{transformerBehaviour.name} does not implement ITransformerAction.");
                return;
            }

            if (activate)
                transformerAction.Activate();
            else
                transformerAction.Deactivate();
        }
    }
}