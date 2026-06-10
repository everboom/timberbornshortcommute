# Steam Workshop description

Ready-to-paste copy for the ShortCommute Workshop page. Steam descriptions use
**BBCode**, so the block below is written in BBCode (not Markdown) — paste it
straight into the Workshop "Description" field. A plain-text fallback follows for
sites that don't accept BBCode (e.g. mod.io).

---

## Steam BBCode (paste this)

```bbcode
[h1]ShortCommute[/h1]
[i]Shorter walks to work, without the day-start freeze.[/i]

Your beavers waste half their day walking across the map to a job on the far side
of the district. [b]ShortCommute[/b] quietly fixes that: every day it moves
employed beavers into homes closer to where they actually work — measured by real
[b]road distance[/b], not straight-line — so more of the workday is spent working
and less of it is spent commuting.

[h2]What it does[/h2]
[list]
[*][b]Minimizes travel time to work.[/b] Each day, per district, it nudges
employed beavers toward closer housing — either a direct move into a nearer open
home, or a [b]home swap[/b] with another beaver when that shortens the combined
commute for both.
[*][b]Homes only — never jobs.[/b] It only ever changes [i]Home[/i] assignments,
so no beaver ever drops their work mid-shift and your job layout stays exactly as
you planned it.
[*][b]Respects your colony.[/b] Honors dwelling capacity and district boundaries,
and only gets aggressive about reshuffling when a district is genuinely
adult-overpopulated.
[*][b]Safe to add or remove.[/b] All changes go through the game's own housing
assignment, so your saves stay valid and removing the mod leaves no trace.
[/list]

[h2]Built for performance[/h2]
Other commute-balancing mods do the right thing but pay a brutal cost for it: they
recompute pathfinding distances from scratch for every worker, every day. On a big
map that's seconds of frozen main-thread work — the dreaded start-of-day stutter.

ShortCommute keeps the same smart housing logic but reorganizes the expensive part:
[list]
[*][b]Explore once, reuse everywhere.[/b] A single distance calculation per home
yields that home's distance to [i]every[/i] workplace at once — then it's cached
and reused for the whole pass. The number of expensive computations drops from
roughly (workers × homes) down to just (homes).
[*][b]Free swap evaluation.[/b] Because of how those results are cached, checking
whether two beavers should trade houses costs nothing extra — it's a simple lookup,
not a fresh calculation.
[*][b]Spread across frames, capped per tick.[/b] Work runs under a hard per-tick
budget. Anything that doesn't fit this frame is picked up the next one — so the
cost is spread thin and the frame stays flat. [b]No day-start freeze.[/b]
[/list]
Net result: the per-move cost falls by roughly [b]three orders of magnitude[/b],
and the seconds-long spike becomes work you won't feel.

[h2]Commute analysis overlay[/h2]
A built-in overlay (one click, top-right) lets you actually [i]see[/i] how your
colony is laid out:
[list]
[*][b]At a glance:[/b] every house is color-coded by its occupants' worst commute,
so long-haul housing pops out instantly across the whole map.
[*][b]Select a house:[/b] lines trace out to each resident's workplace.
[*][b]Select a workplace:[/b] lines trace back to where each worker lives.
[*][b]Select a beaver:[/b] see the actual route it walks from home to work.
[/list]
It's the fastest way to spot where your beavers are living too far from their jobs —
and to watch ShortCommute tidy it up.

[h2]Install notes[/h2]
[list]
[*][b]Disable any other commute-balancer mod[/b] — two mods reassigning homes will
fight each other.
[*][b]Requires the ModSettings mod[/b] (for the overlay's in-game settings toggle).
[/list]
```

---

## Plain-text fallback (mod.io / anywhere BBCode isn't supported)

**ShortCommute — Shorter walks to work, without the day-start freeze.**

Your beavers waste half their day walking across the map to a job on the far side
of the district. ShortCommute quietly fixes that: every day it moves employed
beavers into homes closer to where they actually work — measured by real road
distance, not straight-line — so more of the workday is spent working and less of
it is spent commuting.

**What it does**
- Minimizes travel time to work. Each day, per district, it nudges employed beavers
  toward closer housing — a direct move into a nearer open home, or a home swap with
  another beaver when that shortens the combined commute for both.
- Homes only — never jobs. It only changes Home assignments, so no beaver drops
  their work mid-shift and your job layout stays exactly as you planned it.
- Respects your colony. Honors dwelling capacity and district boundaries, and only
  gets aggressive about reshuffling when a district is genuinely adult-overpopulated.
- Safe to add or remove. All changes go through the game's own housing assignment,
  so saves stay valid and removing the mod leaves no trace.

**Built for performance**
Other commute-balancing mods recompute pathfinding distances from scratch for every
worker, every day — seconds of frozen main-thread work on a big map, felt as the
start-of-day stutter. ShortCommute keeps the same smart logic but reorganizes the
expensive part:
- Explore once, reuse everywhere. A single distance calculation per home yields its
  distance to every workplace at once, then is cached for the whole pass — dropping
  the count of expensive computations from ~(workers × homes) to ~(homes).
- Free swap evaluation. Checking whether two beavers should trade houses is a lookup,
  not a fresh calculation.
- Spread across frames, capped per tick. Work runs under a hard per-tick budget;
  overflow resumes next frame. No day-start freeze.

Net result: per-move cost falls by roughly three orders of magnitude, and the
seconds-long spike becomes work you won't feel.

**Commute analysis overlay**
A built-in overlay (one click, top-right) lets you see how your colony is laid out:
- At a glance: every house is color-coded by its occupants' worst commute.
- Select a house: lines trace out to each resident's workplace.
- Select a workplace: lines trace back to where each worker lives.
- Select a beaver: see the actual route it walks from home to work.

It's the fastest way to spot where beavers are living too far from their jobs.

**Install notes**
- Disable any other commute-balancer mod — two mods reassigning homes will fight.
- Requires the ModSettings mod (for the overlay's in-game settings toggle).
