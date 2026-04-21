using Sirenix.OdinInspector;
using Sirenix.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TriggerType
{
    Single,
    Repeating,
    CountUpToNumber,
    LimitedCount,
    Range,
}

[Serializable]
public class TriggerTypeController
{
    [HideLabel]
    [HorizontalGroup("Split", 0.35f)]
    public TriggerType type;

    [InlineButton("TestTrigger")]
    [InlineButton("Reset")]
    [HorizontalGroup("Split", 0.65f)]
    [HideIf("type", TriggerType.Single)]
    [LabelWidth(50)]
    public bool toggle = false;


    [ShowIf("type", TriggerType.Range)]
    [HideLabel]
    public MaxMinInt rangeCount;

    [ShowIf("type", TriggerType.CountUpToNumber)]
    public int countUpToNumber = 10;

    [ShowIf("type", TriggerType.LimitedCount)]
    public int countLimit = 5;

    [FoldoutGroup("System")]
    public int triggerCount = 0;
    [FoldoutGroup("System")] 
    public bool toggleState = false;
    [HideLabel]
    public DeBugger debug;
    
    public void TestTrigger()
    {
        Debug.Log("Trigger Test: " + IsTriggerAllowed() );
    }

    public bool IsTriggerAllowed()
    {
        bool allowed = true;

        if (toggle)
        {
            toggleState = !toggleState;
            if (toggleState)
            {
                triggerCount++;
            };
        }
        else {
            triggerCount++;
        };
        


        if (type == TriggerType.Single)
        {
            if (triggerCount != 1)
            {
                allowed =  false;
            };
        }
        else if (type == TriggerType.CountUpToNumber)
        {
            if (triggerCount != countUpToNumber)
            {
                allowed = false;
            };
        }
        else if (type == TriggerType.LimitedCount)
        {
            if (triggerCount > countLimit)
            {
                allowed = false;
            };
        }
        else if (type == TriggerType.Range)
        {
            if ( !triggerCount.BetweenRangeInt(rangeCount.min, rangeCount.max, true) )
            {
                allowed = false;
            };
        };

        return allowed;
    }


    public void Reset()
    {
        triggerCount = 0;
        toggleState = false;
    }
}


public class TriggerController : MonoBehaviour
{
    [Title("Layer Mask")]
    [HideLabel]
    [HorizontalGroup("Split", 0.50f)]
    public LayerMaskNames layerMask;
    [Title("")]
    [HorizontalGroup("Split", 0.50f)]
    public bool triggerOnExitOfAll = false;

    [Title("Trigger Controller")]
    [OdinSerialize]
    [HideLabel]
    public TriggerTypeController type;


    

    [Title("Enter Trigger")]
    [HideLabel]
    public FrameCoreEvent enterEvent;


    [Title("Stay Trigger")]
    [HideLabel]
    public FrameCoreEvent stayEvent;

    [Title("Exit Trigger")]
    [HideLabel]
    public FrameCoreEvent exitEvent;



    [FoldoutGroup("Objects In Trigger")]
    public List<GameObject> gameObjectsInTrigger;

    [Title("System")]
    [HideLabel]
    public DeBugger debug;


    bool CheckIfInTrigger(GameObject thisGameObject)
    {
        bool result = false;

        if (!gameObjectsInTrigger.Contains(thisGameObject))
        {
            if (Frame.core.layerMasks.InLayerMask(layerMask, thisGameObject))
            {
                gameObjectsInTrigger.Add(thisGameObject);
                result = true;
            };
        };

        return result;
    }


    private void OnTriggerEnter(Collider other)
    {
        
        if (CheckIfInTrigger(other.gameObject))
        {
            enterEvent.Activate();
        };

    }

    private void OnTriggerStay(Collider other)
    {
        stayEvent.Activate();
    }

    private void OnTriggerExit(Collider other)
    {
        if (gameObjectsInTrigger.Contains(other.gameObject))
        {
            gameObjectsInTrigger.Remove(other.gameObject);
            if (triggerOnExitOfAll)
            {
                if (gameObjectsInTrigger.Count == 0)
                {
                    exitEvent.Activate();
                };
            }
            else {
                exitEvent.Activate();
            };
        };
    }

    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
