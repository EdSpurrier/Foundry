using FrameCoreU.Events;
using FrameCoreU.Unity;
using FrameCoreU.Utils;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Actions
{
    [System.Serializable]
    public class Spawn : FrameAction
    {
        public override string ActionType => "Spawn";
        
        [HorizontalGroup("SpawnSplit", 0.35f)]
        [HideLabel]
        public Transform prefab;

        [InlineButton("AddToPool")]
        [EnumPaging]
        [EnumToggleButtons]
        [HorizontalGroup("SpawnSplit", 0.65f)]
        [HideLabel]
        public SpawnType spawnPositionType;

        public enum SpawnType
        {
            Preset,
            ReferenceTransform,
        }

        [FoldoutGroup("Spawn")]
        public Transform parent;

        [FoldoutGroup("Spawn")]
        [ShowIf("spawnPositionType", SpawnType.Preset)]
        public Vector3 position;

        [FoldoutGroup("Spawn")]
        [ShowIf("spawnPositionType", SpawnType.Preset)]
        public Vector3 rotation;

        [FoldoutGroup("Spawn")]
        [ShowIf("spawnPositionType", SpawnType.ReferenceTransform)]
        public Transform spawnPointReference;

        [FoldoutGroup("Spawn")]
        [ShowIf("spawnPositionType", SpawnType.ReferenceTransform)]
        public Transform spawnRotationReference;

        private void AddToPool()
        {
            if (prefab == null)
            {
                Debug.LogWarning("Add To Pool failed: prefab is null");
                return;
            }
            EditorInteractions.AddToPool(prefab);
        }

        protected override void Activate()
        {
            if (prefab == null)
            {
                Debug.LogWarning("Spawn Action failed: prefab is null");
                return;
            }

            Vector3 spawnPosition = position;
            Quaternion spawnRotation = Quaternion.Euler(rotation);

            if (spawnPositionType == SpawnType.ReferenceTransform)
            {
                if (spawnPointReference == null)
                {
                    Debug.LogWarning("Spawn Action failed: spawnPointReference is null");
                    return;
                }

                spawnPosition = spawnPointReference.position;

                if (spawnRotationReference != null)
                {
                    spawnRotation = spawnRotationReference.rotation;
                }
            }

            GameObject spawn = prefab.SpawnObject(spawnPosition, spawnRotation);

            if (parent != null)
            {
                spawn.transform.SetParent(parent);
            }
        }
    }
}