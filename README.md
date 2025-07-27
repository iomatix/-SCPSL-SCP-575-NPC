## SCP-575 NPC
[![Download Latest Release](https://img.shields.io/badge/Download-Latest%20Release-blue?style=for-the-badge)](https://github.com/iomatix/-SCPSL-SCP-575-NPC/releases/latest)
[![GitHub Downloads](https://img.shields.io/github/downloads/iomatix/-SCPSL-SCP-575-NPC/latest/total?sort=date&style=for-the-badge)](https://github.com/iomatix/-SCPSL-SCP-575-NPC/releases/latest)

## Dependencies:

- **SCPSL-AudioManagerAPI**: `https://github.com/iomatix/-SCPSL-AudioManagerAPI/releases`

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
debug: true
# Configuration settings for blackout mechanics.
blackout_config:
# The chance that a Round even has SCP-575 blackouts
  event_chance: 58
  # Enable or disable randomly timed blackout events.
  random_events: true
  # Delay before first event of each round.
  initial_delay: 95
  # Minimum blackout duration in seconds.
  duration_min: 120
  # Maximum blackout duration in seconds.
  duration_max: 280
  # Minimum delay between events in seconds.
  delay_min: 180
  # Maximum delay between events in seconds.
  delay_max: 435
  # Enable facility-wide blackout if no zones selected.
  enable_facility_blackout: true
  # Chance (%) of outage in Heavy Containment Zone.
  chance_heavy: 69
  # Chance (%) of outage in Light Containment Zone.
  chance_light: 25
  # Chance (%) of outage in Entrance Zone.
  chance_entrance: 55
  # Chance (%) of outage in Surface Zone.
  chance_surface: 12
  # Chance (%) of outage in unspecified zones.
  chance_other: 25
  # Use per-room chance settings instead of per-zone.
  use_per_room_chances: true
  # Disable Tesla gates during blackout.
  disable_teslas: true
  # Cancel nuke detonation during blackout.
  disable_nuke: false
  # Flicker lights when blackout starts.
  flicker_lights: true
  # Duration of initial light flickering in seconds.
  flicker_duration: 3.5
  # Frequency of light flickering.
  flicker_frequency: 1.25
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
  keter_action_delay: 12.75
  # Penetration modifier same as in FirearmsDamageHandler.
  keter_damage_penetration: 0.670000017
  # The modifier applied to velocity when players are damaged by SCP-575.
  keter_damage_velocity_modifier: 1.25
  # The minimum modifier applied to ragdolls when they were damaged by SCP-575.
  keter_force_min_modifier: 0.75
  # The maximum modifier applied to ragdolls when they were damaged by SCP-575.
  keter_force_max_modifier: 2.45000005
# Sanity system configuration.
sanity_config:
# Initial sanity value (0–100) on spawn.
  initial_sanity: 100
  # Base sanity decay rate per second.
  decay_rate_base: 0.109999999
  # Decay multiplier when SCP-575 is active.
  decay_multiplier_blackout: 1.64999998
  # Decay multiplier when player has no light source.
  decay_multiplier_darkness: 1.45000005
  # Passive sanity regen rate per second.
  passive_regen_rate: 0.0799999982
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
# Hints configuration settings.
hints_config:
# Inform players when affected by SCP-575 via hint messages.
  is_enabled_keter_hint: true
  # Inform players when thier sanity is affected.
  is_enabled_sanity_hint: true
  # Hint shown when player's sanity level decreases. {0} = current sanity value
  sanity_decreased_hint: 'Your sanity is decreasing!\n Sanity: {0:F1}. Find light sources or medical items to recover.'
  # Hint shown when player's sanity recovers from medical treatment. {0} = new sanity value
  sanity_increased_hint: 'Your sanity is recovering!\n Sanity: {0:F1} thanks to medical treatment!'
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
  # Cassie countdown before blackout.
  cassie_message_countdown: 'pitch_0.2 .g4 . .g4 pitch_1 door control system pitch_0.25 .g1 pitch_0.9 malfunction pitch_1 . initializing repair'
  # Time between sentence and countdown.
  time_between_sentence_and_start: 48.5999985
  # Time between blackout end and end message.
  time_between_sentence_and_end: 7
  # Cassie message at blackout start.
  cassie_message_start: 'pitch_0.25 .g4 . pitch_0.45 .g3 pitch_0.95 . ATTENTION . AN IMPORTANT . MESSAGE . pitch_0.98 the facility control system pitch_0.25 .g1 pitch_0.93 critical failure'
  # Cassie post-blackout-start message.
  cassie_post_message: 'pitch_0.72 jam_043_3 .g4 pitch_0.95 . ATTENTION . ATTENTION . please supply with light source or the results pitch_0.85 go in to be . pitch_0.8 grave'
  # Cassie message if no blackout occurs.
  cassie_message_wrong: '.g5 . Avoided the malfunction of the control system . .g3'
  # Cassie message at blackout end.
  cassie_message_end: 'pitch_0.45 .g4 pitch_0.65 . .g3 .g1 pitch_1.0 . IMPORTANT MESSAGE . pitch_0.98 the facility . door control system . is now . pitch_0.95 operational'
  # Message for facility-wide blackout.
  cassie_message_facility: 'The Facility black out .'
  # Message for Entrance Zone blackout.
  cassie_message_entrance: ''
  # Message for Light Containment Zone blackout.
  cassie_message_light: ''
  # Message for Heavy Containment Zone blackout.
  cassie_message_heavy: ''
  # Message for Surface Zone blackout.
  cassie_message_surface: ''
  # Message for unspecified zone blackout.
  cassie_message_other: 'pitch_0.75 .g6 pitch_0.25 jam_027_4 .g1 pitch_1.75 .g2 pitch_0.33 .g4 . .g4 . .g4 . pitch_0.25 S pitch_0.65 .g3'
  # Glitch chance per word in Cassie messages.
  glitch_chance: 4
  # Jam chance per word in Cassie messages.
  jam_chance: 3
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