using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace Foundry.Attachments
{
    [System.Serializable]
    public class AttachmentGroup
    {
        [BoxGroup("Settings")]
        [Button("Preview Position")]
        public void PreviewPosition()
        {
            if (attachments == null) return;
            foreach (AttachmentBinding attachment in attachments)
            {
                if (attachment == null) continue;
                attachment.PreviewPosition();
            }
        }
        [BoxGroup("Settings")]
        public bool disableAutoPreview = false;

        public List<AttachmentBinding> attachments = new();
        
        public void AttachAll()
        {
            if (attachments == null) return;

            foreach (AttachmentBinding attachment in attachments)
            {
                if (attachment == null) continue;
                attachment.Attach();
            }
        }

        public void DetachAll()
        {
            if (attachments == null) return;

            foreach (AttachmentBinding attachment in attachments)
            {
                if (attachment == null) continue;
                attachment.Detach();
            }
        }

        
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (disableAutoPreview || attachments == null)
            {
                return;
            }

            foreach (AttachmentBinding attachment in attachments)
            {
                if (attachment == null) continue;
                attachment.PreviewPosition();
            }
        }
#endif
    }
}