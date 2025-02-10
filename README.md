## SCP-575 6.3.0
![GitHub Downloads (all assets, latest release)](https://img.shields.io/github/downloads/iomatix/-SCPSL-SCP-575/latest/total?sort=date&style=for-the-badge)



## Contributors

<a href="https://github.com/iomatix/-SCPSL-SCP-575/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=iomatix/-SCPSL-SCP-575" />
</a>

### Configs
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
  duration_min: 90
  # The maximum number of seconds a blackout event can last. If RandomEvents is disabled, this will be the duration for every event.
  duration_max: 320
  # The minimum amount of seconds between each event.
  delay_min: 240
  # The maximum amount of seconds between each event. If RandomEvents is disabled, this will be the delay between every event.
  delay_max: 580
  # The percentage change that SCP-575 events will occur in any particular round.
  spawn_chance: 75
  # Whether or not people in dark rooms should take damage if they have no light source in their hand.
  enable_keter: true
  # Broadcast shown when a player is damaged by SCP-575.
  keter_broadcast:
  # The broadcast content
    content: 'You were damaged by SCP-575! Equip a flashlight!'
    # The broadcast duration
    duration: 5
    # The broadcast type
    type: Normal
    # Indicates whether the broadcast should be shown
    show: false
  # Whether or not SCP-575's "roar" should happen after a blackout starts.
  voice: true
  # Flicker lights when the event starts.
  flicker_lights: true
  # The number of seconds a first flickering lasts.
  flicker_lights_duration: 4.25
  # Base damage per each stack of delay. Tha damage is inflicted if EnableKeter is set to true.
  keter_damage: 3
  # The delay of receiving damage.
  keter_damage_delay: 5
  # Name displayed in player's death information.
  killed_by: 'SCP-575'
  # Glitch chance during message per word in CASSIE sentence.
  glitch_chance: 8
  # Jam chance during message per word in CASSIE sentence.
  jam_chance: 6
  # Message said by Cassie if no blackout occurs
  cassie_message_wrong: '. I have avoided a system failure . .g5 I am sorry for a .g3 . false alert .'
  # Message said by Cassie when a blackout starts - 3 . 2 . 1 announcement
  cassie_message_start: 'pitch_0.75 .g6 pitch_0.25 jam_027_4 .g1 pitch_1.75 .g2 pitch_0.33 .g4 . .g4 . .g4 . pitch_0.95 .g3 . ATTENTION . ATTENTION . CASSIESYSTEM ALERT . .g2 . pitch_0.35 .g3 pitch_1.0 overcharge of the facility . power system . caused by unknown pitch_0.55 jam_045_7 virus . pitch_1.1 is extremely . dangersh . commencing . pitch_1.15 safe pitch_1.25 t pitch_1.00 protocol . .g1 . pitch_0.98 potential electric power pitch_0.9 outage . pitch_0.97 in . pitch_0.95 3 . pitch_0.9 2 . pitch_0.85 jam_90_4 1 . GOING DARK pitch_0.3 .g3 pitch_0.25 .g2'
  # The time between the sentence and the 3 . 2 . 1 announcement
  time_between_sentence_and_start: 48
  # Message said by Cassie just after the blackout.
  cassie_post_message: 'pitch_0.72 jam_043_3 .g4 pitch_0.95 . ATTENTION . ATTENTION . please supply with light source or the results pitch_0.85 will be . pitch_0.8 grave'
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
  # The message CASSIE will say when a blackout ends.
  cassie_message_end: 'pitch_0.25 .g4 pitch_0.45 . .g3 .g4 pitch_0.98 . IMPORTANT MESSAGE . pitch_0.95 .g3 .g5 the facility power system is now . pitch_0.93 operational . .g3'
  # A blackout in the whole facility will occur if none of the zones is selected randomly and EnableFacilityBlackout is set to true.
  enable_facility_blackout: true
  # Percentage chance of an outage at the Heavy Containment Zone during the blackout.
  chance_heavy: 65
  # Percentage chance of an outage at the Heavy Containment Zone during the blackout.
  chance_light: 25
  # Percentage chance of an outage at the Entrance Zone during the blackout.
  chance_entrance: 45
  # Percentage chance of an outage at the Surface Zone during the blackout.
  chance_surface: 10
  # Percentage chance of an outage at an unknown and unspecified type of zone during the blackout.
  chance_other: 20
  # Change this to true if want to use per room probability settings isntead of per zone settings. The script will check all rooms in the specified zone with its probability.
  use_per_room_chances: true
# Whether of not debug messages are displayed in the console.
debug: true

```
