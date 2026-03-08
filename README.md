## SCP-575 NPC
[![Download Latest Release](https://img.shields.io/badge/Download-Latest%20Release-blue?style=for-the-badge)](https://github.com/iomatix/-SCPSL-SCP-575-NPC/releases/latest)
[![GitHub Downloads](https://img.shields.io/github/downloads/iomatix/-SCPSL-SCP-575-NPC/latest/total?sort=date&style=for-the-badge)](https://github.com/iomatix/-SCPSL-SCP-575-NPC/releases/latest)

## Dependencies:

- **[SCPSL-AudioManagerAPI](https://github.com/iomatix/-SCPSL-AudioManagerAPI/tree/main/AudioManagerAPI)**: `https://github.com/iomatix/-SCPSL-AudioManagerAPI/releases`

## Supporting Development

My mods are **always free to use**.

If you appreciate my work, you can support me by [buying me a coffee](https://buymeacoffee.com/iomatix).


## Contributors

<a href="https://github.com/iomatix/-SCPSL-SCP-575-NPC/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=iomatix/-SCPSL-SCP-575-NPC" />
</a>

## Release – SCP-575 NPC for Exiled Mod

This is the very first official build of the SCP-575 NPC mod, hosted in its own independent repo to fully decouple from the original fork.

**What we did**
- Mirrored our entire commit history into a fresh repository named `-SCPSL-SCP-575-NPC`
- Detached from the upstream `eDexiam/SCP-575` fork to remove “ahead/behind” noise
- Retained all feature branches and tags for continuity

**Why we did it**
- Avoid merge-conflicts and dependency drift with the main fork - our code has diverged completely
- Enable a clean slate for NPC-specific gameplay (blackout events, flicker logic, helper utilities, etc.)
- Simplify future maintenance and community contributions

### Example Config
```
# Enable or disable SCP-575.
is_enabled: true
# Enable debug logging.
debug: false
# Configuration settings for blackout mechanics.
blackout_config:
# The chance that a Round even has SCP-575 blackouts
  event_chance: 55
  # Enable or disable randomly timed blackout events.
  random_events: true
  # Delay before first event of each round.
  initial_delay: 300
  # Minimum blackout duration in seconds.
  duration_min: 30
  # Maximum blackout duration in seconds.
  duration_max: 90
  # Minimum delay between events in seconds.
  delay_min: 180
  # Maximum delay between events in seconds.
  delay_max: 500
  # Enable facility-wide blackout if no zones selected.
  enable_facility_blackout: true
  # Chance (%) of outage in Heavy Containment Zone.
  chance_heavy: 99
  # Chance (%) of outage in Light Containment Zone.
  chance_light: 45
  # Chance (%) of outage in Entrance Zone.
  chance_entrance: 65
  # Chance (%) of outage in Surface Zone.
  chance_surface: 25
  # Chance (%) of outage in unspecified zones.
  chance_other: 0
  # Elevator lockdown probability (%) when a connected room loses power
  elevator_lockdown_probability: 35
  # Use per-room chance settings instead of per-zone.
  use_per_room_chances: false
  # Disable Tesla gates during blackout.
  disable_teslas: true
  # Cancel nuke detonation during blackout.
  disable_nuke: true
  # Flicker lights when blackout starts.
  flicker_lights: true
  # Duration of initial light flickering in seconds.
  flicker_duration: 1.5
  # Frequency of light flickering.
  flicker_frequency: 1.5
  # Red channel of lights color during blackout.
  lights_color_r: 0.899999976
  # Green channel of lights color during blackout.
  lights_color_g: 0.0500000007
  # Blue channel of lights color during blackout.
  lights_color_b: 0.200000003
# Configuration settings for SCP-575 NPC behaviors.
npc_config:
# Specifies whether SCP-575 can be terminated when all generators are engaged.
  is_npc_killable: false
  # Determines whether to disable ragdolls for SCP-575 kills.
  disable_ragdolls: false
  # The delay of receiving damage.
  keter_action_delay: 13.8500004
  # Penetration modifier same as in FirearmsDamageHandler.
  keter_damage_penetration: 0.75
  # The modifier applied to velocity when players are damaged by SCP-575.
  keter_damage_velocity_modifier: 1.25
  # The minimum modifier applied to ragdolls when they were damaged by SCP-575.
  keter_force_min_modifier: 0.75
  # The maximum modifier applied to ragdolls when they were damaged by SCP-575.
  keter_force_max_modifier: 2.3499999
# Sanity system configuration.
sanity_config:
# Initial sanity value (0–100) on spawn.
  initial_sanity: 100
  # Base sanity decay rate per second.
  decay_rate_base: 0.119999997
  # Decay multiplier when SCP-575 is active.
  decay_multiplier_blackout: 1.45000005
  # Decay multiplier when player has no light source.
  decay_multiplier_darkness: 1.45000005
  # Passive sanity regen rate per second.
  passive_regen_rate: 0.075000003
  # Minimum sanity restore percent from medical pills.
  pills_restore_min: 15
  # Maximum sanity restore percent from medical pills.
  pills_restore_max: 35
  # Minimum sanity restore percent from SCP-500.
  scp500_restore_min: 75
  # Maximum sanity restore percent from SCP-500.
  scp500_restore_max: 100
  # Stages of sanity and their associated effects.
  sanity_stages:
  -
  # Min sanity % to activate this stage.
    min_threshold: 75
    # Max sanity % to activate this stage.
    max_threshold: 100
    # Damage to apply on SCP-575 strike at this sanity level.
    damage_on_strike: 0
    # Additional damage to apply on SCP-575 strike at this sanity level per each active stack of the blackout event.
    additional_damage_per_stack: 0
    # Determines whether negative sanity effects should be applied even when the player is holding a lightsource in the room with lights off.
    override_light_source_sanity_protection: false
    # List of effects to apply to the player during this sanity stage.
    effects:
    -
    # Specifies the status effect type to apply.
      effect_type: SilentWalk
      # Duration of the effect in seconds.
      duration: 5
      # Intensity level of the status effect.
      intensity: 3
    -
    # Specifies the status effect type to apply.
      effect_type: Slowness
      # Duration of the effect in seconds.
      duration: 1.25
      # Intensity level of the status effect.
      intensity: 30
    -
    # Specifies the status effect type to apply.
      effect_type: Blurred
      # Duration of the effect in seconds.
      duration: 1
      # Intensity level of the status effect.
      intensity: 1
    -
    # Specifies the status effect type to apply.
      effect_type: Concussed
      # Duration of the effect in seconds.
      duration: 2
      # Intensity level of the status effect.
      intensity: 1
    -
    # Specifies the status effect type to apply.
      effect_type: Blindness
      # Duration of the effect in seconds.
      duration: 0.449999988
      # Intensity level of the status effect.
      intensity: 1
    -
    # Specifies the status effect type to apply.
      effect_type: Deafened
      # Duration of the effect in seconds.
      duration: 0.550000012
      # Intensity level of the status effect.
      intensity: 1
    -
    # Specifies the status effect type to apply.
      effect_type: Flashed
      # Duration of the effect in seconds.
      duration: 0.150000006
      # Intensity level of the status effect.
      intensity: 1
  -
  # Min sanity % to activate this stage.
    min_threshold: 50
    # Max sanity % to activate this stage.
    max_threshold: 75
    # Damage to apply on SCP-575 strike at this sanity level.
    damage_on_strike: 3
    # Additional damage to apply on SCP-575 strike at this sanity level per each active stack of the blackout event.
    additional_damage_per_stack: 2
    # Determines whether negative sanity effects should be applied even when the player is holding a lightsource in the room with lights off.
    override_light_source_sanity_protection: false
    # List of effects to apply to the player during this sanity stage.
    effects:
    -
    # Specifies the status effect type to apply.
      effect_type: SilentWalk
      # Duration of the effect in seconds.
      duration: 5
      # Intensity level of the status effect.
      intensity: 7
    -
    # Specifies the status effect type to apply.
      effect_type: Slowness
      # Duration of the effect in seconds.
      duration: 1.64999998
      # Intensity level of the status effect.
      intensity: 40
    -
    # Specifies the status effect type to apply.
      effect_type: Disabled
      # Duration of the effect in seconds.
      duration: 7
      # Intensity level of the status effect.
      intensity: 1
    -
    # Specifies the status effect type to apply.
      effect_type: Traumatized
      # Duration of the effect in seconds.
      duration: 7
      # Intensity level of the status effect.
      intensity: 1
    -
    # Specifies the status effect type to apply.
      effect_type: Exhausted
      # Duration of the effect in seconds.
      duration: 1.5
      # Intensity level of the status effect.
      intensity: 1
    -
    # Specifies the status effect type to apply.
      effect_type: Blurred
      # Duration of the effect in seconds.
      duration: 2
      # Intensity level of the status effect.
      intensity: 1
    -
    # Specifies the status effect type to apply.
      effect_type: Concussed
      # Duration of the effect in seconds.
      duration: 3
      # Intensity level of the status effect.
      intensity: 1
    -
    # Specifies the status effect type to apply.
      effect_type: Blindness
      # Duration of the effect in seconds.
      duration: 0.75
      # Intensity level of the status effect.
      intensity: 1
    -
    # Specifies the status effect type to apply.
      effect_type: Deafened
      # Duration of the effect in seconds.
      duration: 1.25
      # Intensity level of the status effect.
      intensity: 1
    -
    # Specifies the status effect type to apply.
      effect_type: Flashed
      # Duration of the effect in seconds.
      duration: 0.25
      # Intensity level of the status effect.
      intensity: 1
  -
  # Min sanity % to activate this stage.
    min_threshold: 25
    # Max sanity % to activate this stage.
    max_threshold: 50
    # Damage to apply on SCP-575 strike at this sanity level.
    damage_on_strike: 6
    # Additional damage to apply on SCP-575 strike at this sanity level per each active stack of the blackout event.
    additional_damage_per_stack: 5
    # Determines whether negative sanity effects should be applied even when the player is holding a lightsource in the room with lights off.
    override_light_source_sanity_protection: false
    # List of effects to apply to the player during this sanity stage.
    effects:
    -
    # Specifies the status effect type to apply.
      effect_type: SilentWalk
      # Duration of the effect in seconds.
      duration: 5
      # Intensity level of the status effect.
      intensity: 9
    -
    # Specifies the status effect type to apply.
      effect_type: Slowness
      # Duration of the effect in seconds.
      duration: 2.45000005
      # Intensity level of the status effect.
      intensity: 55
    -
    # Specifies the status effect type to apply.
      effect_type: Disabled
      # Duration of the effect in seconds.
      duration: 9
      # Intensity level of the status effect.
      intensity: 1
    -
    # Specifies the status effect type to apply.
      effect_type: Traumatized
      # Duration of the effect in seconds.
      duration: 9
      # Intensity level of the status effect.
      intensity: 1
    -
    # Specifies the status effect type to apply.
      effect_type: Exhausted
      # Duration of the effect in seconds.
      duration: 2.5
      # Intensity level of the status effect.
      intensity: 1
    -
    # Specifies the status effect type to apply.
      effect_type: Blurred
      # Duration of the effect in seconds.
      duration: 5
      # Intensity level of the status effect.
      intensity: 1
    -
    # Specifies the status effect type to apply.
      effect_type: Concussed
      # Duration of the effect in seconds.
      duration: 8
      # Intensity level of the status effect.
      intensity: 1
    -
    # Specifies the status effect type to apply.
      effect_type: Blindness
      # Duration of the effect in seconds.
      duration: 1.25
      # Intensity level of the status effect.
      intensity: 1
    -
    # Specifies the status effect type to apply.
      effect_type: Deafened
      # Duration of the effect in seconds.
      duration: 2.25
      # Intensity level of the status effect.
      intensity: 1
    -
    # Specifies the status effect type to apply.
      effect_type: Flashed
      # Duration of the effect in seconds.
      duration: 0.349999994
      # Intensity level of the status effect.
      intensity: 1
  -
  # Min sanity % to activate this stage.
    min_threshold: 0
    # Max sanity % to activate this stage.
    max_threshold: 25
    # Damage to apply on SCP-575 strike at this sanity level.
    damage_on_strike: 9
    # Additional damage to apply on SCP-575 strike at this sanity level per each active stack of the blackout event.
    additional_damage_per_stack: 8
    # Determines whether negative sanity effects should be applied even when the player is holding a lightsource in the room with lights off.
    override_light_source_sanity_protection: true
    # List of effects to apply to the player during this sanity stage.
    effects:
    -
    # Specifies the status effect type to apply.
      effect_type: SilentWalk
      # Duration of the effect in seconds.
      duration: 5
      # Intensity level of the status effect.
      intensity: 10
    -
    # Specifies the status effect type to apply.
      effect_type: Slowness
      # Duration of the effect in seconds.
      duration: 3.25
      # Intensity level of the status effect.
      intensity: 70
    -
    # Specifies the status effect type to apply.
      effect_type: Disabled
      # Duration of the effect in seconds.
      duration: 15
      # Intensity level of the status effect.
      intensity: 1
    -
    # Specifies the status effect type to apply.
      effect_type: Traumatized
      # Duration of the effect in seconds.
      duration: 15
      # Intensity level of the status effect.
      intensity: 1
    -
    # Specifies the status effect type to apply.
      effect_type: Exhausted
      # Duration of the effect in seconds.
      duration: 4.75
      # Intensity level of the status effect.
      intensity: 1
    -
    # Specifies the status effect type to apply.
      effect_type: Blurred
      # Duration of the effect in seconds.
      duration: 7
      # Intensity level of the status effect.
      intensity: 1
    -
    # Specifies the status effect type to apply.
      effect_type: Concussed
      # Duration of the effect in seconds.
      duration: 10
      # Intensity level of the status effect.
      intensity: 1
    -
    # Specifies the status effect type to apply.
      effect_type: Blindness
      # Duration of the effect in seconds.
      duration: 1.45000005
      # Intensity level of the status effect.
      intensity: 1
    -
    # Specifies the status effect type to apply.
      effect_type: Deafened
      # Duration of the effect in seconds.
      duration: 3.5
      # Intensity level of the status effect.
      intensity: 1
    -
    # Specifies the status effect type to apply.
      effect_type: Flashed
      # Duration of the effect in seconds.
      duration: 0.649999976
      # Intensity level of the status effect.
      intensity: 1
# Light source system configuration.
lightsource_config:
# Cooldown on the light source triggered on hit by SCP-575.
  keter_lightsource_cooldown: 7.25
  # Minimum number of flickers caused by SCP-575.
  min_flicker_count: 3
  # Maximum number of flickers caused by SCP-575.
  max_flicker_count: 11
  # Minimum duration of the flicker effect in milliseconds.
  min_flicker_duration_ms: 1500
  # Maximum duration of the flicker effect in milliseconds.
  max_flicker_duration_ms: 2500
# Hints configuration settings.
hints_config:
# Inform players when affected by SCP-575 via hint messages.
  is_enabled_keter_hint: true
  # Inform players when thier sanity is affected.
  is_enabled_sanity_hint: true
  # Hint shown when player's sanity level decreases. {0} = current sanity value
  sanity_decreased_hint: |-
    Your sanity is decreasing!
     Sanity: {0}. Find light sources or medical items to recover.
  # Hint shown when player's sanity recovers from medical treatment. {0} = new sanity value
  sanity_increased_hint: |-
    Your sanity is recovering!
     Sanity: {0} thanks to medical treatment!
  # Hint shown when player is affected by SCP-575.
  keter_hint: 'You were affected by actions of SCP-575! Equip a flashlight!'
  # Inform players about cooldown of light emitter.
  is_enabled_light_emitter_cooldown_hint: true
  # Hint shown when using light source on cooldown.
  light_emitter_cooldown_hint: 'Your light source is on cooldown!'
  # Hint shown when light source is disabled by SCP-575.
  light_emitter_disabled_hint: 'Your light source has been disabled!'
  # Name displayed in death info.
  killed_by: 'SCP-575'
  # Message displayed when killed by SCP-575.
  killed_by_message: 'Shredded apart by SCP-575'
  # Ragdoll inspection text after death by SCP-575.
  ragdoll_inspect_text: 'Flesh stripped by shadow tendrils, leaving a shadowy skeleton.'
# Cassie announcement configuration settings.
cassie_config:
# Enable Cassie countdown announcement.
  is_countdown_enabled: true
  # Clear message queue before important messages.
  cassie_message_clear_before_important: true
  # Priority for important Cassie messages.
  cassie_message_priority: 3.0999999
  # Cassie countdown before blackout.
  cassie_message_countdown: 'pitch_0.9 power failure . pitch_1'
  # Time between sentence and countdown.
  time_between_sentence_and_start: 5
  # Time between blackout end and end message.
  time_between_sentence_and_end: 3
  # Cassie message at blackout start.
  cassie_message_start: 'warning . facility power grid failure imminent . anomalous activity detected .'
  # Cassie post-blackout-start message.
  cassie_post_message: 'pitch_0.8 darkness is no longer safe . stay in light areas . pitch_1'
  # Cassie message if no blackout occurs.
  cassie_message_wrong: 'pitch_1.1 . power grid stabilized . false alert detected . pitch_1'
  # Cassie message at blackout end.
  cassie_message_end: 'pitch_1.15 facility power system now operational . pitch_1'
  # Message for facility-wide blackout.
  cassie_message_facility: 'The Facility .'
  # Message for Entrance Zone blackout.
  cassie_message_entrance: 'The Entrance Zone .'
  # Message for Light Containment Zone blackout.
  cassie_message_light: 'The Light Containment Zone .'
  # Message for Heavy Containment Zone blackout.
  cassie_message_heavy: 'The Heavy Containment Zone.'
  # Message for Surface Zone blackout.
  cassie_message_surface: 'The Surface .'
  # Message for unspecified zone blackout.
  cassie_message_other: '. pitch_0.35 .g6 pitch_0.95 the malfunction is Unspecified .'
  # Glitch chance per word in Cassie messages.
  glitch_chance: 15
  # Jam chance per word in Cassie messages.
  jam_chance: 10
  # Cassie Keter sound during blackout.
  cassie_keter: 'pitch_0.15 .g7'
# Audio system configuration.
audio_config:
# The cooldown time in seconds for global scream audio playback. Must be positive.
  global_scream_cooldown: 35
  # The default duration in seconds for audio fade-in and fade-out effects. Must be non-negative.
  default_fade_duration: 1
# Interval for automatic cleanup of event handlers (seconds).
handler_cleanup_interval: 90
```
