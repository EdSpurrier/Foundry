using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.CameraSystem
{
    public class CameraRig2D : MonoBehaviour
    {
        [BoxGroup("Follow")]
        [SerializeField] private Transform target;
        [BoxGroup("Follow")]
        [SerializeField] private float smoothTime = 0.25f;
        [BoxGroup("Follow")]
        [SerializeField] private Vector3 offset = new(0f, 1f, -10f);

        [BoxGroup("Look Ahead")]
        [SerializeField] private bool lookAheadEnabled = true;
        [BoxGroup("Look Ahead")]
        [SerializeField] private float lookAheadDistance = 2f;
        [BoxGroup("Look Ahead")]
        [SerializeField] private float lookAheadSmoothTime = 0.15f;
        [BoxGroup("Look Ahead")]
        [SerializeField] private float lookAheadMoveThreshold = 0.1f;

        [BoxGroup("Zoom")]
        [SerializeField] private Camera targetCamera;
        [BoxGroup("Zoom")]
        [SerializeField] private float zoomSmoothTime = 0.2f;
        [BoxGroup("Zoom")]
        [SerializeField] private float targetZoom = 5f;

        private Vector3 followVelocity;
        private Vector3 lookAheadOffset;
        private Vector3 lookAheadVelocity;
        private float zoomVelocity;

        private ICameraLookAheadSource lookAheadSource;

        public Transform Target => target;
        public Vector3 Offset => offset;
        public float TargetZoom => targetZoom;
        public bool LookAheadEnabled => lookAheadEnabled;

        private void Awake()
        {
            CacheTargetComponents();

            if (targetCamera == null)
                targetCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            UpdateLookAhead();
            UpdatePosition();
            UpdateZoom();
        }

        public void SetTarget(Transform newTarget, bool snap = false)
        {
            target = newTarget;
            CacheTargetComponents();

            if (snap)
                SnapNow();
        }

        public void SetOffset(Vector3 newOffset)
        {
            offset = newOffset;
        }

        public void SetZoom(float newZoom, bool snap = false)
        {
            targetZoom = newZoom;

            if (snap && targetCamera != null)
                targetCamera.orthographicSize = targetZoom;
        }

        public void SetLookAhead(bool enabled)
        {
            lookAheadEnabled = enabled;

            if (!enabled)
                lookAheadOffset = Vector3.zero;
        }

        public void SetLookAheadSettings(float distance, float smooth, float threshold)
        {
            lookAheadDistance = distance;
            lookAheadSmoothTime = smooth;
            lookAheadMoveThreshold = threshold;
        }

        public void SnapNow()
        {
            if (target == null)
                return;

            Vector3 goal = target.position + offset + lookAheadOffset;
            transform.position = goal;
        }

        private void CacheTargetComponents()
        {
            lookAheadSource = null;

            if (target == null)
                return;

            target.TryGetComponent(out lookAheadSource);
        }

        private void UpdateLookAhead()
        {
            if (!lookAheadEnabled || lookAheadSource == null)
            {
                lookAheadOffset = Vector3.SmoothDamp(
                    lookAheadOffset,
                    Vector3.zero,
                    ref lookAheadVelocity,
                    lookAheadSmoothTime);

                return;
            }

            float velocityX = lookAheadSource.CameraVelocity.x;
            float x = 0f;

            if (Mathf.Abs(velocityX) > lookAheadMoveThreshold)
                x = Mathf.Sign(velocityX) * lookAheadDistance;

            Vector3 targetLookAhead = new(x, 0f, 0f);

            lookAheadOffset = Vector3.SmoothDamp(
                lookAheadOffset,
                targetLookAhead,
                ref lookAheadVelocity,
                lookAheadSmoothTime);
        }

        private void UpdatePosition()
        {
            Vector3 goal = target.position + offset + lookAheadOffset;
            transform.position = Vector3.SmoothDamp(transform.position, goal, ref followVelocity, smoothTime);
        }

        private void UpdateZoom()
        {
            if (targetCamera == null)
                return;

            targetCamera.orthographicSize = Mathf.SmoothDamp(
                targetCamera.orthographicSize,
                targetZoom,
                ref zoomVelocity,
                zoomSmoothTime);
        }

        private void OnValidate()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
        }
    }
}