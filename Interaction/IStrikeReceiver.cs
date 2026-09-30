using Foundry.Data;

namespace Foundry.Interaction
{
    // Anything that reacts to being struck (pecked, hit...). Put it on the object with the collider, or on a parent of
    // it - Strike.Dispatch finds the nearest object up the hierarchy that has any receivers and calls all of them.
    public interface IStrikeReceiver
    {
        void OnStrike(in StrikeData strike);
    }
}
