"""Economy sim with the ORIGINAL fight model and data (commit 99d11bb), for before/after tables."""
import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import math, random
import sim, econ

OLD_CRE = {
 'soggy_boot': (1, 0, .25), 'puddle_minnow': (3, 0, .35), 'grumpy_perch': (7, 0, .5), 'stripe_bass': (14, 0, .65), 'mud_gulper': (18, 0, .8),
 'pebblefin': (26, 1, .55), 'snorkel_crab': (48, 1, .7), 'glimmer_eel': (55, 1, .9), 'moon_carp': (70, 1, .75), 'candy_wrasse': (85, 1, .7),
 'static_ray': (190, 1, 1.0), 'tidal_tuna': (240, 1, 1.25), 'crowned_sunfish': (260, 2, 1.1), 'prism_squid': (320, 2, .95), 'abyss_lantern': (420, 2, 1.05),
 'thunderhead_grouper': (650, 2, 1.3), 'old_whiskers': (1800, 3, 1.6), 'kraken_pup': (2600, 3, 1.55)}
OLD_RODS = [('Driftwood', 0, 2.2, 1.0), ('Bamboo', 150, 3.0, 1.3), ('Carbon', 900, 4.0, 1.7), ('Storm', 4000, 5.2, 2.2)]

def old_run(cid, rod, r, strat='human', start=None, golden=None, size=None):
    base = OLD_CRE[cid][2]
    sz = sim.roll_size(r) if size is None else size
    g = (r.random() < .04) if golden is None else golden
    st = sim.fight_strength(base, sz, g)
    D = r.uniform(10, 22) * 0.85 + 1.5 if start is None else start; M = D + 8; T = .15; t = r.random() * 10
    reel, line = rod[2], rod[3]
    hold = True; want = True; delay = 0.0; mt = T
    for i in range(9000):
        t += .02
        th = .55 + .45 * math.sin(t * (2 + 2 * st))
        if r.random() < .03: th += r.random() * .6
        pull = st * th * 2.8
        if strat == 'bot': p = T < .7
        else:
            if want and T >= .75: want = False; delay = r.uniform(.15, .3)
            elif not want and T <= .3: want = True; delay = r.uniform(.15, .3)
            if delay > 0: delay -= .02
            else: hold = want
            p = hold
        if p: T += (st * th * 1.5 + .3) * .02 / line; D += (pull * .6 - reel) * .02
        else: T -= .9 * .02; D += pull * .02
        T = min(max(T, 0), 1.2); mt = max(mt, T)
        if T >= 1: return 'snap', i * .02, mt
        if D >= M: return 'escape', i * .02, mt
        if D <= 1.8: return 'land', i * .02, mt
    return 'timeout', 180, mt

sim.CRE.clear(); sim.CRE.update({k: (v[0], v[1], v[2], 'steady') for k, v in OLD_CRE.items()})
sim.RODS[:] = OLD_RODS
econ.RODS = sim.RODS; econ.CRE = sim.CRE
econ.run = old_run
econ.BAITS[0] = ('bread', 0, 0, 0.0, (4, 7)) + econ.BAITS[0][5:]
econ.COOLERS[0] = ('Bucket', 0, 4)
econ.ISLANDS[1] = econ.ISLANDS[1][:4] + (250,) + econ.ISLANDS[1][5:]
econ.ISLANDS[2] = econ.ISLANDS[2][:4] + (1500,) + econ.ISLANDS[2][5:]

if __name__ == '__main__':
    strat = sys.argv[1] if len(sys.argv) > 1 else 'human'
    r = random.Random(1)
    print('fight win % (old model), strategy', strat)
    for cid in OLD_CRE:
        print('  %-20s' % cid + ''.join('%6.0f%%' % (100 * sum(old_run(cid, rod, r, strat)[0] == 'land' for _ in range(300)) / 300) for rod in OLD_RODS))
    econ.report(strat, int(sys.argv[2]) if len(sys.argv) > 2 else 12)
