# Gameplay notes for other owners

Branch `tune/gameplay`. Files I own: Fishing.cs, PlayerController.cs, Data.cs, GameState.cs, Bobber.cs, plus
three new files: **FightModel.cs**, **CastAim.cs** and **GameplayDemo.cs**. Balance details are in `BALANCE.md`.

All existing public APIs are unchanged: Fishing.State/FishState/Charge/Tension/Distance/MaxDistance/Bobber/
AutoReel/NetState/ResetLine; PlayerController.Cam/RodTip/Fishing/Hud/UiOpen/Automation/SimMove/Pitch/Turn/
Respawn/CaptureMouse/ReleaseMouse/Create; GameState and Data members; `Data.CatchValue(4 args)` still exists.

## New read-only cues on `Fishing` (for HUD / audio)

| member | meaning | suggested use |
|---|---|---|
| `float LungeWarning` | 0 normally; ramps 0 to 1 over the 0.55-0.8 s before a lunge | **HUD**: pulse the tension bar or flash "!" / "EASE OFF" as it rises. This is the key readability cue: letting go when it rises is how you avoid snaps. |
| `bool Lunging` | the fish is lunging right now (holding builds tension fast) | HUD: shake the tension bar or tint it red |
| `float Fatigue` | 0..1, how tired the fish is (up to 35% weaker) | optional: a small "tiring..." hint or a meter |
| `FightStyle HookedStyle` | Steady / Thrasher / Runner | optional: label "It's a runner - don't give it slack!" |
| `bool Nibbling` | true for 0.35 s after a nibble (a fake tug before the real bite) | optional: a subtle "..." prompt; **do not** show the "CLICK!" prompt |
| `float BiteTimeLeft` | seconds left to strike (1.1-1.45 s window) | optional: a shrinking ring around the crosshair |
| `Vector3 AimPoint`, `bool AimOnWater` | predicted landing point while charging | already drawn by CastAim.cs (ring, orange over land) |
| `static float ChargeCurve(t)`, `static Vector3 LaunchVelocity(...)`, `static bool PredictLanding(...)` | cast math | |

Suggested HUD snippet (Hud.cs, inside the fight-meter update, `f` = Fishing):
```csharp
if (f.LungeWarning > 0f || f.Lunging)
{
    float pulse = f.Lunging ? 1f : f.LungeWarning * (0.5f + 0.5f * Mathf.Sin(Time.time * 30f));
    _tensionFill.color = Color.Lerp(_tensionFill.color, new Color(1f, 0.25f, 0.2f), pulse);
    // and/or a label: f.Lunging ? "LUNGE! let go" : "Ease off..."
}
```

Notification texts that changed: a snap now says "SNAP! You held on too hard...", an escape says "It got away!
Reel more often so it can't run.", a missed bite says "Too slow - it stole the bait...", and there is a new
"Too early - that was just a nibble. You spooked it!". Catch messages may now include "Catch of the Day!".

## Catch of the Day

`GameState.CatchOfTheDay` is today's featured species id on the current island, and is worth +50%
(`Data.CatchOfTheDay(island, date)` is a pure, stable date hash; `Data.IslandSpecies(island)` gives the pool).
Cooler records already store the boosted value. **HUD/Stall owner**: please show it, for example on the
Market prompt or in the island status ("Catch of the Day: Moon Carp +50%"), and mark it in the journal.

## Settings hook (UX owner: tune/review adds Settings.cs)

PlayerController now has public fields `LookSensitivity` (deg per axis unit, default 2.2), `InvertY`,
`BaseFov` (default 70) and `HeadBob` (default off). **PlayerController lerps `Cam.fieldOfView` toward
`BaseFov` (+5 while sprinting) every frame**, so `Settings.ApplyCamera(cam)` setting `cam.fieldOfView`
directly will be overridden at once. After both branches are on main, I (gameplay) will add this to
PlayerController (or the UX owner can add it in Settings.ApplyCamera):
```csharp
// PlayerController.Awake(), and on Settings.Changed:
void ApplySettings()
{
    LookSensitivity = 2.2f * Settings.MouseSensitivity;   // Settings is a 1 = default multiplier
    InvertY = Settings.InvertY;
    BaseFov = Settings.FieldOfView;
}
```
Until then, `Settings.ApplyCamera` could instead do `var pc = cam.GetComponentInParent<PlayerController>(); if (pc) pc.BaseFov = FieldOfView; else cam.fieldOfView = FieldOfView;`.
A "Head bob" toggle in the settings UI could set `PlayerController.HeadBob`.

## Camera shake

`PlayerController.AddShake(0..1)` adds decaying "trauma". The camera shake is built from `Pitch` every
LateUpdate, so it never drifts the look direction. Callers: cast, bite, lunge start, landing, snap, falling in.
Anyone may call it (for example the ferry horn, or a big catch announced by a co-op partner).

## QA: tests

New file `Assets/BrineTime/Tests/GameplayTests.cs` (7 plain `[Test]`s, no scene needed, about 2 s total):
the tier win rates (this also logs the C# table), snaps only while holding, lunges are telegraphed,
fights are deterministic for a seed, the charge curve, atomic save + .bak, save round-trip and migration
of an unversioned save with junk in it, and Catch of the Day stability. CoreLoop is unchanged and still
passes (the bot catches 100% of bread fish in the sim).

## Saves

`GameState.Save` now writes `<save>.tmp` and then `File.Replace`s it in, keeping `<save>.bak`. `Load` falls
back to the .bak if the main file is unreadable. Saves carry `version` (2). Older saves load unchanged; out of
range values are clamped, unknown creature ids are dropped, and missing cooler values are recomputed.
`GameState.ToJson()` / `FromJson(string)` / `WriteAtomic(path, text)` are public for tests and tools.

## README / store copy (review owner)

Prices changed: Bamboo $75, Carbon $750, Storm Rod $7000, Coral Key ticket $350, Stormrock ticket $2000,
Bucket holds 5. If the README lists prices or the fight rules, please update them; the new rules are
"watch for the twitch, then let go before the lunge" and "runners sprint if you give them slack".

## Dev tool

`BrineTime.exe -autoplay -gpdemo=<dir>` fishes 4 rounds on its own and saves screenshots of each beat
(aim ring, nibble, bite, tension, lunge telegraph, catch), then quits. It does nothing without the flag.
