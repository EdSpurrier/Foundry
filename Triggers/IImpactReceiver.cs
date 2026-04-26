using Foundry.Data;

namespace Foundry.Triggers
{
    public interface IImpactReceiver
    {
        void OnImpact(ImpactData impactData);
    }
}
