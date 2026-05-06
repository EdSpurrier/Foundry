using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.CameraSystem
{
    public class CameraCore : MonoBehaviour
    {
        [Title("References")]
        [SerializeField] private CameraRig2D mainRig;

        [Title("Defaults")]
        [SerializeField] private Transform defaultTarget;
        [SerializeField] private Vector3 defaultOffset = new(0f, 1f, -10f);
        [SerializeField] private float defaultZoom = 5f;
        [SerializeField] private bool defaultLookAheadEnabled = true;

        private Coroutine temporaryFocusRoutine;

        private Transform cachedTargetBeforeFocus;
        private Vector3 cachedOffsetBeforeFocus;
        private float cachedZoomBeforeFocus;
        private bool cachedLookAheadBeforeFocus;
        private bool hasTemporaryFocusState;

        public CameraRig2D MainRig => mainRig;
        public Transform DefaultTarget => defaultTarget;

        private void Awake()
        {
            ResetCamera(true);
        }

        public void SetTarget(Transform target, bool snap = false)
        {
            if (mainRig == null || target == null)
                return;

            mainRig.SetTarget(target, snap);
        }

        public void SetOffset(Vector3 offset)
        {
            if (mainRig == null)
                return;

            mainRig.SetOffset(offset);
        }

        public void SetZoom(float zoom, bool snap = false)
        {
            if (mainRig == null)
                return;

            mainRig.SetZoom(zoom, snap);
        }

        public void SetLookAhead(bool enabled)
        {
            if (mainRig == null)
                return;

            mainRig.SetLookAhead(enabled);
        }

        public void SetDefaultTarget(Transform target)
        {
            defaultTarget = target;
        }

        public void ResetCamera(bool snap = false)
        {
            if (mainRig == null)
                return;

            StopTemporaryFocus(restoreState: false);

            if (defaultTarget != null)
                mainRig.SetTarget(defaultTarget, snap);

            mainRig.SetOffset(defaultOffset);
            mainRig.SetZoom(defaultZoom, snap);
            mainRig.SetLookAhead(defaultLookAheadEnabled);

            if (snap)
                mainRig.SnapNow();
        }

        public void FocusTemporary(
            Transform focusTarget,
            float duration,
            bool snap = false,
            bool disableLookAhead = true,
            bool restorePreviousState = true)
        {
            if (mainRig == null || focusTarget == null)
                return;

            StopTemporaryFocus(restoreState: false);
            CacheCurrentState();

            if (disableLookAhead)
                mainRig.SetLookAhead(false);

            mainRig.SetTarget(focusTarget, snap);

            temporaryFocusRoutine = StartCoroutine(
                FocusTemporaryRoutine(duration, snap, restorePreviousState));
        }

        public void FocusTemporary(
            Transform focusTarget,
            Vector3 focusOffset,
            float? focusZoom,
            float duration,
            bool snap = false,
            bool disableLookAhead = true,
            bool restorePreviousState = true)
        {
            if (mainRig == null || focusTarget == null)
                return;

            StopTemporaryFocus(restoreState: false);
            CacheCurrentState();

            if (disableLookAhead)
                mainRig.SetLookAhead(false);

            mainRig.SetTarget(focusTarget, snap);
            mainRig.SetOffset(focusOffset);

            if (focusZoom.HasValue)
                mainRig.SetZoom(focusZoom.Value, snap);

            temporaryFocusRoutine = StartCoroutine(
                FocusTemporaryRoutine(duration, snap, restorePreviousState));
        }

        public void StopTemporaryFocus(bool restoreState = true)
        {
            if (temporaryFocusRoutine != null)
            {
                StopCoroutine(temporaryFocusRoutine);
                temporaryFocusRoutine = null;
            }

            if (!hasTemporaryFocusState)
                return;

            if (restoreState)
                RestoreCachedState(false);
            else
                ClearCachedState();
        }

        private IEnumerator FocusTemporaryRoutine(float duration, bool snapOnRestore, bool restorePreviousState)
        {
            yield return new WaitForSeconds(duration);

            if (restorePreviousState)
                RestoreCachedState(snapOnRestore);
            else
                ClearCachedState();

            temporaryFocusRoutine = null;
        }

        private void CacheCurrentState()
        {
            cachedTargetBeforeFocus = mainRig.Target;
            cachedOffsetBeforeFocus = mainRig.Offset;
            cachedZoomBeforeFocus = mainRig.TargetZoom;
            cachedLookAheadBeforeFocus = mainRig.LookAheadEnabled;
            hasTemporaryFocusState = true;
        }

        private void RestoreCachedState(bool snap)
        {
            if (!hasTemporaryFocusState || mainRig == null)
                return;

            if (cachedTargetBeforeFocus != null)
                mainRig.SetTarget(cachedTargetBeforeFocus, snap);

            mainRig.SetOffset(cachedOffsetBeforeFocus);
            mainRig.SetZoom(cachedZoomBeforeFocus, snap);
            mainRig.SetLookAhead(cachedLookAheadBeforeFocus);

            if (snap)
                mainRig.SnapNow();

            ClearCachedState();
        }

        private void ClearCachedState()
        {
            hasTemporaryFocusState = false;
            cachedTargetBeforeFocus = null;
            cachedOffsetBeforeFocus = Vector3.zero;
            cachedZoomBeforeFocus = 0f;
            cachedLookAheadBeforeFocus = false;
        }
    }
}