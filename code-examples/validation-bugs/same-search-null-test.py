"""Same-search null test: is the best rule from a big search better than luck?

Searching thousands of entry rules will always find some that look good. The
question is whether the WINNERS of the real search beat the winners of the SAME
search run on data where no rule can have an edge.

This toy version builds a market with an upward drift and no real signal, mines
random entry rules on it, and compares against the same mine on shuffled entries.
Long rules "win" on the real data purely by riding the drift. A test that compares
one candidate against random entries would call them real; the same-search null
shows the real winners are no better than the shuffled winners.

The production version runs the full mining pipeline (signals, stop/target grid,
walk-forward, holdout) end to end on null data, then compares counts of survivors
and the best score. It also has a drift-free mode: signals are computed on real
prices, but every P&L is computed on prices with each hour's average drift removed.

Run: python same-search-null-test.py
"""
import numpy as np

rng = np.random.default_rng(7)
N_BARS, N_RULES, HOLD = 20_000, 2_000, 60
DRIFT = 0.004                                    # small upward drift per bar, no signal

ret = rng.normal(DRIFT, 1.0, N_BARS)
price = np.cumsum(ret)
feature = rng.normal(0, 1, (N_BARS, 20))         # 20 "indicators", all pure noise


def forward_pnl(px, entries, side):
    e = entries[entries < len(px) - HOLD]
    return side * (px[e + HOLD] - px[e])


def mine(px, shuffle=False):
    """Score N_RULES random threshold rules; return each rule's mean holdout P&L.

    shuffle=True permutes the whole indicator table in time ONCE, so rules that share
    an indicator still share their trades, exactly like the real search. Shuffling
    each rule's entries independently would make the null look far more stable than
    the real search and bias the comparison."""
    split = int(N_BARS * 0.7)
    feat = feature[rng.permutation(N_BARS)] if shuffle else feature
    out = []
    for _ in range(N_RULES):
        f, thr, side = rng.integers(20), rng.normal(0, 1.5), rng.choice([-1, 1])
        hits = np.flatnonzero(feat[:, f] > thr)
        mine_pnl = forward_pnl(px[:split], hits[hits < split], side)
        hold_pnl = forward_pnl(px, hits[hits >= split], side)
        if len(mine_pnl) > 30 and mine_pnl.mean() > 0 and len(hold_pnl) > 30:
            out.append((hold_pnl.mean(), side))
    return np.array(out)


def summary(r):
    long_ = r[r[:, 1] == 1, 0]
    return len(r), 100 * (r[:, 0] > 0).mean(), long_.max()


real = summary(mine(price))
N_NULL = 20
nulls = np.array([summary(mine(price, shuffle=True)) for _ in range(N_NULL)])
print(f"real search        survivors {real[0]:4.0f}  holdout-positive {real[1]:3.0f}%  best long {real[2]:.3f}")
print(f"{N_NULL} shuffled runs   survivors {nulls[:, 0].min():.0f}-{nulls[:, 0].max():.0f}  "
      f"holdout-positive {nulls[:, 1].min():.0f}-{nulls[:, 1].max():.0f}%  "
      f"best long {nulls[:, 2].min():.3f}-{nulls[:, 2].max():.3f}")

# The decision metric is fixed BEFORE looking: the best score, because that is what the
# search actually promotes. p = share of null searches whose best is at least as good.
p = (np.sum(nulls[:, 2] >= real[2]) + 1) / (N_NULL + 1)
print(f"\np (best long vs null bests) = {p:.2f}  ->  {'REAL' if p <= 0.05 else 'LUCK / DRIFT'}")
print("This market has no edge by construction. Secondary stats can still land outside the\n"
      "null range in one run by chance, which is why the metric is chosen in advance.")
