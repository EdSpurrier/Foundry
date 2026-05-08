using Sirenix.OdinInspector;
using System.Collections.Generic;
using System.Linq;
using Foundry.Damage;
using FrameCoreU.Events;
using UnityEngine;

public enum LifeStatus
{
    Alive,
    Dead,
    Invincible,
    Inactive
}

public class Life : MonoBehaviour, IDamageReceiver
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