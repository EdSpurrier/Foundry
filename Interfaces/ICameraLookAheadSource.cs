using UnityEngine;

namespace Foundry.Interfaces
{
    public interface ICameraLookAheadSource
    {
        Vector2 CameraVelocity { get; }
    }
}