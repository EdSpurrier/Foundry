using UnityEngine;

namespace Foundry.Interfaces
{
    public interface IImpactReceiver
    {
        void OnImpact(float force, Collision collision);
    }
}