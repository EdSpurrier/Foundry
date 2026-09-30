using System.Collections.Generic;
using Foundry.Data;
using UnityEngine;

namespace Foundry.Interaction
{
    // Delivers a strike (a peck, a hit) to whatever it hit. Whoever does the striking (e.g. the player's Peck ability)
    // finds the collider and fills in a StrikeData; this does the rest the same way for every striker:
    //  - a loose (non-kinematic) Rigidbody is knocked along the strike by its Impulse - lighter objects fly further
    //  - every enabled IStrikeReceiver on the nearest object that has any (the collider's own object, else up its
    //    parents) is told, and decides for itself what the strike means (Interactable, Life, Consumable, ...)
    public static class Strike
    {
        public struct Result
        {
            public GameObject Target;       // the object whose receivers were told (the collider's object if none)
            public int ReceiverCount;
            public bool Launched;
        }

        private static readonly List<IStrikeReceiver> Found = new();

        public static Result Dispatch(in StrikeData strike)
        {
            Result result = default;
            if (strike.Collider == null)
                return result;

            result.Launched = Launch(strike);

            // Copied out first - a receiver may strike something else in turn (e.g. a breaking object knocking its
            // neighbour), which would reuse the shared list
            Transform owner = FindReceivers(strike.Collider.transform, Found);
            IStrikeReceiver[] receivers = Found.ToArray();
            Found.Clear();

            result.Target = owner != null ? owner.gameObject : strike.Collider.gameObject;
            result.ReceiverCount = receivers.Length;

            foreach (IStrikeReceiver receiver in receivers)
            {
                if (receiver is Object unityObject && unityObject == null)
                    continue;

                receiver.OnStrike(strike);
            }

            return result;
        }

        // Whether striking this collider would reach any receiver - e.g. to ignore trigger volumes that don't react
        public static bool HasReceiver(Collider collider)
        {
            if (collider == null)
                return false;

            bool found = FindReceivers(collider.transform, Found) != null;
            Found.Clear();
            return found;
        }

        // The nearest object from `from` upward with at least one enabled receiver, and its receivers
        private static Transform FindReceivers(Transform from, List<IStrikeReceiver> receivers)
        {
            for (Transform current = from; current != null; current = current.parent)
            {
                current.GetComponents(receivers);
                receivers.RemoveAll(receiver => receiver is Behaviour behaviour && !behaviour.isActiveAndEnabled);
                if (receivers.Count > 0)
                    return current;
            }

            return null;
        }

        private static bool Launch(in StrikeData strike)
        {
            Rigidbody body = strike.Collider.attachedRigidbody;
            if (body == null || body.isKinematic || strike.Impulse <= 0f)
                return false;

            Vector3 direction = strike.LaunchDirection.sqrMagnitude > 0.0001f ? strike.LaunchDirection.normalized : strike.Direction.normalized;
            if (direction == Vector3.zero)
                return false;

            // At the struck point, so an off-centre hit sends it spinning
            body.AddForceAtPosition(direction * strike.Impulse, strike.Point, ForceMode.Impulse);
            return true;
        }
    }
}
