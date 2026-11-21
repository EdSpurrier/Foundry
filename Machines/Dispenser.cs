using System.Collections;
using System.Collections.Generic;
using FrameCoreU.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Machines
{
    // Hands out copies of an object, one per Dispense(): a hatch that drops a crate, a chute that rolls out a ball, a
    // pipe that spits out a barrel. Optionally swings a door open first and shut again after. The Template is a scene
    // object (kept switched off) that each copy is cloned from - set it up exactly as you want the copies (Rigidbody,
    // layer, StayAwake...). Wire Dispense() to anything with the Dispense action (e.g. an Interactable an egg hits).
    //
    // Copies are real objects that stay in the world (a pile of crates) - Max Alive removes the oldest beyond that.
    public class Dispenser : MonoBehaviour
    {
        [Title("What")]
        [Tooltip("The object to copy. Keep it switched off in the scene (it's switched off on start); each copy is a clone of it, switched on.")]
        [SerializeField, Required] private GameObject template;

        [Tooltip("Where copies appear, and which way they face. Empty = where the Template is.")]
        [SerializeField] private Transform spawnPoint;

        [Tooltip("Velocity (m/s, world) each copy starts with - e.g. out of the hatch.")]
        [SerializeField] private Vector3 launchVelocity = new(0f, 0.5f, -5f);

        [Tooltip("Copies go under this (keeps the hierarchy tidy). Empty = under this object.")]
        [SerializeField] private Transform container;

        [Tooltip("The most copies in the world at once - past this the oldest is removed. 0 = no limit.")]
        [SerializeField, Min(0)] private int maxAlive = 12;

        [Title("Door")]
        [Tooltip("Optional: a hinge transform (the door is its child) swung open before each copy comes out and shut after.")]
        [SerializeField] private Transform door;

        [ShowIf(nameof(door))]
        [Tooltip("The door's local rotation when open (closed = how it starts).")]
        [SerializeField] private Vector3 doorOpenEuler = new(-100f, 0f, 0f);

        [ShowIf(nameof(door))]
        [SuffixLabel("s", Overlay = true)]
        [SerializeField, Min(0f)] private float doorOpenTime = 0.25f;

        [ShowIf(nameof(door))]
        [Tooltip("How long it stays open after the copy comes out.")]
        [SuffixLabel("s", Overlay = true)]
        [SerializeField, Min(0f)] private float doorHoldTime = 1f;

        [ShowIf(nameof(door))]
        [SuffixLabel("s", Overlay = true)]
        [SerializeField, Min(0f)] private float doorCloseTime = 0.5f;

        [Title("Timing")]
        [Tooltip("Dispense() is ignored until this long after the last one started (and while the door is still moving).")]
        [SuffixLabel("s", Overlay = true)]
        [SerializeField, Min(0f)] private float cooldown = 1.5f;

        [Title("Events")]
        [SerializeField, HideLabel] private FrameCoreEvent onDispense = new() { eventName = "Dispenser - Dispense" };
        [SerializeField, HideLabel] private FrameCoreEvent onDoorShut = new() { eventName = "Dispenser - Door Shut" };

        [FoldoutGroup("Debug")]
        [SerializeField, ReadOnly] private int dispensed;

        [FoldoutGroup("Debug")]
        [SerializeField, ReadOnly] private bool busy;

        private readonly List<GameObject> _alive = new();
        private Quaternion _doorClosed;
        private float _lastDispense = float.NegativeInfinity;

        public int Dispensed => dispensed;
        public bool Busy => busy;
        public IReadOnlyList<GameObject> Alive => _alive;

        private void Awake()
        {
            if (template != null && template.activeSelf)
                template.SetActive(false);
            if (door != null)
                _doorClosed = door.localRotation;
        }

        [Button("Dispense")]
        public void Dispense()
        {
            if (!Application.isPlaying || template == null)
                return;
            if (busy || Time.time < _lastDispense + cooldown)
                return;

            _lastDispense = Time.time;
            StartCoroutine(DispenseRoutine());
        }

        private IEnumerator DispenseRoutine()
        {
            busy = true;
            Quaternion open = _doorClosed * Quaternion.Euler(doorOpenEuler);

            if (door != null)
                yield return SwingDoor(_doorClosed, open, doorOpenTime);

            Release();

            if (door != null)
            {
                yield return new WaitForSeconds(doorHoldTime);
                yield return SwingDoor(open, _doorClosed, doorCloseTime);
                onDoorShut?.Activate();
            }

            busy = false;
        }

        private void Release()
        {
            Transform at = spawnPoint != null ? spawnPoint : template.transform;
            GameObject copy = Instantiate(template, at.position, at.rotation, container != null ? container : transform);
            copy.name = $"{template.name} {dispensed + 1}";
            copy.SetActive(true);

            if (copy.TryGetComponent(out Rigidbody body) && !body.isKinematic)
                body.linearVelocity = launchVelocity;

            _alive.RemoveAll(o => o == null);
            _alive.Add(copy);
            while (maxAlive > 0 && _alive.Count > maxAlive)
            {
                Destroy(_alive[0]);
                _alive.RemoveAt(0);
            }

            dispensed++;
            onDispense?.Activate();
        }

        private IEnumerator SwingDoor(Quaternion from, Quaternion to, float time)
        {
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                door.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t / time));
                yield return null;
            }
            door.localRotation = to;
        }

        private void OnDrawGizmosSelected()
        {
            Transform at = spawnPoint != null ? spawnPoint : template != null ? template.transform : null;
            if (at == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(at.position, 0.2f);
            Gizmos.DrawLine(at.position, at.position + launchVelocity * 0.3f);
        }
    }
}
