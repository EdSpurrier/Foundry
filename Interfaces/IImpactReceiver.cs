using Foundry.Data;

namespace Foundry.Interfaces
{
    public interface IImpactReceiver
    {
        void OnImpact(ImpactData impactData);
    }
}
