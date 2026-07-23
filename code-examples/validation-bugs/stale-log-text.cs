// Trade log text not matching the order actually placed
//
// Problem this solves: several live strategies hardcode the quantity in
// both the EnterLong/EnterShort call and the human-readable Print() line
// logged right after it. The order call and the log line are two separate
// literals with nothing tying them together, so when the traded quantity
// gets sized up, it's easy to update one and miss the other. The order
// itself is unaffected either way — this is a logging bug, not an
// execution bug — but it means the log can no longer be trusted as a
// record of what quantity actually got traded. Found by checking the
// order call itself instead of trusting the print statement next to it.
// Still open across most of the affected strategies as of this writeup —
// see docs/engineering-notes.md.

// --- before: two independent literals, already drifted apart -------------
if (emaCrossDown)
{
    EnterShort(2, "EXAMPLE_Strategy_Short");
    _tradedToday = true;
    Print("[EXAMPLE_Strategy] *** ENTRY PLACED ***"
        + " bar=" + CurrentBar + " time=" + Time[0]
        + " direction=SHORT qty=1"          // stale — actual order is qty 2
        + " stop=" + StopTicks + "t target=" + TargetTicks + "t");
}

// --- after: single source of truth for quantity ---------------------------
private const int TradeQty = 2;

if (emaCrossDown)
{
    EnterShort(TradeQty, "EXAMPLE_Strategy_Short");
    _tradedToday = true;
    Print("[EXAMPLE_Strategy] *** ENTRY PLACED ***"
        + " bar=" + CurrentBar + " time=" + Time[0]
        + " direction=SHORT qty=" + TradeQty
        + " stop=" + StopTicks + "t target=" + TargetTicks + "t");
}
