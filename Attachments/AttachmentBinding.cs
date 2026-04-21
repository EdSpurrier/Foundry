using UnityEngine;

namespace Foundry.Attachments
{
    [System.Serializable]
    public class AttachmentBinding
    {
        public Transform attachmentObject;
        public Transform attachmentRoot;
        public Vector3 positionOffset;
        public Vector3 rotationOffset;

        [System.NonSerialized]
        private Transform originalParent;

        public void PreviewPosition()
        {
            if (attachmentObject == null || attachmentRoot == null)
            {
                return;
            }

            attachmentObject.position = attachmentRoot.position + positionOffset;
            attachmentObject.eulerAngles = attachmentRoot.eulerAngles + rotationOffset;
        }

        public void Attach()
        {
            if (attachmentObject == null)
            {
                return;
            }

            originalParent = attachmentObject.parent;

            if (attachmentRoot == null)
            {
                return;
            }

            attachmentObject.SetParent(attachmentRoot);
            attachmentObject.localPosition = positionOffset;
            attachmentObject.localEulerAngles = rotationOffset;
        }

        public void Detach()
        {
            if (attachmentObject == null)
            {
                return;
            }

            attachmentObject.SetParent(originalParent);
        }
    }
}