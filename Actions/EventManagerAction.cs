using System.Collections.Generic;
using FrameCoreU.Events;
using FrameCoreU.Events.Library;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Actions
{
    // Starts EventManager chains from any event - e.g. a lever's On Turned On kicks off a "door opens, camera pans,
    // platform slides out" sequence authored once on an EventManager, so several switches can share it
    [System.Serializable]
    public class EventManagerAction : FrameAction
    {
        public enum Source
        {
            EventManagers,  // the EventManagers listed
            ByName,         // the one with this name in an EventManagerLibrary
            Random          // a random one from an EventManagerLibrary
        }

        public override string ActionType => "Event Manager";

        [Title("Event Manager Settings")]
        [EnumToggleButtons]
        public Source source = Source.EventManagers;

        [ShowIf(nameof(source), Source.EventManagers)]
        [ListDrawerSettings(DefaultExpandedState = true)]
        public List<EventManager> eventManagers = new();

        [HideIf(nameof(source), Source.EventManagers)]
        public EventManagerLibrary library;

        [ShowIf(nameof(source), Source.ByName)]
        [Tooltip("The entry's Event Name in the library.")]
        public string eventName;

        [ShowIf(nameof(source), Source.EventManagers)]
        [Tooltip("Let a Single Use EventManager run again every time this action fires (it's normally blocked after its first run). Off = it respects the EventManager's own Single Use setting.")]
        public bool allowRepeat;

        protected override void Activate()
        {
            switch (source)
            {
                case Source.ByName:
                    if (library == null) { Warn("No EventManagerLibrary set"); return; }
                    library.TriggerEventManager(eventName);
                    return;

                case Source.Random:
                    if (library == null) { Warn("No EventManagerLibrary set"); return; }
                    library.TriggerRandomEventManager();
                    return;
            }

            if (eventManagers == null || eventManagers.Count == 0)
            {
                Warn("No EventManagers set");
                return;
            }

            foreach (EventManager eventManager in eventManagers)
            {
                if (eventManager == null) continue;

                if (allowRepeat)
                    eventManager.activated = false;

                eventManager.Activate();
            }
        }

        private void Warn(string message)
        {
            Debug.LogWarning($"[EventManagerAction] {message} - {actionName}");
        }
    }
}
