using System;
using System.Collections.Generic;
using Foundry.Common;
using Foundry.Data;
using Foundry.Triggers;
using FrameCoreU.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Interaction
{
    // Something the player can use - a switch, button, lever, target, pressure plate, control panel. It can be used by a
    // strike (e.g. the chicken pecking it), by something thrown or shot into it (e.g. an egg - via its ImpactTrigger3D),
    // by something entering its trigger collider (e.g. stepping onto it), or by Interact() / the Interactable action. Wire its events to what it controls (open a door, start a fan, move a platform).
    //  - Press: fires On Interact every time it's used (a doorbell, a button that pulses something)
    //  - Toggle: flips between on and off, firing On Turned On / On Turned Off (a lever, a light switch)
    //  - Once: fires once, then can't be used again until Reset Interactable (a one-way switch)
    //
    // Hit Direction (optional) makes it care which side it's hit from: e.g. an egg from the left switches it on and one
    // from the right switches it off, or only a hit from above presses it.
    public class Interactable : MonoBehaviour, IStrikeReceiver, IImpactReceiver
    {
        public enum InteractMode
        {
            Press,
            Toggle,
            Once
        }

        // The side of the interactable a hit comes from (a hit travelling right comes from the left)
        public enum HitSide
        {
            Left,
            Right,
            Above,
            Below
        }

        public enum HitResponse
        {
            Use,        // what its mode does: press, toggle or use-once
            TurnOn,     // Toggle only
            TurnOff,    // Toggle only
            Ignore
        }

        // What can use it (for which the side rules apply)
        [Flags]
        public enum HitSources
        {
            Strikes = 1,
            Impacts = 2,
            Triggers = 4
        }

        public enum TriggerBehaviour
        {
            UseOnEnter,         // something entering counts as a hit
            OnWhileOccupied     // Toggle only: on while anything's in it, off when it's empty (a pressure plate)
        }

        [Title("Interactable")]
        [EnumToggleButtons, HideLabel]
        [SerializeField] private InteractMode mode = InteractMode.Press;

        [ShowIf(nameof(mode), InteractMode.Toggle)]
        [Tooltip("Whether it starts switched on.")]
        [SerializeField] private bool isOn;

        [Tooltip("Minimum time between uses, so one press (or a bouncing egg) doesn't count twice.")]
        [SerializeField] private Cooldown cooldown = new();

        [Title("Used By")]
        [Tooltip("Striking it (e.g. a peck) uses it.")]
        [SerializeField] private bool useOnStrike = true;

        [ShowIf(nameof(useOnStrike))]
        [Tooltip("Only strikes of this kind use it (e.g. \"Peck\"). Empty = any strike.")]
        [SerializeField] private string strikeKind;

        [Tooltip("Something thrown or shot into it (e.g. an egg) uses it. The thrown object needs an ImpactTrigger3D whose Detection Mask includes this object's layer (eggs have one).")]
        [SerializeField] private bool useOnImpact = true;

        [ShowIf(nameof(useOnImpact))]
        [Tooltip("Which thrown/shot objects count, by their layer (default: Egg). Nothing = anything thrown into it.")]
        [SerializeField] private LayerMask impactLayers;

        [ShowIf(nameof(useOnImpact))]
        [Tooltip("How fast (m/s) it must be hit - so an egg rolling gently into it doesn't count.")]
        [SuffixLabel("m/s", Overlay = true)]
        [SerializeField, Min(0f)] private float minImpactSpeed = 3f;

        [Tooltip("Something entering a trigger collider on this object uses it - the chicken stepping onto a pressure plate, a pushed rock sliding onto it, an egg rolling into a slot. Needs a collider with Is Trigger on this same object (it can have a solid one as well).")]
        [InfoBox("Add a collider with Is Trigger ticked to this object for this to work.", InfoMessageType.Warning, nameof(MissingTrigger))]
        [SerializeField] private bool useOnTrigger;

        [ShowIf(nameof(useOnTrigger))]
        [Tooltip("What can set it off, by layer (default: Player, Moveable, Egg). Nothing = anything.")]
        [SerializeField] private LayerMask triggerLayers;

        [ShowIf(nameof(ShowTriggerBehaviour))]
        [EnumToggleButtons]
        [Tooltip("Use On Enter: each thing entering counts as a hit (press / toggle, with the side rules). On While Occupied: a pressure plate - on while anything's in it, off when the last thing leaves.")]
        [SerializeField] private TriggerBehaviour triggerBehaviour = TriggerBehaviour.UseOnEnter;

        [Title("Hit Direction")]
        [LabelText("Respond Per Side")]
        [InfoBox("A hit from any side does what its mode does. Tick Respond Per Side to choose what a hit from the left, right, above and below each do (e.g. an egg from the left turns it on, from the right turns it off).", InfoMessageType.None, "@!directional")]
        [Tooltip("Off: a hit from any side does what its mode does. On: choose what a hit from each side does.")]
        [SerializeField] private bool directional;

        [ShowIf(nameof(directional))]
        [EnumToggleButtons]
        [Tooltip("Which uses the side rules apply to - the others do what the mode does from any side. (For a trigger, the side is the way the thing entering was moving.)")]
        [SerializeField] private HitSources sideRulesApplyTo = HitSources.Strikes | HitSources.Impacts | HitSources.Triggers;

        [ShowIf(nameof(directional))]
        [Tooltip("Sides are this object's own left/right/up/down (they turn with it). Off = the world's.")]
        [SerializeField] private bool sidesTurnWithObject = true;

        [ShowIf(nameof(directional))]
        [ValueDropdown(nameof(ResponseOptions))]
        [Tooltip("A hit coming from the left (travelling right).")]
        [SerializeField] private HitResponse fromLeft = HitResponse.Use;

        [ShowIf(nameof(directional))]
        [ValueDropdown(nameof(ResponseOptions))]
        [Tooltip("A hit coming from the right (travelling left).")]
        [SerializeField] private HitResponse fromRight = HitResponse.Use;

        [ShowIf(nameof(directional))]
        [ValueDropdown(nameof(ResponseOptions))]
        [Tooltip("A hit coming from above (travelling down).")]
        [SerializeField] private HitResponse fromAbove = HitResponse.Use;

        [ShowIf(nameof(directional))]
        [ValueDropdown(nameof(ResponseOptions))]
        [Tooltip("A hit coming from below (travelling up).")]
        [SerializeField] private HitResponse fromBelow = HitResponse.Use;

        [Title("Events")]
        [SerializeField, HideLabel] private FrameCoreEvent onInteract = new() { eventName = "Interactable - Interact" };

        [ShowIf(nameof(mode), InteractMode.Toggle)]
        [SerializeField, HideLabel] private FrameCoreEvent onTurnedOn = new() { eventName = "Interactable - Turned On" };

        [ShowIf(nameof(mode), InteractMode.Toggle)]
        [SerializeField, HideLabel] private FrameCoreEvent onTurnedOff = new() { eventName = "Interactable - Turned Off" };

        [FoldoutGroup("Debug")]
        [SerializeField, ReadOnly] private bool used;

        [FoldoutGroup("Debug")]
        [SerializeField, ReadOnly] private string lastHit;

        [FoldoutGroup("Debug")]
        [SerializeField, ReadOnly] private int occupants;

        private bool _startedOn;
        private Collider _trigger;

        // Things inside the trigger, by their Rigidbody's object (or collider's) - counted per collider, since one
        // thing can have several
        private readonly Dictionary<GameObject, int> _inside = new();
        private readonly List<GameObject> _stale = new();

        private bool OccupancyMode => useOnTrigger && mode == InteractMode.Toggle && triggerBehaviour == TriggerBehaviour.OnWhileOccupied;
        private bool ShowTriggerBehaviour => useOnTrigger && mode == InteractMode.Toggle;

        public InteractMode Mode => mode;
        public bool IsOn => isOn;
        public bool CanInteract => isActiveAndEnabled && cooldown.Ready && !(mode == InteractMode.Once && used);

        // Fires with this interactable after every use (and every Set On change), for code listeners
        public event Action<Interactable> Interacted;

        private void Reset()
        {
            impactLayers = LayerMask.GetMask("Egg");
            triggerLayers = LayerMask.GetMask("Player", "Moveable", "Egg");
        }

        private void Awake()
        {
            _startedOn = isOn;
            _trigger = FindTrigger();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!useOnTrigger || !CountsForTrigger(other))
                return;

            GameObject occupant = OccupantOf(other);
            _inside.TryGetValue(occupant, out int colliders);
            _inside[occupant] = colliders + 1;
            occupants = _inside.Count;
            if (colliders > 0)
                return; // another collider of something already inside

            if (OccupancyMode)
            {
                if (_inside.Count == 1)
                    SetOccupied(true, occupant.name);
                return;
            }

            bool sided = directional && (sideRulesApplyTo & HitSources.Triggers) != 0;
            HandleHit(EntryDirection(other), sided, occupant.name);
        }

        private void OnTriggerExit(Collider other)
        {
            GameObject occupant = OccupantOf(other);
            if (!_inside.TryGetValue(occupant, out int colliders))
                return;

            if (colliders > 1)
            {
                _inside[occupant] = colliders - 1;
                return;
            }

            _inside.Remove(occupant);
            occupants = _inside.Count;
            if (OccupancyMode && _inside.Count == 0)
                SetOccupied(false, occupant.name);
        }

        // Something inside that was despawned or destroyed never sends an exit - drop it, or a pressure plate would
        // stay down forever
        private void FixedUpdate()
        {
            if (_inside.Count == 0)
                return;

            foreach (GameObject occupant in _inside.Keys)
                if (occupant == null || !occupant.activeInHierarchy)
                    _stale.Add(occupant);

            if (_stale.Count == 0)
                return;

            foreach (GameObject occupant in _stale)
                _inside.Remove(occupant);
            _stale.Clear();
            occupants = _inside.Count;

            if (OccupancyMode && _inside.Count == 0)
                SetOccupied(false, "(left)");
        }

        private void OnDisable()
        {
            _inside.Clear();
            occupants = 0;
        }

        public void OnStrike(in StrikeData strike)
        {
            if (!useOnStrike)
                return;
            if (!string.IsNullOrEmpty(strikeKind) && strike.Kind != strikeKind)
                return;

            bool sided = directional && (sideRulesApplyTo & HitSources.Strikes) != 0;
            HandleHit(strike.Direction, sided, "Strike");
        }

        public void OnImpact(ImpactData impact)
        {
            if (!useOnImpact || impact == null || impact.source == null)
                return;
            if (impactLayers.value != 0 && (impactLayers.value & (1 << impact.source.layer)) == 0)
                return;
            if (impact.force < minImpactSpeed)
                return;

            // The impact is reported by the thrown object: its collision's relative velocity is our velocity minus
            // its own, so the way it was travelling is the reverse of that
            Vector3 travel = impact.collision3D != null ? -impact.collision3D.relativeVelocity : -impact.normal;
            bool sided = directional && (sideRulesApplyTo & HitSources.Impacts) != 0;
            HandleHit(travel, sided, impact.source.name);
        }

        // Uses it as its mode does, if it can be used right now. Returns whether it was.
        [Button]
        public bool Interact(GameObject instigator = null)
        {
            return Respond(HitResponse.Use);
        }

        // Uses it as if hit travelling in a direction (world space) - applying the Hit Direction rules if they're on
        public bool InteractFrom(Vector3 hitDirection, GameObject instigator = null)
        {
            return directional ? Respond(ResponseFor(SideOf(hitDirection))) : Respond(HitResponse.Use);
        }

        // Switches a Toggle on or off directly (e.g. to sync two levers), firing its events if it changes
        public void SetOn(bool on)
        {
            if (mode != InteractMode.Toggle || on == isOn)
                return;

            ApplyOn(on);
            Interacted?.Invoke(this);
        }

        // Back to how it started: usable again (Once), and its starting on/off state (Toggle, without firing events)
        [Button]
        public void ResetInteractable()
        {
            used = false;
            _inside.Clear();
            occupants = 0;
            isOn = _startedOn;
            cooldown.Clear();
        }

        private void HandleHit(Vector3 travel, bool sided, string by)
        {
            HitResponse response = HitResponse.Use;
            string side = "";
            if (sided && travel.sqrMagnitude > 0.0001f)
            {
                HitSide hitSide = SideOf(travel);
                response = ResponseFor(hitSide);
                side = $" from {hitSide}";
            }

            lastHit = $"{by}{side} → {response}";
            Respond(response);
        }

        private bool Respond(HitResponse response)
        {
            if (response == HitResponse.Ignore || !CanInteract)
                return false;

            // Turn On / Off only mean something to a toggle; for the other modes they're just a use
            if (mode == InteractMode.Toggle && response != HitResponse.Use)
            {
                bool target = response == HitResponse.TurnOn;
                if (target == isOn || !cooldown.TryUse())
                    return false;

                used = true;
                onInteract?.Activate();
                ApplyOn(target);
                Interacted?.Invoke(this);
                return true;
            }

            if (!cooldown.TryUse())
                return false;

            used = true;
            onInteract?.Activate();

            if (mode == InteractMode.Toggle)
                ApplyOn(!isOn);

            Interacted?.Invoke(this);
            return true;
        }

        // Pressure plate: straight on/off with what's inside, no cooldown (a quick step off must still release it)
        private void SetOccupied(bool on, string by)
        {
            if (on == isOn || !isActiveAndEnabled)
                return;

            used = true;
            lastHit = $"{by} → {(on ? "pressed" : "released")}";
            if (on) onInteract?.Activate();
            ApplyOn(on);
            Interacted?.Invoke(this);
        }

        private bool CountsForTrigger(Collider other)
        {
            GameObject occupant = OccupantOf(other);
            return triggerLayers.value == 0
                   || (triggerLayers.value & (1 << other.gameObject.layer)) != 0
                   || (triggerLayers.value & (1 << occupant.layer)) != 0;
        }

        private static GameObject OccupantOf(Collider other) => other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;

        // The way something entering was moving: its Rigidbody's velocity, or (if it's still or kinematic) from where
        // it is toward the trigger's centre
        private Vector3 EntryDirection(Collider other)
        {
            Rigidbody body = other.attachedRigidbody;
            if (body != null && !body.isKinematic && body.linearVelocity.sqrMagnitude > 0.01f)
                return body.linearVelocity;

            Vector3 center = _trigger != null ? _trigger.bounds.center : transform.position;
            return center - other.bounds.center;
        }

        private Collider FindTrigger()
        {
            foreach (Collider own in GetComponents<Collider>())
                if (own.isTrigger) return own;
            return null;
        }

        private bool MissingTrigger() => useOnTrigger && FindTrigger() == null;

        private void ApplyOn(bool on)
        {
            isOn = on;
            if (on) onTurnedOn?.Activate();
            else onTurnedOff?.Activate();
        }

        // Which side a hit travelling this way comes from: whichever of left/right/up/down it's mostly moving along
        private HitSide SideOf(Vector3 travel)
        {
            Vector3 direction = sidesTurnWithObject ? transform.InverseTransformDirection(travel) : travel;
            if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
                return direction.x > 0f ? HitSide.Left : HitSide.Right;
            return direction.y < 0f ? HitSide.Above : HitSide.Below;
        }

        private HitResponse ResponseFor(HitSide side) => side switch
        {
            HitSide.Left => fromLeft,
            HitSide.Right => fromRight,
            HitSide.Above => fromAbove,
            _ => fromBelow
        };

        // Turn On / Turn Off are only offered for a Toggle
        private IEnumerable<ValueDropdownItem<HitResponse>> ResponseOptions()
        {
            yield return new ValueDropdownItem<HitResponse>(mode == InteractMode.Toggle ? "Toggle" : mode == InteractMode.Press ? "Press" : "Use (once)", HitResponse.Use);
            if (mode == InteractMode.Toggle)
            {
                yield return new ValueDropdownItem<HitResponse>("Turn On", HitResponse.TurnOn);
                yield return new ValueDropdownItem<HitResponse>("Turn Off", HitResponse.TurnOff);
            }
            yield return new ValueDropdownItem<HitResponse>("Ignore", HitResponse.Ignore);
        }

#if UNITY_EDITOR
        // With Hit Direction on: an arrow into each side, coloured by what a hit from there does
        private void OnDrawGizmosSelected()
        {
            if (!directional)
                return;

            Bounds bounds = TryGetComponent(out Collider ownCollider) ? ownCollider.bounds : new Bounds(transform.position, Vector3.one * 0.5f);
            Vector3 center = bounds.center;
            float reachX = bounds.extents.x + 0.6f, reachY = bounds.extents.y + 0.6f;
            Vector3 right = sidesTurnWithObject ? transform.right : Vector3.right;
            Vector3 up = sidesTurnWithObject ? transform.up : Vector3.up;

            DrawSide(center - right * reachX, right, fromLeft, "from left");
            DrawSide(center + right * reachX, -right, fromRight, "from right");
            DrawSide(center + up * reachY, -up, fromAbove, "from above");
            DrawSide(center - up * reachY, up, fromBelow, "from below");
        }

        private void DrawSide(Vector3 start, Vector3 direction, HitResponse response, string label)
        {
            Gizmos.color = response switch
            {
                HitResponse.Use => new Color(0.3f, 0.9f, 0.4f),
                HitResponse.TurnOn => new Color(0.3f, 0.8f, 1f),
                HitResponse.TurnOff => new Color(1f, 0.6f, 0.2f),
                _ => new Color(0.5f, 0.5f, 0.5f, 0.5f)
            };

            const float LENGTH = 0.45f;
            Vector3 tip = start + direction * LENGTH;
            Gizmos.DrawLine(start, tip);
            Vector3 side = Vector3.Cross(direction, Vector3.forward).normalized * 0.1f;
            Gizmos.DrawLine(tip, tip - direction * 0.15f + side);
            Gizmos.DrawLine(tip, tip - direction * 0.15f - side);

            string text = response == HitResponse.Use ? (mode == InteractMode.Toggle ? "Toggle" : mode == InteractMode.Press ? "Press" : "Use") : response.ToString();
            UnityEditor.Handles.Label(start - direction * 0.1f, $"{label}: {text}");
        }
#endif
    }
}
