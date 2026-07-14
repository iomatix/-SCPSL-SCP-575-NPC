using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.ServerEvents;
using LabApi.Events.CustomHandlers;
using LabApi.Extensions;
using LabApi.Features.Wrappers;
using MapGeneration;
using MEC;
using SCP_575.Shared.Audio.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SCP_575.Handlers
{
    /// <summary>
    /// Intercepts facility elevator state updates and client interactions to route electrical grid failures.
    /// </summary>
    public class ElevatorHandler : CustomEventsHandler
    {
        #region Fields & Instance Registries
        private readonly Plugin _plugin;
        private readonly Dictionary<Elevator, CoroutineHandle> _activeFlickers = new();
        private readonly object _lock = new();
        #endregion

        #region Constructor
        public ElevatorHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        }
        #endregion

        #region Events Cleanups
        public override void OnServerRoundEnded(RoundEndedEventArgs ev) => ClearAllFlickers();
        public override void OnServerWaitingForPlayers() => ClearAllFlickers();
        #endregion

        #region Event Overrides
        public override void OnPlayerInteractingElevator(PlayerInteractingElevatorEventArgs ev)
        {
            if (!_plugin.IsEventActive || ev?.Player?.GameObject is null || ev.Elevator is null) return;

            if (_plugin.NpcLogic.IsBlackoutActive)
            {
                _plugin.AudioManager?.PlayAtPosition(AudioKey.LightShortCircuit, ev.Player.Position, isTransient: true, sourcePlayer: ev.Player);
            }
        }

        public override void OnServerElevatorSequenceChanged(ElevatorSequenceChangedEventArgs ev)
        {
            if (!_plugin.IsEventActive || ev?.Elevator is null) return;

           FacilityZone targetZone = ev.Elevator.GetZone();

            if (targetZone == FacilityZone.None) return;

            string flickerTag = $"ElevatorFlicker_{ev.Elevator.GetHashCode()}";

            if (_plugin.NpcLogic.IsZoneUnderBlackout(targetZone))
            {
                lock (_lock)
                {
                    // Thread Isolation: Deterministically purge both outer and inner sequence streams
                    flickerTag.Kill();
                    if (_activeFlickers.TryGetValue(ev.Elevator, out CoroutineHandle oldHandle))
                    {
                        Timing.KillCoroutines(oldHandle);
                    }

                    _activeFlickers[ev.Elevator] = Timing.RunCoroutine(RunElevatorDarknessSequence(ev.Elevator, flickerTag));
                }
            }
            else
            {
                lock (_lock)
                {
                    flickerTag.Kill();
                    if (_activeFlickers.TryGetValue(ev.Elevator, out CoroutineHandle handle))
                    {
                        Timing.KillCoroutines(handle);
                        _activeFlickers.Remove(ev.Elevator);
                    }
                }
                ev.Elevator.TurnOnLights();
            }
        }
        #endregion

        #region Isolated Sequence Engine
        private IEnumerator<float> RunElevatorDarknessSequence(Elevator elevator, string flickerTag)
        {
            if (_plugin.Blackout.FlickerLights && !elevator.AreLightsOff())
            {
                // Core Execution Fix: Capture the CoroutineHandle to safely feed it into WaitUntilDone
                CoroutineHandle handle = Timing.RunCoroutine(elevator.FlickerElevatorLightsCoroutine(_plugin.Blackout.FlickerDuration, _plugin.Blackout.FlickerFrequency), flickerTag);
                yield return Timing.WaitUntilDone(handle);
            }

            elevator.TurnOffLights(_plugin.Blackout.DurationMax);
        }

        /// <summary>
        /// Flushes the active dictionary tracking map and aborts all running cabin thread streams instantly.
        /// </summary>
        public void ClearAllFlickers()
        {
            lock (_lock)
            {
                foreach (Elevator elevator in _activeFlickers.Keys)
                {
                    $"ElevatorFlicker_{elevator.GetHashCode()}".Kill();
                }
                foreach (CoroutineHandle handle in _activeFlickers.Values)
                {
                    Timing.KillCoroutines(handle);
                }
                _activeFlickers.Clear();
            }
        }
        #endregion
    }
}