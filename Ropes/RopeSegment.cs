using Foundry.Data;
using Foundry.Interaction;
using Foundry.Triggers;
using UnityEngine;

namespace Foundry.Ropes
{
    // One link of a Rope, added when the rope is built. Passes hits on to the rope, which cuts there: strikes (e.g. a
    // peck), and things thrown through it (e.g. an egg - sensed as it flies through the trigger segment, or reported
    // by the egg's ImpactTrigger3D if the segments are solid). It also softens a strike's knock to a gentle swing (a full
    // launch would yank the light segment and upset the chain).
    public class RopeSegment : MonoBehaviour, IStrikeReceiver, IStrikeLaunchModifier, IImpactReceiver
    {
        public Rope Rope { get; private set; }
        public int Index { get; private set; }

        private Rigidbody _body;

        internal void Initialise(Rope rope, int index)
        {
            Rope = rope;
            Index = index;
            _body = GetComponent<Rigidbody>();
        }

        public void OnStrike(in StrikeData strike)
        {
            if (Rope != null)
                Rope.OnSegmentStruck(Index, strike.Kind);
        }

        // At most Strike Swing (m/s) of speed change on the segment
        public Vector3 ModifyLaunch(in StrikeData strike, Rigidbody body, Vector3 impulse)
        {
            if (Rope == null || body == null)
                return impulse;

            float maxImpulse = Rope.StrikeSwing * body.mass;
            return impulse.sqrMagnitude > maxImpulse * maxImpulse ? impulse.normalized * maxImpulse : impulse;
        }

        // Trigger segments (the default): something flying through
        private void OnTriggerEnter(Collider other)
        {
            if (Rope == null)
                return;

            Rigidbody thrown = other.attachedRigidbody;
            if (thrown == null || thrown.isKinematic)
                return;

            Vector3 relative = thrown.linearVelocity - (_body != null ? _body.linearVelocity : Vector3.zero);
            Rope.OnSegmentThrownInto(Index, thrown.gameObject, other.gameObject.layer, relative.magnitude);
        }

        // Solid segments (Collide With World): the thrown object's ImpactTrigger3D reports hitting it
        public void OnImpact(ImpactData impact)
        {
            if (Rope == null || impact == null || impact.source == null)
                return;

            Rope.OnSegmentThrownInto(Index, impact.source, impact.source.layer, impact.force);
        }
    }
}
