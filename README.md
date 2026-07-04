## SCP-575 NPC
[![Download Latest Release](https://img.shields.io/badge/Download-Latest%20Release-blue?style=for-the-badge)](https://github.com/iomatix/-SCPSL-SCP-575-NPC/releases/latest)
[![GitHub Downloads](https://img.shields.io/github/downloads/iomatix/-SCPSL-SCP-575-NPC/latest/total?sort=date&style=for-the-badge)](https://github.com/iomatix/-SCPSL-SCP-575-NPC/releases/latest)


## Dependencies:

- **[SCPSL-AudioManagerAPI](https://github.com/iomatix/-SCPSL-AudioManagerAPI/tree/main/AudioManagerAPI)**: [Releases](https://github.com/iomatix/-SCPSL-AudioManagerAPI/releases)

## Supporting Development

My mods are **always free to use**.

If you appreciate my work, you can support me by [buying me a coffee](https://buymeacoffee.com/iomatix).


## Contributors

<a href="https://github.com/iomatix/-SCPSL-SCP-575-NPC/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=iomatix/-SCPSL-SCP-575-NPC" />
</a>

## Release – SCP-575 NPC has been rewritten to native **LabAPI** (but compatibility layer has been added so the plugin works with the **Exiled** too!)

This is the very first official build of the SCP-575 NPC mod, hosted in its own independent repo to fully decouple from the original fork.

**What we did**
- Mirrored our entire commit history into a fresh repository named `-SCPSL-SCP-575-NPC`
- Detached from the upstream `eDexiam/SCP-575` fork to remove “ahead/behind” noise
- Retained all feature branches and tags for continuity

**Why we did it**
- Avoid merge-conflicts and dependency drift with the main fork - our code has diverged completely
- Enable a clean slate for NPC-specific gameplay (blackout events, flicker logic, helper utilities, etc.)
- Simplify future maintenance and community contributions

## 🛠️ Administrative Commands

Version 9.10.2 introduces a centralized administrative control command `scp575` with aliases `575` and `ev575`. These commands are fully integrated into both the Remote Admin (RA) Console and the Server Game Console.

### 🔒 Permissions
To prevent malicious execution, all subcommands require the native `- FacilityManagement.*` administrative permission node.

### 📋 Subcommand Matrix

| Command | Syntax | Description |
| :--- | :--- | :--- |
| **Initialize Framework** | `scp575 init` / `scp575 start` | Bypasses configuration spawn weights and forces the SCP-575 environment online mid-round. Spins up execution coroutines, sets baseline parameters, and awakens tracking loops. |
| **Trigger Blackout** | `scp575 blackout [zone]` / `scp575 trigger [zone]` | Forces an immediate global or zone-specific blackout event. Supports fuzzy parsing and acronym expansions (e.g., `lcz`, `hcz`, `ez`, `surface`). Dispatches custom CASSIE broadcasts and overrides illumination fields. |
| **Emergency Disable** | `scp575 stop` / `scp575 disable` | Instantly terminates the anomaly event lifecycle. Kills active loops, silences custom audio modules, clears structural dictionary caches, and restores facility lighting. |
| **Set Blackout Stacks** | `scp575 setstacks [value]` | Explicitly overrides the running blackout stack register to a fixed baseline (e.g., `scp575 setstacks 3`). Higher stacks heavily escalate sanity decay multipliers and physical entity strike damage. Passing `0` clears the matrix and restores facility power. |
| **Shift Blackout Stacks** | `scp575 changestacks [delta]` | Shifts the running blackout stack count up or down using relative integer modifiers (e.g., `scp575 changestacks -2` or `scp575 changestacks 3`). Automatically applies safe clamps to prevent sub-zero alokacji errors. |
| **Modify Player Sanity** | `scp575 sanity [player] [action] [value]` | Seamlessly modifies a targeted player's neuro-integrity metrics. Evaluates action modifiers via typo-tolerant fuzzy cascades supporting `set`, `add`, and `sub` (subtract) vectors within a safe `0-100%` range boundary clamp. |

### 💡 Execution Examples

* Starting the event manually if it didn't roll naturally at round start:
`scp575 start`

* Testing or triggering an immediate atmospheric event for your players:
`scp575 trigger`

* Instantly turning the facility back to normal if a round gets stuck:
`scp575 disable`

* Setting the threat matrix to maximum danger (5 stack multiplication loop):
`scp575 setstacks 5`
