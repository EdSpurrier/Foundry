using FrameCoreU.Events;
using Foundry.Attachments;
using UnityEngine;

namespace Foundry.Actions
{
    [System.Serializable]
    public class Detach : FrameAction
    {
        public override string ActionType => "Detach";

        public AttachmentGroup attachmentGroup;

        protected override void Activate()
        {
            if (attachmentGroup == null)
            {
                Debug.LogWarning($"Action Detach >> No AttachmentGroup assigned - {actionName}");
                return;
            }

            attachmentGroup.DetachAll();
        }
    }
}