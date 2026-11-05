// Bug: a brand new evaluation account was silently blocked from every trade.
//
// The risk guard keeps a trailing drawdown floor per account and persists it to a
// file. When no file exists yet, it seeds the floor from a hard-coded table of
// known accounts. The old account and the new one shared a three letter prefix,
// and the seed matched on the prefix:
//
//     old account:  "EVALX...0001"  (passed long ago, floor locked at $50,100)
//     new account:  "EVALX...0002"  (fresh $50,000 balance)
//
// The new account inherited the old account's locked floor. Its balance was below
// that floor, so the guard computed $0 of room, a $0 daily stop, and rejected
// every entry. Nothing errored. The strategies looked enabled and healthy; the
// account just never traded while the identical strategies on four other accounts did.
//
// Found by comparing the guard's saved floor file against the account's real
// balance, not by reading the strategy logs (which only said "entry blocked").

// ---- before -------------------------------------------------------------
private static FloorState SeedFor_Before(string accountName, double bal)
{
    var s = new FloorState();
    if (accountName.StartsWith("EVALX"))            // matches EVERY account of this type
    { s.EodPeak = 52100.0; s.Floor = 50100.0; s.Locked = true; }
    else
    { s.EodPeak = Math.Max(StartBalance, bal); s.Floor = s.EodPeak - TrailDollars; }
    return s;
}

// ---- after --------------------------------------------------------------
// Seed by the full account id, and make the fallthrough the safe case:
// any account the table doesn't know starts as a fresh account.
private static FloorState SeedFor(string accountName, double bal)
{
    var s = new FloorState();
    if (accountName.EndsWith("EVALX...0001"))       // exactly one account
    { s.EodPeak = 52100.0; s.Floor = 50100.0; s.Locked = true; }
    else                                            // unknown account: fresh balance, untouched trail
    { s.EodPeak = Math.Max(StartBalance, bal); s.Floor = s.EodPeak - TrailDollars; }
    return s;
}

// The bad floor had already been written to disk, so the code fix alone was not
// enough: the persisted file had to be deleted (after a backup) so the account
// reseeded. And because the guard caches floors in static memory, the platform had
// to be fully restarted, not just recompiled. After restart the file came back as
// peak $50,000 / floor $48,000 / unlocked, and the account took its next signal.
