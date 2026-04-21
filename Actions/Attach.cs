using FrameCoreU.Events;
using Foundry.Attachments;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Actions
{
    [System.Serializable]
    public class Attach : FrameAction
    {
        public override string ActionType => "Attach";
        
        [HideLabel]
        public AttachmentGroup attachmentGroup;

        protected override void Activate()
        {
            if (attachmentGroup == null)
            {
                Debug.LogWarning($"Action Attach >> No AttachmentGroup assigned - {actionName}");
                return;
            }

            attachmentGroup.AttachAll();
        }
    }
}