using System;
using Foundry.Interfaces;
using FrameCoreU.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Examples.Scripts
{
    public class BreakableObject : MonoBehaviour, IImpactReceiver
    {
        [SerializeField] private float breakForce = 10f;

        [Title("Events")]
        [SerializeField] protected FrameCoreEvent onBreak;
        
        public void OnImpact(float force, Collision collision)
        {
            Debug.Log($"Break Force: {force}");
            
            if (force < breakForce)
                return;

            Debug.Log($"{name} broke from impact!");

            onBreak.Activate();
        }
    }
}