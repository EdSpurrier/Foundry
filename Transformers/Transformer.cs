using Foundry.Core;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Foundry.Transformers
{
    public abstract class Transformer : MonoBehaviour
    {
        [Title("Settings")]
        public bool active = true;

        [Title("Update")]
        public UpdateType updateType = UpdateType.LateUpdate;

        [Title("Start")]
        public bool initializeOnStart = true;
        public bool processOnStart = false;

        protected virtual void Awake()
        {
        }

        protected virtual void Start()
        {
            if (initializeOnStart)
            {
                Initialize();
            }

            if (processOnStart)
            {
                TryProcess();
            }
        }

        public virtual void Initialize()
        {
        }

        public void Activate()
        {
            active = true;
        }

        public void Deactivate()
        {
            active = false;
        }

        public void ProcessNow()
        {
            TryProcess();
        }

        private void FixedUpdate()
        {
            if (updateType == UpdateType.FixedUpdate)
            {
                TryProcess();
            }
        }

        private void Update()
        {
            if (updateType == UpdateType.Update)
            {
                TryProcess();
            }
        }

        private void LateUpdate()
        {
            if (updateType == UpdateType.LateUpdate)
            {
                TryProcess();
            }
        }

        private void TryProcess()
        {
            if (!active)
            {
                return;
            }

            if (!CanProcess())
            {
                return;
            }

            Process();
        }

        protected virtual bool CanProcess()
        {
            return true;
        }

        protected abstract void Process();
        
#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            ValidateData();
        }

        protected virtual void ValidateData()
        {
        }
#endif
    }
}