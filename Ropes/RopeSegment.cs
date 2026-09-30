using Foundry.Data;
using Foundry.Interaction;
using UnityEngine;

namespace Foundry.Ropes
{
    // One link of a Rope, added when the rope is built - passes strikes on it (e.g. a peck) to the rope, which cuts there
    public class RopeSegment : MonoBehaviour, IStrikeReceiver
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
    }
}
