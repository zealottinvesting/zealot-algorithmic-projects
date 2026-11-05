"""Replica parity check: does the deployed C# strategy enter on exactly the research trades?

Before a strategy goes live, its NinjaScript logic is re-implemented line for line in
Python (same bar building, same indicator math, same session rules) and run on the
research data. Every entry timestamp has to match the research pipeline's entry list.
A "close enough" match is not accepted: each mismatch gets traced to a cause.

Real results from the November 2026 basket:
    Nasdaq Asia long   335 / 335 entries matched
    Gold NY short      446 / 446 entries matched
    Gold Asia short    137 / 137 matched, plus 4 extra entries in the replica

The 4 extras were all on Sundays. The research data had stray single bars on a few
weekends, which created a fake trading day and shifted that week's pivot levels in
the RESEARCH, not in the live code. The live behavior was the correct one.

Things this check caught while porting (each would have made live differ from research):
  * Bollinger Bands: NT8's built-in uses population std; research used sample std.
  * Pivots: NT8's indicator uses the session template; research used regular-hours bars.
  * Higher timeframes: built inside the strategy from 1-minute bars with the same
    right-closed bin labels as research, instead of adding a second data series
    (which has its own ordering and trading-hours quirks).
  * Stops in ticks: 23.8 and 190.65 ticks in research had to be rounded to 24 and 191.
"""
import pandas as pd


def parity(research_entries: pd.Series, replica_entries: pd.Series, name: str) -> pd.DataFrame:
    r = pd.DatetimeIndex(research_entries).sort_values()
    p = pd.DatetimeIndex(replica_entries).sort_values()
    only_r, only_p = r.difference(p), p.difference(r)
    print(f"{name}: matched {len(r.intersection(p))}/{len(r)}  "
          f"missing in replica {len(only_r)}  extra in replica {len(only_p)}")
    rows = [("missing", t) for t in only_r] + [("extra", t) for t in only_p]
    out = pd.DataFrame(rows, columns=["kind", "time"])
    if len(out):
        out["weekday"] = out.time.dt.day_name()   # the Sunday pattern above showed up here first
        print(out.to_string(index=False))
    return out


if __name__ == "__main__":
    research = pd.Series(pd.to_datetime(["2026-03-02 22:05", "2026-03-04 01:10", "2026-03-09 23:40"]))
    replica = pd.Series(pd.to_datetime(["2026-03-02 22:05", "2026-03-04 01:10", "2026-03-09 23:40",
                                        "2026-03-08 19:15"]))
    parity(research, replica, "example")
