using Foundry.Data;

namespace Foundry.Triggers
{
    public interface IUpdraftReceiver
    {
        void OnUpdraft(UpdraftData updraftData, float deltaTime);
    }
}
