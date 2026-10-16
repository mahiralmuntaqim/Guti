"""Economy + progression simulation. Uses the fight model in fightmodel.py (mirror of FightModel.cs)
and the content tables below (mirror of Data.cs). python -I econ.py [strategy] [runs]"""
import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import random, sim
from sim import CRE, RODS, run

# ---- Data.cs mirror ----
# bait: name, cost, pack, consume, bite(min,max), table
BAITS = [
 ('bread', 0, 0, 0.0, (3.5, 6), {'puddle_minnow': 50, 'grumpy_perch': 30, 'soggy_boot': 20}),
 ('worm', 12, 5, 0.6, (2.5, 5), {'grumpy_perch': 25, 'stripe_bass': 30, 'mud_gulper': 25, 'pebblefin': 15, 'soggy_boot': 5}),
 ('glowberry', 90, 5, 0.5, (2, 4), {'pebblefin': 15, 'glimmer_eel': 28, 'moon_carp': 24, 'snorkel_crab': 28, 'crowned_sunfish': 5}),
 ('spoon', 600, 3, 0.35, (1.5, 3), {'glimmer_eel': 20, 'static_ray': 30, 'tidal_tuna': 30, 'abyss_lantern': 16, 'old_whiskers': 4}),
]
COOLERS = [('Bucket', 0, 5), ('Cooler', 200, 8), ('Ice Chest', 1200, 14)]
# island: name, baits, maxrod, maxcooler, ticket, species, exclusive
ISLANDS = [
 ('Brine', [0, 1, 2], 1, 1, 0, 0, {}),
 ('Coral', [0, 1, 2], 2, 2, 350, 5, {'worm': {'candy_wrasse': 10}, 'glowberry': {'candy_wrasse': 18, 'prism_squid': 9}, 'spoon': {'prism_squid': 10}}),
 ('Storm', [0, 1, 2, 3], 3, 2, 2000, 10, {'glowberry': {'thunderhead_grouper': 5}, 'spoon': {'thunderhead_grouper': 18, 'kraken_pup': 4, 'old_whiskers': 3}}),
]
STARTING_MONEY = 20
GOLDEN = 0.04; GOLDEN_MULT = 3; CLEAN_MULT = 1.2; CLEAN_LIMIT = 0.85

# ---- timings (seconds) ----
T_CHARGE, T_FLIGHT, T_HOOK, T_AFTER = 0.9, 1.2, 0.35, 1.2
P_MISS_BITE = 0.06
T_SELL_TRIP = 16.0      # dock end -> market/store -> dock end, incl. menus
T_FERRY = 8.0


def table(bait, island):
    t = dict(BAITS[bait][5])
    for k, v in ISLANDS[island][6].get(BAITS[bait][0], {}).items():
        t[k] = t.get(k, 0) + v
    return t


CACHE = {}


def outcome(cid, rod, r, strat):
    key = (cid, rod, strat)
    if key not in CACHE:
        rr = random.Random(sum(map(ord, cid)) * 31 + rod * 7 + len(strat))
        lst = []
        for _ in range(250):
            g = rr.random() < GOLDEN
            sz = sim.roll_size(rr)
            res, dur, mt = run(cid, RODS[rod], rr, strat, golden=g, size=sz)
            lst.append((res, dur, mt, g, sz))
        CACHE[key] = lst
    return r.choice(CACHE[key])


def value(cid, size, golden, clean):
    v = CRE[cid][0] * size
    if golden: v *= GOLDEN_MULT
    if clean: v *= CLEAN_MULT
    return max(1, round(v))


def cast(bait, rod, island, r, strat):
    """one cast -> (seconds, $value or 0, creature or None, baits used)"""
    lo, hi = BAITS[bait][4]
    t = T_CHARGE + T_FLIGHT
    while True:
        t += r.uniform(lo, hi)
        if r.random() > P_MISS_BITE: break
    tb = table(bait, island); tot = sum(tb.values()); x = r.random() * tot
    for cid, w in tb.items():
        x -= w
        if x <= 0: break
    res, dur, mt, g, sz = outcome(cid, rod, r, strat)
    t += T_HOOK + dur + T_AFTER
    if res == 'land':
        used = 1 if r.random() < BAITS[bait][3] else 0
        return t, value(cid, sz, g, mt <= CLEAN_LIMIT), cid, used
    return t, 0, None, (1 if res == 'snap' and bait > 0 else 0)


def rate(bait, rod, island, strat, n=600, seed=0, slots=8):
    """expected net $/min for a bait, including bait cost and selling trips."""
    r = random.Random(seed); T = 0; V = 0; used = 0; catches = 0
    for _ in range(n):
        t, v, c, u = cast(bait, rod, island, r, strat); T += t; V += v; used += u; catches += c is not None
    T += catches / slots * T_SELL_TRIP
    b = BAITS[bait]; cost = used * (b[1] / b[2] if b[2] else 0)
    return (V - cost) / T * 60, catches / n


