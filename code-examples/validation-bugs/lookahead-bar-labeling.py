# Look-ahead leak from bar labeling during resampling
#
# Problem this solves: pandas' df.resample() defaults to labeling each
# aggregated bar by the START of its window (label='left', closed='left').
# NinjaTrader labels bars by when they CLOSE. Left unfixed, a resampled
# bar's computed value gets forward-filled onto the finer entry timeframe
# starting at that bar's OPEN time instead of its CLOSE time — a signal
# becomes visible up to one full timeframe-width earlier than it could
# actually have been seen live. This affected every strategy's signal
# timing equally, since every strategy resamples through the same helper.
# See docs/engineering-notes.md for the full incident writeup.

import pandas as pd


# --- before: look-ahead leak ---------------------------------------------
def _resample_simple_BUGGY(df: pd.DataFrame, minutes: int) -> pd.DataFrame:
    return df.resample(f"{minutes}min").agg(
        open=("open", "first"), high=("high", "max"),
        low=("low", "min"),     close=("close", "last"),
        volume=("volume", "sum"),
    ).dropna(subset=["open"])


# --- after: matches NinjaTrader's close-time bar convention ---------------
def _resample_simple(df: pd.DataFrame, minutes: int) -> pd.DataFrame:
    # label='right', closed='right' — bar timestamp = close time, matching
    # NinjaTrader's bar convention. Without this, a bar's computed value
    # gets ffill-joined onto the 1m grid starting at its OPEN time instead
    # of its CLOSE time.
    return df.resample(f"{minutes}min", label="right", closed="right").agg(
        open=("open", "first"), high=("high", "max"),
        low=("low", "min"),     close=("close", "last"),
        volume=("volume", "sum"),
    ).dropna(subset=["open"])


def _map1m(series: pd.Series, target_index: pd.DatetimeIndex) -> pd.Series:
    return series.reindex(target_index, method="ffill").fillna(False).astype(bool)


# --- how the fix got confirmed --------------------------------------------
# Rerunning the backtest and seeing the numbers move isn't proof, since a
# bug and a fix both change the numbers. Confirmation meant picking one
# specific historical signal, resampling both the buggy and corrected way,
# and checking which one's timestamp actually lined up with when that
# higher-timeframe bar would have closed on the live platform.
def confirm_signal_timestamp(df_1m: pd.DataFrame, minutes: int, signal_time) -> None:
    before = _resample_simple_BUGGY(df_1m, minutes)
    after  = _resample_simple(df_1m, minutes)

    before_bar = before.loc[before.index <= signal_time].iloc[-1]
    after_bar  = after.loc[after.index <= signal_time].iloc[-1]

    print(f"buggy bar visible as of its window START: {before_bar.name}")
    print(f"fixed bar visible as of its window CLOSE:  {after_bar.name}")
    # The fixed timestamp is the one that should match a live NinjaTrader
    # bar-close event for the same window — checked against real logged
    # bar-close times, not just eyeballed.
