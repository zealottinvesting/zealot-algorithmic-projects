# Backtest config silently drifting from the live deployed strategy
#
# Problem this solves: the backtest engine's strategy config is a
# hand-maintained Python dict, separate from the live NinjaScript files
# it's supposed to mirror. Nothing enforces that the two stay in sync, so
# an update to one doesn't guarantee the other got touched. This has
# happened at more than one scale: whole strategies present in the config
# that no longer exist in the live basket, and single field values on
# still-live strategies quietly going stale.
#
# A concrete instance: one strategy's live .cs file had its target
# distance intentionally updated after a validation pass. The backtest
# config's copy of that same field didn't get updated in the same pass —
# either the edit didn't land or a later full-config rewrite pulled from
# an older snapshot of the live file. Exact tick values are left out below
# on purpose; the point is the shape of the bug and how it got caught, not
# the sizing itself.

STRAT_CONFIGS_BUGGY = [
    dict(id=3, name="EXAMPLE_STRAT", account="Acc1", symbol="MNQ",
         direction="long",
         gate_bars=3,
         stop_ticks=198.5,      # matches live — untouched by the later edit
         target_ticks=None),    # actual value omitted — stale copy, didn't match live .cs
]

STRAT_CONFIGS_FIXED = [
    dict(id=3, name="EXAMPLE_STRAT", account="Acc1", symbol="MNQ",
         direction="long",
         gate_bars=3,
         stop_ticks=198.5,
         target_ticks=None),    # actual value omitted — now re-synced to live .cs
]


# --- how the drift got caught ---------------------------------------------
# Not by reading the config and guessing something looked off. By treating
# the config as untrusted and diffing every field against the live .cs
# property defaults directly, one strategy at a time.
def diff_config_against_live(backtest_cfg: dict, live_cs_defaults: dict) -> list[str]:
    mismatches = []
    for key, expected in live_cs_defaults.items():
        actual = backtest_cfg.get(key)
        if actual != expected:
            mismatches.append(f"{key}: backtest={actual!r} live={expected!r}")
    return mismatches


# Example live_cs_defaults would come from reading the strategy's
# [NinjaScriptProperty] default block directly out of the .cs file, not
# from memory of what it "should" be — the same discipline used to catch
# the two stale/renamed strategies still sitting in an earlier version of
# this config, and the frozen-data-series bug elsewhere in this project.
