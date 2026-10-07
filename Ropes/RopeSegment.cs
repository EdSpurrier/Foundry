using Foundry.Data;
using Foundry.Interaction;
using UnityEngine;

namespace Foundry.Ropes
{
    // One link of a Rope, added when the rope is built - passes strikes on it (e.g. a peck) to the rope, which cuts there,
    // and softens the strike's knock to a gentle swing (a full launch would yank the light segment and upset the chain)
    public class RopeSegment : MonoBehaviour, IStrikeReceiver, IStrikeLaunchModifier
    {
        public Rope Rope { get; private set; }
        public int Index { get; private set; }

        internal void Initialise(Rope rope, int index)
        {
            Rope = rope;
            Index = index;
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
    }
}
