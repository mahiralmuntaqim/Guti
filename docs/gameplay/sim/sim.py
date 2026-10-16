import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import math, random, sys
from fightmodel import *

# id: worth, rarity, strength, style  (mirror Data.cs)
CRE = {
'soggy_boot':(2,0,.25,'steady'),'puddle_minnow':(6,0,.32,'thrasher'),'grumpy_perch':(12,0,.40,'steady'),
'stripe_bass':(20,0,.58,'runner'),'mud_gulper':(26,0,.62,'steady'),
'pebblefin':(32,1,.60,'thrasher'),'snorkel_crab':(48,1,.80,'steady'),'glimmer_eel':(55,1,.85,'thrasher'),
'moon_carp':(70,1,.82,'steady'),'candy_wrasse':(85,1,.85,'thrasher'),
'static_ray':(190,1,1.12,'runner'),'tidal_tuna':(240,1,1.25,'runner'),'crowned_sunfish':(260,2,.95,'steady'),
'prism_squid':(320,2,.90,'thrasher'),'abyss_lantern':(420,2,1.15,'steady'),
'thunderhead_grouper':(650,2,1.25,'steady'),'old_whiskers':(1800,3,1.45,'runner'),'kraken_pup':(2600,3,1.50,'thrasher')}
RODS = [('Driftwood',0,2.95,1.0),('Bamboo',75,4.05,1.3),('Carbon',750,5.4,1.7),('Storm',7000,7.0,2.2)]
CAST = [1.0,1.08,1.16,1.25]
BAIT_FISH = [['puddle_minnow','grumpy_perch','soggy_boot'],
             ['grumpy_perch','stripe_bass','mud_gulper','pebblefin'],
             ['pebblefin','glimmer_eel','moon_carp','snorkel_crab','crowned_sunfish','candy_wrasse','prism_squid'],
             ['glimmer_eel','static_ray','tidal_tuna','abyss_lantern','thunderhead_grouper'],
             ['old_whiskers','kraken_pup']]

def roll_size(r): return min(1.9, max(.45, 1 + .18 * r.gauss(0, 1)))
def fight_strength(base, size, golden):
    t = (size - .45) / (1.9 - .45); v = base * (.85 + .35 * t)
    return v * 1.25 if golden else v
def stamina_of(base): return 6.0 + 6.0 * base

def react(r): return r.uniform(.15, .30)

def run(cid, rod, r, strat='human', start=None, golden=None, size=None):
    w, ra, base, style = CRE[cid]
    sz = roll_size(r) if size is None else size
    g = (r.random() < .04) if golden is None else golden
    s = fight_strength(base, sz, g)
    if start is None: start = r.uniform(10, 22) * CAST[RODS.index(rod)] * 0.85 + 1.5
    f = Fight(s, style, stamina_of(base), rod[2], rod[3], start, r)
    hold = True; want = True; delay = 0.0; lunge_seen = False
    n = 0
    while f.result is None and n < 9000:
        if strat == 'bot':
            h = f.tension < .7
        else:
            # hysteresis 0.75 / 0.3; attentive also lets go on a lunge telegraph
            tele = strat == 'attentive' and (f.warning > 0 or f.lunging)
            if want and (f.tension >= .75 or (tele and not lunge_seen)):
                want = False; delay = react(r)
                if tele: lunge_seen = True
            elif not want and f.tension <= .3 and not (tele):
                want = True; delay = react(r)
            if not tele: lunge_seen = False
            if delay > 0: delay -= DT
            else: hold = want
            h = hold
        f.step(h); n += 1
    return f.result, n * DT, f.max_tension

def table(strats=('human','attentive'), N=300, seed=1, cres=None):
    r = random.Random(seed)
    out = {}
    for strat in strats:
        print(f'== strategy: {strat}  (catch %, mean fight s)')
        print('%-20s' % 'creature' + ''.join('%15s' % x[0] for x in RODS))
        for cid in (cres or CRE):
            row = []
            for rod in RODS:
                res = [run(cid, rod, r, strat) for _ in range(N)]
                wins = [x for x in res if x[0] == 'land']
                p = len(wins) / N; dur = sum(x[1] for x in wins) / max(1, len(wins))
                out[(strat, cid, rod[0])] = (p, dur)
                row.append('%5.0f%% %5.1fs' % (p * 100, dur))
            print('%-20s' % cid + ''.join('%15s' % x for x in row))
    return out

if __name__ == '__main__':
    table()
    r = random.Random(3)
    print('test bot, driftwood, bread fish:', {k: sum(run(k, RODS[0], r, 'bot')[0] == 'land' for _ in range(500)) / 500 for k in BAIT_FISH[0]})
