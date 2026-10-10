using UnityEngine;

namespace Foundry.Data
{
    // A deliberate hit on something - a peck, a hammer blow, a headbutt - sent to every IStrikeReceiver on the thing
    // that was hit (see Foundry.Interaction.Strike.Dispatch). Each receiver decides what the hit means for it: a
    // switch flips, a rope is cut, dirt is dug, something breaks or is eaten, a loose object is knocked flying.
    public struct StrikeData
    {
        // Who struck (e.g. the player) and what struck (e.g. the peck ability's object)
        public GameObject Instigator;
        public GameObject Source;

        // What kind of strike, for receivers that only react to some (e.g. "Peck")
        public string Kind;

        // The collider that was hit, where, which way the strike was travelling, and the surface normal there
        public Collider Collider;
        public Vector3 Point;
        public Vector3 Direction;
        public Vector3 Normal;

        // Which way the striker faces (e.g. the chicken's left/right) - the "forward" for a strike that goes straight up
        // or down, so receivers can tell sideways from the strike direction alone. Zero if unknown.
        public Vector3 Facing;

        // Damage for anything with Life (0 = none)
        public int Damage;

        // Impulse (N·s) given to a loose Rigidbody that was hit, along LaunchDirection (Direction if zero) - so a
        // light object flies further than a heavy one
        public float Impulse;
        public Vector3 LaunchDirection;
    }
}
