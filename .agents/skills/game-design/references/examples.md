# Game Design Reply Examples

## Example 1: Designing a Power

Prompt: `"How should the rain power work?"`

> Make rain slow and wide: it soaks the ground and feeds rivers over a minute or two, so the payoff lands where the attacker *will* be.
>
> Chain: rain, then rivers rise and fields turn to mud, then the column slows and bunches at crossings. Regiments that push across a ford at the peak are swept away, and the banks watching them waver.
>
> | Unit | Effect |
> |---|---|
> | Cavalry | Charges stall in mud; flooded fords are impassable |
> | Siege | Bogs down hardest |
> | Ranged | Wet strings cut range; rain cuts sight |
> | Infantry | Mild slowdown that worsens as thousands churn the mud |
>
> Combos and counters: wet ground stops fire; a cold snap turns floods to ice; heat dries it out again.
> Counterplay: the attacker waits for the river to drop or bridges it, so rain buys time rather than winning. Both armies get wet.
> Timeline: paint the storm's area; its bar runs from the first drops to the river's peak.
> Build: wetness and water height are grid fields read by both the simulation and the terrain shader.
> **Should the attacker AI wait for the water to drop, or should some commanders risk the crossing?**

## Example 2: Recording a Stated Decision

Prompt: `"Have some powers counter other fire/wind/rain etc"`

> Make counters come from how the world fields interact, not from rules like "rain cancels fire". They're then partial and timed: rain before a fire is cheap, rain on a raging fire is costly.
>
> | Power | Countered by |
> |---|---|
> | Fire | Rain; a backfire |
> | Rain, flood | Heat; cold (ice bridge) |
> | Fog, smoke | Wind; heat |
>
> Turnabouts beat cancels: wind pushes their fire back onto their own army.
> Risks: a strict rock-paper-scissors cycle, counter chains longer than three steps, counters cheaper than the setups they answer.
> Build: the fields update each other every tick in a fixed order, with strengths in a tunable table.
> Recorded in `docs/design/game-brief.md`; the full counter web is in the power palette.
> **In the campaign, does the attacker have its own priests casting powers?**
