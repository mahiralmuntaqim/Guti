# Line-by-line mirror of Assets/BrineTime/Scripts/FightModel.cs. Keep in sync!
import math, random
DT = 0.02

# ---- constants (FightModel.cs) ----
FishPull = 3.8; HeldPull = 0.6; TensionGain = 1.5; TensionBase = 0.3; TensionRelax = 1.0
LandDistance = 1.8; EscapeMargin = 10.8; StartTension = 0.15
FatigueSlow = 0.35          # strength lost at full fatigue
FatigueRecover = 0.3        # fraction of the drain rate recovered per second while released

# style: base, amp, freq0, freqS, lungeMin, lungeMax, warn, dur, power, run
STYLES = {
 'steady':   (0.72, 0.18, 1.4, 1.2, 6.0, 10.0, 0.75, 0.60, 0.55, 0.0),
 'thrasher': (0.55, 0.42, 2.4, 2.4, 3.0, 5.5,  0.55, 0.35, 0.90, 0.0),
 'runner':   (0.60, 0.25, 1.6, 1.4, 5.0, 8.0,  0.80, 1.10, 0.70, 0.7),
}

class Fight:
    def __init__(self, strength, style, stamina, reel, line, start, rng):
        self.s = strength; self.st = STYLES[style]; self.stamina = stamina
        self.reel = reel; self.line = line; self.rng = rng
        self.distance = start; self.max_distance = start + EscapeMargin
        self.tension = StartTension; self.t = rng.random() * 10.0
        self.fatigue = 0.0; self.released_t = 0.0
        self.lunge_timer = self.next_lunge(); self.lunge_left = 0.0
        self.warning = 0.0; self.lunging = False; self.thrash = 0.0
        self.result = None  # 'snap' / 'escape' / 'land'
        self.max_tension = self.tension
    def next_lunge(self):
        b = self.st
        return b[4] + self.rng.random() * (b[5] - b[4])
    def step(self, holding, dt=DT):
        b = self.st; s = self.s
        self.t += dt
        # lunge cycle: wait -> telegraph (warning ramps 0..1) -> lunge
        self.lunging = False; self.warning = 0.0
        if self.lunge_left > 0:
            self.lunge_left -= dt; self.lunging = True
            if self.lunge_left <= 0: self.lunge_timer = self.next_lunge()
        else:
            self.lunge_timer -= dt
            if self.lunge_timer <= 0:
                self.lunge_left = b[7]; self.lunging = True
            elif self.lunge_timer < b[6]:
                self.warning = 1.0 - self.lunge_timer / b[6]
        th = b[0] + b[1] * math.sin(self.t * (b[2] + b[3] * s))
        if b[1] > 0.4: th += 0.12 * math.sin(self.t * 7.3)   # thrashers jitter
        if self.lunging: th += b[8]
        if holding: self.released_t = 0.0
        else: self.released_t += dt
        if b[9] > 0: th *= 1.0 + b[9] * min(self.released_t / 1.2, 1.0)
        self.thrash = th
        eff = s * (1.0 - FatigueSlow * self.fatigue)
        pull = eff * th * FishPull
        if holding:
            self.fatigue = min(1.0, self.fatigue + dt / self.stamina)
            self.tension += (eff * th * TensionGain + TensionBase) * dt / self.line
            self.distance += (pull * HeldPull - self.reel) * dt
        else:
            self.fatigue = max(0.0, self.fatigue - FatigueRecover * dt / self.stamina)
            self.tension -= TensionRelax * dt
            self.distance += pull * dt
        self.tension = min(max(self.tension, 0.0), 1.2)
        self.max_tension = max(self.max_tension, self.tension)
        if self.tension >= 1.0: self.result = 'snap'
        elif self.distance >= self.max_distance: self.result = 'escape'
        elif self.distance <= LandDistance: self.result = 'land'
        return self.result
