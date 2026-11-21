using System;
using Foundry.Data;
using FrameCoreU.Events;
using FrameCoreU.Unity;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Interaction
{
    // Something that can be eaten - fruit, seeds, a worm. Striking it (e.g. the chicken pecking it) takes a bite; after
    // Bites To Eat it's eaten: On Consumed fires (wire it to whatever eating it does, e.g. grant a power-up) and it's
    // removed (despawned back to its pool if it came from one). Consume() eats it outright from anything else.
    // With Remove When Eaten off it stays; Eat Again After makes it edible again (a permanent fruit you keep biting).
    public class Consumable : MonoBehaviour, IStrikeReceiver
    {
        [Title("Consumable")]
        [Tooltip("Striking it (e.g. a peck) takes a bite. Off = only Consume() eats it.")]
        [SerializeField] private bool eatOnStrike = true;

        [ShowIf(nameof(eatOnStrike))]
        [Tooltip("Only strikes of this kind take a bite (e.g. \"Peck\"). Empty = any strike.")]
        [SerializeField] private string strikeKind;

        [ShowIf(nameof(eatOnStrike))]
        [Tooltip("How many strikes it takes to eat it.")]
        [SerializeField, Min(1)] private int bitesToEat = 1;

        [Tooltip("Remove it once eaten (despawned to its pool if it came from one, otherwise destroyed). Off = it stays (e.g. an endless feeder) - use Reset Consumable to eat it again.")]
        [SerializeField] private bool removeWhenEaten = true;

        [HideIf(nameof(removeWhenEaten))]
        [Tooltip("When it stays: it can be eaten again this long (s) after each time it's eaten - e.g. a fruit the chicken keeps biting for its power-up (0 = straight away). Negative = never: only Reset Consumable makes it edible again.")]
        [SuffixLabel("s", Overlay = true)]
        [SerializeField] private float eatAgainAfter = -1f;

        [Title("Events")]
        [Tooltip("Each bite before the last.")]
        [SerializeField, HideLabel] private FrameCoreEvent onBite = new() { eventName = "Consumable - Bite" };

        [SerializeField, HideLabel] private FrameCoreEvent onConsumed = new() { eventName = "Consumable - Consumed" };

        [FoldoutGroup("Debug")]
        [SerializeField, ReadOnly] private int bitesTaken;

        [FoldoutGroup("Debug")]
        [SerializeField, ReadOnly] private bool consumed;

        public bool IsConsumed => consumed;
        public int BitesLeft => Mathf.Max(0, bitesToEat - bitesTaken);

        // Who ate it last (e.g. the player) - for code that needs to know
        public GameObject LastConsumer { get; private set; }

        // Fires with this consumable and whoever ate it (null if Consume() was called without one)
        public event Action<Consumable, GameObject> Consumed;

        public void OnStrike(in StrikeData strike)
        {
            if (!eatOnStrike || consumed)
                return;
            if (!string.IsNullOrEmpty(strikeKind) && strike.Kind != strikeKind)
                return;

            bitesTaken++;
            if (bitesTaken < bitesToEat)
            {
                onBite?.Activate();
                return;
            }

            Consume(strike.Instigator);
        }

        [Button]
        public void Consume(GameObject consumer = null)
        {
            if (consumed)
                return;

            consumed = true;
            LastConsumer = consumer;
            onConsumed?.Activate();
            Consumed?.Invoke(this, consumer);

            if (removeWhenEaten)
                gameObject.Despawn();
            else if (eatAgainAfter == 0f)
                ResetConsumable();
            else if (eatAgainAfter > 0f && isActiveAndEnabled)
                StartCoroutine(EatAgainLater());
        }

        private System.Collections.IEnumerator EatAgainLater()
        {
            yield return new WaitForSeconds(eatAgainAfter);
            if (consumed)
                ResetConsumable();
        }

        // Uneaten again, e.g. when a pooled fruit is respawned or a feeder refills
        [Button]
        public void ResetConsumable()
        {
            consumed = false;
            bitesTaken = 0;
            LastConsumer = null;
        }

        // Pooled objects are re-enabled on respawn - start uneaten
        private void OnEnable()
        {
            if (consumed && removeWhenEaten)
                ResetConsumable();
        }
    }
}
