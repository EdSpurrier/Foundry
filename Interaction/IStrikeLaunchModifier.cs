using Foundry.Data;
using UnityEngine;

namespace Foundry.Interaction
{
    // Lets something that's struck change (or cancel) the knock it gets - e.g. a rope segment only swings a little,
    // where a pebble flies off. Put it on the struck collider's object. Return the impulse (N·s) to apply instead;
    // Vector3.zero = no knock at all.
    public interface IStrikeLaunchModifier
    {
        Vector3 ModifyLaunch(in StrikeData strike, Rigidbody body, Vector3 impulse);
    }
}
