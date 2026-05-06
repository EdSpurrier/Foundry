using UnityEngine;

namespace Foundry.CameraSystem
{
    public interface ICameraLookAheadSource
    {
        Vector2 CameraVelocity { get; }
    }
}