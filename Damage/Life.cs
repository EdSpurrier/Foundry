using Sirenix.OdinInspector;
using System.Collections.Generic;
using System.Linq;
using Foundry.Data;
using Foundry.Interaction;
using Foundry.Triggers;
using FrameCoreU.Events;
using UnityEngine;

namespace Foundry.Damage
{
    public enum LifeStatus
    {
        Alive,
        Dead,
        Invincible,
        Inactive
    }

    // Life points that damage takes away, with stages along the way (e.g. cracked at 50, shattered at 20) and death at
    // zero. Strikes (e.g. the chicken's peck) and, if enabled, impacts (e.g. an egg shot into it) damage it too - so a
    // window can crack then break, or a beam give way after a few pecks or eggs.
    public class Life : MonoBehaviour, IDamageReceiver, IStrikeReceiver, IImpactReceiver
    {
        [HideLabel]
        [HorizontalGroup("Split", 0.3f)]
        public LifeStatus status = LifeStatus.Alive;

        [HideLabel]
        [HorizontalGroup("Split", 0.35f)]
        [SuffixLabel("Life", Overlay = true)]
        public int lifePoints = 100;

        [HideLabel]
        [HorizontalGroup("Split", 0.35f)]
        [SuffixLabel("Max Life", Overlay = true)]
        public int maxLifePoints = 100;

        [System.Serializable]
        public class LifeStage
        {
            public int stageLifePoints = 50;

            [HideLabel]
            [FoldoutGroup("Stage Activate Event")]
            public FrameCoreEvent stageActivateEvent;
        }


        [Tooltip("Strikes (e.g. a peck) damage it by their Damage. Off = only other damage (explosions, raycasts...) hurts it.")]
        public bool damagedByStrikes = true;

        [ShowIf(nameof(damagedByStrikes))]
        [Tooltip("Only strikes of this kind damage it (e.g. \"Peck\"). Empty = any strike.")]
        public string strikeKind;

        [Tooltip("Things thrown or shot into it (e.g. an egg) damage it. Pick per object: strikes only (must be pecked), impacts only (only an egg can break it), or both. The thrown object needs an ImpactTrigger3D whose Detection Mask includes this object's layer (eggs: Ground, Egg, Interactable).")]
        public bool damagedByImpacts;

        [ShowIf(nameof(damagedByImpacts))]
        [Tooltip("Which thrown objects count, by their layer (default: Egg). Nothing = anything thrown into it.")]
        public LayerMask impactLayers;

        [ShowIf(nameof(damagedByImpacts))]
        [Tooltip("How fast (m/s) it must be hit to take damage - an egg rolling gently into it doesn't count.")]
        [SuffixLabel("m/s", Overlay = true)]
        [Min(0f)] public float minImpactSpeed = 3f;

        [ShowIf(nameof(damagedByImpacts))]
        [Tooltip("Damage from a hit at Min Impact Speed.")]
        [Min(0)] public int impactDamage = 1;

        [ShowIf(nameof(damagedByImpacts))]
        [Tooltip("Extra damage per m/s faster than Min Impact Speed (0 = every qualifying hit does Impact Damage). E.g. 0.5 = a hit 4 m/s over does 2 extra.")]
        [Min(0f)] public float damagePerExtraSpeed;

        private GameObject _lastImpactSource;
        private float _lastImpactTime = -1f;

        [ReadOnly]
        public LifeStage currentLifeStage { get; set; }

        [FoldoutGroup("Stages")]
        public List<LifeStage> lifeStages = new();

        [BoxGroup("Life Events")]

        [FoldoutGroup("Life Events/Heal Event")]
        [HideLabel]
        public FrameCoreEvent healEvent = new FrameCoreEvent { eventName = "Heal" };

        [FoldoutGroup("Life Events/Hurt Event")]
        [HideLabel]
        public FrameCoreEvent hurtEvent = new FrameCoreEvent { eventName = "Hurt" };

        [FoldoutGroup("Life Events/Death Event")]
        [HideLabel]
        public FrameCoreEvent deathEvent = new FrameCoreEvent { eventName = "Death" };

        private void Reset()
        {
            impactLayers = LayerMask.GetMask("Egg");
        }

        // Something thrown or shot into it (reported by the thrower's ImpactTrigger3D, e.g. an egg's)
        public void OnImpact(ImpactData impact)
        {
            if (!damagedByImpacts || impact == null || impact.source == null)
                return;
            if (impactLayers.value != 0 && (impactLayers.value & (1 << impact.source.layer)) == 0)
                return;
            if (impact.force < minImpactSpeed)
                return;

            // One hit can report more than one contact as it bounces - count it once
            if (impact.source == _lastImpactSource && Time.time - _lastImpactTime < 0.1f)
                return;
            _lastImpactSource = impact.source;
            _lastImpactTime = Time.time;

            int amount = impactDamage + Mathf.FloorToInt((impact.force - minImpactSpeed) * damagePerExtraSpeed);
            Vector3 travel = impact.collision3D != null ? -impact.collision3D.relativeVelocity.normalized : -impact.normal;

            ApplyDamage(new DamageData
            {
                amount = amount,
                damageType = DamageType.Impact,
                source = impact.source,
                instigator = impact.source,
                target = gameObject,
                point = impact.point,
                direction = travel,
                normal = impact.normal,
                force = impact.force
            });
        }

        private void Start()
        {
            lifeStages = lifeStages
                .OrderBy(lifeStage => lifeStage.stageLifePoints)
                .ToList();

            currentLifeStage = null;
        }

        public void ApplyDamage(DamageData damageData)
        {
            if (damageData == null)
                return;

            Damage(damageData.amount);
        }

        public void OnStrike(in StrikeData strike)
        {
            if (!damagedByStrikes || strike.Damage <= 0)
                return;
            if (!string.IsNullOrEmpty(strikeKind) && strike.Kind != strikeKind)
                return;

            ApplyDamage(new DamageData
            {
                amount = strike.Damage,
                damageType = DamageType.Strike,
                source = strike.Source,
                instigator = strike.Instigator,
                target = gameObject,
                point = strike.Point,
                direction = strike.Direction,
                normal = strike.Normal,
                force = strike.Impulse
            });
        }

        public void Damage(int amount)
        {
            if (status == LifeStatus.Dead ||
                status == LifeStatus.Inactive ||
                status == LifeStatus.Invincible)
                return;

            if (amount <= 0)
                return;

            lifePoints -= amount;

            if (lifePoints <= 0)
            {
                lifePoints = 0;
                Die();
                return;
            }

            hurtEvent.Activate();
            CheckAndActivateLifeStage();
        }

        public void CheckAndActivateLifeStage()
        {
            foreach (LifeStage lifeStage in lifeStages)
            {
                if (lifeStage.stageLifePoints >= lifePoints)
                {
                    if (lifeStage != currentLifeStage)
                    {
                        currentLifeStage = lifeStage;
                        currentLifeStage.stageActivateEvent?.Activate();
                    }

                    return;
                }
            }
        }

        public void Heal(int amount)
        {
            if (status == LifeStatus.Dead || status == LifeStatus.Inactive)
                return;

            if (amount <= 0)
                return;

            lifePoints += amount;

            if (lifePoints > maxLifePoints)
                lifePoints = maxLifePoints;

            healEvent.Activate();
        }

        public void Die()
        {
            if (status == LifeStatus.Dead)
                return;

            status = LifeStatus.Dead;
            deathEvent.Activate();
        }

        public void ResetLife()
        {
            status = LifeStatus.Alive;
            lifePoints = maxLifePoints;
            currentLifeStage = null;
        }
    }
}