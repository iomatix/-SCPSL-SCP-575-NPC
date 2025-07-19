## SCP-575 NPC
![GitHub Downloads (all assets, latest release)](https://img.shields.io/github/downloads/iomatix/-SCPSL-SCP-575/latest/total?sort=date&style=for-the-badge)

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
# Whether or not the plugin is enabled.
is_enabled: true
# The type of SCP-575 that will be used. Valid options: Npc
spawn_type: Npc
# The configs for NPC instances of SCP-575.
npc_config:
# Whether or not randomly timed events should occur. If false, all events will be at the same interval apart.
  random_events: true
  # Whether or not tesla gates should be disabled during blackouts.
  disable_teslas: true
  # Whether or not nuke detonation should be cancelled during blackouts.
  disable_nuke: false
  # The delay before the first event of each round, in seconds.
  initial_delay: 45
  # The minimum number of seconds a blackout event can last.
  duration_min: 95
  # The maximum number of seconds a blackout event can last. If RandomEvents is disabled, this will be the duration for every event.
  duration_max: 445
  # The minimum amount of seconds between each event.
  delay_min: 225
  # The maximum amount of seconds between each event. If RandomEvents is disabled, this will be the delay between every event.
  delay_max: 565
  # The percentage change that SCP-575 events will occur in any particular round.
  spawn_chance: 165
  # Whether or not people in dark rooms should take damage if they have no light source in their hand.
  enable_keter: true
  # Specifies whether SCP-575 can be terminated when all generators are engaged. If set to false, generator activation only halts SCP-575's behavior and resets its event state without killing it.
  is_npc_killable: false
  # Determines how kills by SCP-575 handle ragdolls. If set to false, a skeleton ragdoll is spawned instead of the default one. If set to true, no ragdoll is created upon death.
  disable_ragdolls: false
  # Whether or not to inform players about being damaged by SCP-575 via Broadcast messages.
  enable_keter_broadcast: false
  # Broadcast message shown when a player is damaged by SCP-575 if EnableKeterBroadcast is set to true.
  keter_broadcast: 'An entity lurking in the shadows has struck you! Arm yourself with a flashlight!'
  # Whether or not to enable effects triggered by SCP-575 on player hurt.
  enable_keter_on_deal_damage_effects: true
  # Enable 'Ensnared' effect when damaged by SCP-575.
  enable_effect_ensnared: true
  # Enable 'Flashed' effect when damaged by SCP-575.
  enable_effect_flashed: true
  # Enable 'Blurred' effect when damaged by SCP-575.
  enable_effect_blurred: true
  # Enable 'Deafened' effect when damaged by SCP-575.
  enable_effect_deafened: true
  # Enable 'AmnesiaVision' effect when damaged by SCP-575.
  enable_effect_amnesia_vision: true
  # Enable 'Sinkhole' effect when damaged by SCP-575.
  enable_effect_sinkhole: true
  # Enable 'Concussed' effect when damaged by SCP-575.
  enable_effect_concussed: true
  # Enable 'Blindness' effect when damaged by SCP-575.
  enable_effect_blindness: true
  # Enable 'Burned' effect when damaged by SCP-575.
  enable_effect_burned: false
  # Enable 'AmnesiaItems' effect when damaged by SCP-575.
  enable_effect_amnesia_items: false
  # Enable 'Stained' effect when damaged by SCP-575.
  enable_effect_stained: true
  # Enable 'Asphyxiated' effect when damaged by SCP-575.
  enable_effect_asphyxiated: false
  # Enable 'Bleeding' effect when damaged by SCP-575.
  enable_effect_bleeding: false
  # Enable 'Disabled' effect when damaged by SCP-575.
  enable_effect_disabled: true
  # Enable 'Exhausted' effect when damaged by SCP-575.
  enable_effect_exhausted: true
  # Enable 'Traumatized' effect when damaged by SCP-575.
  enable_effect_traumatized: true
  # Whether or not SCP-575's sound effect should happen on the client damaged by the entity.
  enable_scream_sound: true
  # Flicker lights when the event starts.
  flicker_lights: true
  # The number of seconds a first flickering lasts.
  flicker_lights_duration: 4.25
  # Base damage per each stack of delay. Tha damage is inflicted if EnableKeter is set to true.
  keter_damage: 35
  # Penetration modifier same as in FirearmsDamageHandler.
  keter_damage_penetration: 0.649999976
  # The delay of receiving damage.
  keter_damage_delay: 12
  # The minimum modifier applied to ragdolls when they were damaged by SCP-575.
  keter_force_min_modifier: 0.75
  # The maximum modifier applied to ragdolls when they were damaged by SCP-575.
  keter_force_max_modifier: 2.5
  # The modifier applied to velocity when players are damaged by SCP-575.
  keter_damage_velocity_modifier: 1.25
  # Name displayed in player's death information.
  killed_by: 'SCP-575'
  # Killed by message
  killed_by_message: 'Shredded apart by SCP-575'
  # Ragdoll death information.
  ragdoll_inspect_text: 'Flesh stripped by shadow tendrils, leaving a shadowy skeleton.'
  # Glitch chance during message per word in CASSIE sentence.
  glitch_chance: 8
  # Jam chance during message per word in CASSIE sentence.
  jam_chance: 6
  # Message said by Cassie if no blackout occurs
  cassie_message_wrong: '. the facility system has avoided a system failure . .g5 I am sorry for a .g3 . false alert .'
  # Message said by Cassie when a blackout starts.
  cassie_message_start: 'pitch_0.75 .g6 pitch_0.25 jam_027_4 .g1 pitch_1.75 .g2 pitch_0.33 .g4 . .g4 . .g4 . pitch_0.95 .g3 . ATTENTION . ATTENTION . CASSIESYSTEM ALERT . .g2 . pitch_0.35 .g3 pitch_1.0 overcharge of the facility . power system . caused by unknown pitch_0.55 jam_045_7 virus . pitch_1.1 is extremely . dangersh . commencing . pitch_1.15 safe pitch_1.25 t pitch_1.00 protocol . .g1 . pitch_0.98 potential electric power pitch_0.9 outage . pitch_0.97 in . pitch_0.95 3 . pitch_0.9 2 . pitch_0.85 jam_90_4 1 . GOING DARK pitch_0.3 .g3 pitch_0.25 .g2'
  # The time between the sentence and the 3 . 2 . 1 announcement
  time_between_sentence_and_start: 48.5999985
  # Message said by Cassie just after the blackout.
  cassie_post_message: 'pitch_0.72 jam_043_3 .g4 pitch_0.95 . ATTENTION . ATTENTION . please supply with light source or the results pitch_0.85 go in to be . pitch_0.8 grave'
  # The time between the sentence and the blockout end.
  time_between_sentence_and_end: 7
  # Message said by Cassie after CassiePostMessage if outage gonna occure at whole site.
  cassie_message_facility: 'The Facility black out .'
  # Message said by Cassie after CassiePostMessage if outage gonna occure at the Entrance Zone.
  cassie_message_entrance: ''
  # Message said by Cassie after CassiePostMessage if outage gonna occure at the Light Containment Zone.
  cassie_message_light: ''
  # Message said by Cassie after CassiePostMessage if outage gonna occure at the Heavy Containment Zone.
  cassie_message_heavy: ''
  # Message said by Cassie after CassiePostMessage if outage gonna occure at the entrance zone.
  cassie_message_surface: ''
  # Message said by Cassie after CassiePostMessage if outage gonna occure at unknown type of zones or unspecified zones.
  cassie_message_other: 'pitch_0.75 .g6 pitch_0.25 jam_027_4 .g1 pitch_1.75 .g2 pitch_0.33 .g4 . .g4 . .g4 . pitch_0.25 S pitch_0.65 .g3'
  # The sound CASSIE will make during a blackout.
  cassie_keter: 'jam_66_3 pitch_0.15 .g2 . pitch_0.5 .g5 . pitch_0.15 H . A .g3 . . pitch_0.15 Y pitch_0.3 O . pitch_0.57 .g1 . pitch_0.35 jam_050_2 .g6 jam_048_3 X . . pitch_0.25 jam_017_1 S . jam_050_1 .g3'
  # Play horror ambient sound on blackout.
  keter_ambient: true
  # The message CASSIE will say when a blackout ends.
  cassie_message_end: 'pitch_0.25 .g4 pitch_0.45 . .g3 .g4 pitch_0.98 . IMPORTANT MESSAGE . pitch_0.95 .g3 .g5 the facility power system is now . pitch_0.93 operational . .g3'
  # Should cassie clear the messeage and broadcast cue before important message to prevent spam?
  cassie_message_clear_before_important: true
  # A blackout in the whole facility will occur if none of the zones is selected randomly and EnableFacilityBlackout is set to true.
  enable_facility_blackout: true
  # Percentage chance of an outage at the Heavy Containment Zone during the blackout.
  chance_heavy: 65
  # Percentage chance of an outage at the Heavy Containment Zone during the blackout.
  chance_light: 25
  # Percentage chance of an outage at the Entrance Zone during the blackout.
  chance_entrance: 45
  # Percentage chance of an outage at the Surface Zone during the blackout.
  chance_surface: 8
  # Percentage chance of an outage at an unknown and unspecified type of zone during the blackout.
  chance_other: 22
  # Change this to true if want to use per room probability settings isntead of per zone settings. The script will check all rooms in the specified zone with its probability.
  use_per_room_chances: true
# Whether of not debug messages are displayed in the console.
debug: true
```
