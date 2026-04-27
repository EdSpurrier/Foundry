using UnityEngine;
using Sirenix.OdinInspector;

namespace Foundry.Transformers
{
    public class Follower : Transformer
    {
        [Title("Follower")]
        public Transform follower;

        [Title("Target")]
        public Transform target;

        [Title("Start")]
        public bool startAtTrueTargetPosition = false;
        public bool startAtLockedTargetPosition = false;
        public bool startAtTargetRotation = false;

        [Title("Position")]
        public bool followPosition = true;
        public bool smoothPosition = false;
        public float positionSpeed = 1f;

        [FoldoutGroup("Position Settings")]
        public Vector3 positionOffset = Vector3.zero;

        [FoldoutGroup("Position Lock")]
        public bool lockPositionX = false;

        [FoldoutGroup("Position Lock")]
        public bool lockPositionY = false;

        [FoldoutGroup("Position Lock")]
        public bool lockPositionZ = false;

        [Title("Rotation")]
        public bool followRotation = true;
        public bool smoothRotation = false;
        public float rotationSpeed = 1f;

        [FoldoutGroup("Rotation Lock")]
        public bool lockRotationX = false;

        [FoldoutGroup("Rotation Lock")]
        public bool lockRotationY = false;

        [FoldoutGroup("Rotation Lock")]
        public bool lockRotationZ = false;
        
        protected override void Awake()
        {
            base.Awake();

            if (follower == null)
            {
                follower = transform;
            }
        }

        public override void Initialize()
        {
            if (follower == null || target == null)
            {
                return;
            }

            if (startAtTrueTargetPosition || startAtLockedTargetPosition)
            {
                Vector3 newPosition = target.position + positionOffset;
                Vector3 currentPosition = follower.position;

                if (startAtLockedTargetPosition && lockPositionX) newPosition.x = currentPosition.x;
                if (startAtLockedTargetPosition && lockPositionY) newPosition.y = currentPosition.y;
                if (startAtLockedTargetPosition && lockPositionZ) newPosition.z = currentPosition.z;

                follower.position = newPosition;
            }

            if (startAtTargetRotation)
            {
                Vector3 newRotation = target.eulerAngles;
                Vector3 currentRotation = follower.eulerAngles;

                if (lockRotationX) newRotation.x = currentRotation.x;
                if (lockRotationY) newRotation.y = currentRotation.y;
                if (lockRotationZ) newRotation.z = currentRotation.z;

                follower.rotation = Quaternion.Euler(newRotation);
            }
        }

        protected override bool CanProcess()
        {
            return active && follower != null && target != null;
        }

        protected override void Process()
        {
            if (followPosition)
            {
                FollowPosition();
            }

            if (followRotation)
            {
                FollowRotation();
            }
        }

        private void FollowPosition()
        {
            Vector3 currentPosition = follower.position;
            Vector3 newPosition = target.position + positionOffset;

            if (lockPositionX) newPosition.x = currentPosition.x;
            if (lockPositionY) newPosition.y = currentPosition.y;
            if (lockPositionZ) newPosition.z = currentPosition.z;

            if (smoothPosition)
            {
                follower.position = Vector3.Lerp(
                    follower.position,
                    newPosition,
                    positionSpeed * Time.deltaTime
                );
            }
            else
            {
                follower.position = newPosition;
            }
        }

        private void FollowRotation()
        {
            Vector3 currentRotation = follower.eulerAngles;
            Vector3 newRotation = target.eulerAngles;

            if (lockRotationX) newRotation.x = currentRotation.x;
            if (lockRotationY) newRotation.y = currentRotation.y;
            if (lockRotationZ) newRotation.z = currentRotation.z;

            if (smoothRotation)
            {
                follower.rotation = Quaternion.Lerp(
                    follower.rotation,
                    Quaternion.Euler(newRotation),
                    rotationSpeed * Time.deltaTime
                );
            }
            else
            {
                follower.rotation = Quaternion.Euler(newRotation);
            }
        }
        
#if UNITY_EDITOR
        protected override void ValidateData()
        {
            if (follower == null)
            {
                follower = transform;
            }
        }
#endif
    }
}