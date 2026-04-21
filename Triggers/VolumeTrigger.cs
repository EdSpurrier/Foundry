using System;
using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VolumeTrigger : MonoBehaviour
{
    [Title("Volume Trigger")]
    public LayerMaskNames layerMask;
    public MaxMinInt volumeBoundsInclusive = new MaxMinInt {
        min = 1,
        max = 10000
    };

    public bool activated = false;

    public bool enterEvents = false;
    [ShowIf("enterEvents")]
    [FoldoutGroup("Enter Trigger")]
    [HideLabel]
    public FrameCoreEvent enterEvent;


    public bool exitEvents = false;
    [ShowIf("exitEvents")]
    [FoldoutGroup("Exit Trigger")]
    [HideLabel]
    public FrameCoreEvent exitEvent;



    [FoldoutGroup("Objects In Trigger")]
    public List<GameObject> gameObjectsInTrigger;

    [Title("System")]
    [HideLabel]
    public DeBugger debug;

    
    
    public int ObjectsInTrigger()
    {
        return gameObjectsInTrigger.Count;
    }

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
        TriggerEnter(other.gameObject);
    }


    private void OnTriggerEnter2D(Collider2D other)
    {
        TriggerEnter(other.gameObject);
    }

    private void TriggerEnter(GameObject gameObject)
    {
        if (CheckIfInTrigger(gameObject))
        {
            debug.Log("Object Entered & Accepted = " + gameObject.name);
            
            if (activated)
            {
                return;
            };

            if (gameObjectsInTrigger.Count.BetweenRangeInt(volumeBoundsInclusive.min, volumeBoundsInclusive.max, true) )
            {
                debug.Log("Trigger Enter Activated");
                enterEvent.Activate();
                activated = true;
            };
        };
    }

    private void OnTriggerExit(Collider other)
    {
        debug.Log("3D Trigger Exit");
        TriggerExit(other.gameObject);
    }
    
    private void OnTriggerExit2D(Collider2D other)
    {
        debug.Log("2D Trigger Exit");
        TriggerExit(other.gameObject);
    }

    private void TriggerExit(GameObject gameObject)
    {
        if (gameObjectsInTrigger.Contains(gameObject))
        {
            gameObjectsInTrigger.Remove(gameObject);

            debug.Log("Object Exited & Removed = " + gameObject.name);

            if (!activated)
            {
                return;
            };

            if ( !gameObjectsInTrigger.Count.BetweenRangeInt(volumeBoundsInclusive.min, volumeBoundsInclusive.max, true) )
            {
                debug.Log("Trigger Exit Activated");
                if(exitEvents)       exitEvent.Activate();
                activated = false;
            };
        };
    }
}
