namespace ChestCounter;

/// <summary>Size and opacity choices of the overlay window (percent), offered in the settings.</summary>
public static class OverlayAppearance
{
    public static readonly int[] Sizes = { 70, 80, 90, 100, 115, 130, 150, 175 };

    /// <summary>Below 30 % the numbers can no longer be read.</summary>
    public static readonly int[] Opacities = { 100, 90, 80, 70, 60, 50, 40, 30 };

    public static int ClampSize(int percent) => Math.Clamp(percent, Sizes[0], Sizes[^1]);
    public static int ClampOpacity(int percent) => Math.Clamp(percent, Opacities[^1], 100);

    /// <summary>Next value of the list, back to the first one after the last (or for a value edited by hand).</summary>
    public static int Next(int[] values, int current) => values[(Array.IndexOf(values, current) + 1) % values.Length];
}

/// <summary>Builds what the overlays (window and OBS page) show from the saved matches and the match in progress.</summary>
public static class OverlayModel
{
    /// <summary>Caption of the right counter between two matches (also used to keep the overlay width stable).</summary>
    public static string LastMatchCaption => L.T("dernière partie", "last match", "última partida");

    /// <param name="live">Figures of the match in progress, when the replay could be read (null otherwise).</param>
    /// <param name="inProgress">A match is being recorded (even if its figures are not readable yet).</param>
    /// <param name="dropRate">1-in-N used for the probability lines (default 137500).</param>
    /// <param name="dropName">Item name (shown in the report).</param>
    public static OverlayView Build(TrackerData d, MatchRecord? live, bool inProgress, int dropRate = 137500, string dropName = "Pirate Shop Access")
    {
        var liveChests = live?.ChestsOpened ?? 0;
        var liveSquadChests = live?.SquadChestsOpened ?? liveChests;
        var liveTreasures = live?.TreasureFinds.Count ?? 0;

        var total = d.Matches.Sum(m => m.ChestsOpened) + liveChests;
        var squadTotal = d.Matches.Sum(m => m.SquadChestsOpened) + liveSquadChests;
        var session = d.Matches.Where(ReportWriter.IsThisSession).ToList();
        var rows = new List<OverlayRow>();

        // Session: chests, then small grey "n parties · moy. x" (finished matches of this session).
        string? sessionDetail = null;
        if (session.Count > 0)
        {
            var avg = (double)session.Sum(m => m.ChestsOpened) / session.Count;
            var avgText = Math.Round(avg, MidpointRounding.AwayFromZero).ToString("0", L.Culture);
            sessionDetail = $"{L.Count(session.Count, "partie", "parties", "match", "matches", "partida", "partidas")} · {L.T("moy.", "avg.", "media")} {avgText}";
        }
        rows.Add(new OverlayRow(L.T("Session", "Session", "Sesión"), L.Count(session.Sum(m => m.ChestsOpened) + liveChests, "coffre", "chest", "cofre"), Detail: sessionDetail));

        var last = d.Matches.OrderByDescending(m => m.MatchStartUtc ?? m.AnalyzedAtUtc).FirstOrDefault();
        if (last != null)
        {
            rows.Add(new OverlayRow(L.T("Dernière", "Last", "Última"), L.Count(last.ChestsOpened, "coffre", "chest", "cofre")));
        }

        var treasures = d.Matches.Sum(m => m.TreasureFinds.Count) + liveTreasures;
        rows.Add(new OverlayRow(L.T("Trésors", "Treasures", "Tesoros"), $"★ {L.N(treasures)}", Treasure: true));

        string totalProb = ProbText(total, dropRate);
        string squadTotalProb = ProbText(squadTotal, dropRate);
        string? currentProb = null;
        string? squadCurrentProb = null;
        string? squadCurrent = null;
        int currentN = 0;
        int squadCurrentN = 0;

        if (live != null)
        {
            currentN = live.ChestsOpened;
            currentProb = ProbText(currentN, dropRate);
            squadCurrentN = live.SquadChestsOpened;
            squadCurrent = L.N(squadCurrentN);
            squadCurrentProb = ProbText(squadCurrentN, dropRate);
            return new OverlayView(
                L.N(total), L.N(live.ChestsOpened), L.T("partie en cours", "current match", "partida actual"), true,
                L.T("● EN DIRECT", "● LIVE", "● EN DIRECTO"), rows, totalProb, currentProb,
                L.N(squadTotal), squadCurrent, squadTotalProb, squadCurrentProb);
        }
        if (inProgress)
        {
            return new OverlayView(
                L.N(total), "…", L.T("en cours", "in progress", "en curso"), true,
                L.T("● PARTIE EN COURS", "● MATCH IN PROGRESS", "● PARTIDA EN CURSO"), rows, totalProb, null,
                L.N(squadTotal), "…", squadTotalProb, null);
        }
        if (last != null)
        {
            currentN = last.ChestsOpened;
            currentProb = ProbText(currentN, dropRate);
            squadCurrentN = last.SquadChestsOpened;
            squadCurrent = L.N(squadCurrentN);
            squadCurrentProb = ProbText(squadCurrentN, dropRate);
        }
        return new OverlayView(
            L.N(total), last != null ? L.N(last.ChestsOpened) : null, LastMatchCaption, false, null, rows,
            totalProb, currentProb, L.N(squadTotal), squadCurrent, squadTotalProb, squadCurrentProb);
    }

    /// <summary>1 − (1 − 1/rate)^n, shown as "1 in X Y%".</summary>
    public static string ProbText(int n, int rate)
    {
        if (n <= 0 || rate <= 1) return "—";
        double p = 1.0 - Math.Pow(1.0 - 1.0 / rate, n);
        if (p <= 0 || double.IsNaN(p) || double.IsInfinity(p)) return "1 in ∞ 0%";
        double inv = 1.0 / p;
        int x = (int)Math.Round(inv, MidpointRounding.AwayFromZero);
        if (x < 1) x = 1;
        double pct = p * 100.0;
        string pctStr = pct.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "%";
        return $"1 in {x} {pctStr}";
    }
}