def progression(strat='attentive', seed=0, verbose=False, limit=4 * 3600):
    r = random.Random(seed)
    st = dict(money=STARTING_MONEY, rod=0, cooler=0, island=0, cur=0)
    unlocked = {0}; baits = [0, 0, 0, 0]; fish = []; journal = set(); clock = [0.0]
    events = {}

    def ev(name):
        if name not in events:
            events[name] = clock[0]
            if verbose: print('%6.1f min  %-22s money=$%d species=%d' % (clock[0] / 60, name, st['money'], len(journal)))

    rates = {}

    def best_bait():
        best, bv = 0, -1e9
        for b in ISLANDS[st['island']][1]:
            key = (b, st['rod'], st['island'])
            if key not in rates: rates[key] = rate(b, st['rod'], st['island'], strat, 300, seed)[0]
            v = rates[key]
            if any(c not in journal for c in table(b, st['island'])) and len(journal) < 12: v *= 1.15
            if BAITS[b][1] and baits[b] == 0 and st['money'] < BAITS[b][1]: continue
            if v > bv: best, bv = b, v
        return best

    def shop():
        st['money'] += sum(fish); fish.clear()
        extra = 0
        changed = True
        while changed:
            changed = False
            isl = ISLANDS[st['island']]
            for i in range(len(ISLANDS)):
                if i not in unlocked and len(journal) >= ISLANDS[i][5] and st['money'] >= ISLANDS[i][4]:
                    st['money'] -= ISLANDS[i][4]; unlocked.add(i); ev('unlock ' + ISLANDS[i][0]); changed = True
            if max(unlocked) != st['island']:
                st['island'] = max(unlocked); extra += T_FERRY; ev('arrive ' + ISLANDS[st['island']][0]); rates.clear(); changed = True
                continue
            if st['rod'] + 1 <= isl[2] and st['money'] >= RODS[st['rod'] + 1][1]:
                st['money'] -= RODS[st['rod'] + 1][1]; st['rod'] += 1; ev('rod ' + RODS[st['rod']][0]); changed = True; rates.clear()
            nc = st['cooler'] + 1
            if nc <= isl[3] and st['money'] >= COOLERS[nc][1] + 40:
                st['money'] -= COOLERS[nc][1]; st['cooler'] = nc; ev('cooler ' + COOLERS[nc][0]); changed = True
        st['cur'] = best_bait()
        b = BAITS[st['cur']]
        if b[1]:
            packs = 0
            while baits[st['cur']] < b[2] * 2 and st['money'] >= b[1] and packs < 3:
                st['money'] -= b[1]; baits[st['cur']] += b[2]; packs += 1
            if baits[st['cur']] <= 0: st['cur'] = 0
        return extra

    clock[0] += shop()
    while clock[0] < limit and 'rod Storm' not in events:
        cur = st['cur']
        if cur and baits[cur] <= 0:
            clock[0] += T_SELL_TRIP; clock[0] += shop(); continue
        dt, v, c, used = cast(cur, st['rod'], st['island'], r, strat)
        clock[0] += dt
        baits[cur] -= used
        if c and len(fish) < COOLERS[st['cooler']][2]:
            fish.append(v); journal.add(c)
            if len(journal) == 5: ev('5 species')
            if len(journal) == 10: ev('10 species')
            if c in ('old_whiskers', 'kraken_pup'): ev('first legendary')
        if len(fish) >= COOLERS[st['cooler']][2]:
            clock[0] += T_SELL_TRIP; clock[0] += shop(); ev('first sale')
    return events


def report(strat, runs):
    print('net $/min (catch rate) by bait x rod [Driftwood, Bamboo, Carbon, Storm], strategy', strat)
    for island in range(3):
        print(' island', ISLANDS[island][0])
        for b in ISLANDS[island][1]:
            row = []
            for rod in range(4):
                v, cr = rate(b, rod, island, strat)
                row.append('%7.0f (%3.0f%%)' % (v, cr * 100))
            print('  %-10s' % BAITS[b][0] + ''.join(row))
    allev = {}
    for s in range(runs):
        e = progression(strat, s, verbose=(s == 0))
        for k, v in e.items(): allev.setdefault(k, []).append(v / 60)
    print('milestones (median min over %d runs, [min..max], n reached):' % runs)
    for k, v in sorted(allev.items(), key=lambda kv: sorted(kv[1])[len(kv[1]) // 2]):
        v.sort(); print('  %-20s %6.1f  [%5.1f..%5.1f] n=%d' % (k, v[len(v) // 2], v[0], v[-1], len(v)))


if __name__ == '__main__':
    report(sys.argv[1] if len(sys.argv) > 1 else 'attentive', int(sys.argv[2]) if len(sys.argv) > 2 else 12)
