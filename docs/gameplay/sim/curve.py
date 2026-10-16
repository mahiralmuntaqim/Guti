import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import sim, random
from sim import *
r = random.Random(7)
N = int(sys.argv[1]) if len(sys.argv) > 1 else 200
strats = sys.argv[2].split(',') if len(sys.argv) > 2 else ['attentive']
for strat in strats:
  for style in ['steady','thrasher','runner']:
    print('==', strat, style)
    print('str  ' + ''.join('%10s' % x[0] for x in RODS))
    for s10 in range(3, 19, 1):
        s = s10 / 10
        CRE['x'] = (1, 0, s, style)
        row = []
        for rod in RODS:
            row.append(sum(run('x', rod, r, strat)[0] == 'land' for _ in range(N)) / N)
        print('%.1f  ' % s + ''.join('%9.0f%%' % (p * 100) for p in row))
