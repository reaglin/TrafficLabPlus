# TrafficLab+ — hand tests

What Ron (or another tester) does with the build, phase by phase. Each test says what to do and what
should happen. Report anything that differs, or anything that was confusing, and it becomes a plan item.

**The build to use:** `manual-test\TrafficLabPlus.exe` (rebuilt with the command below), or
`dotnet run --project src/TrafficLabPlus.App`.

```powershell
dotnet publish src/TrafficLabPlus.App/TrafficLabPlus.App.csproj -c Release -r win-x64 `
    --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o manual-test
```

---

## Phases 0–2 — the engine and the page (2026-10-09)

### Test 1 — LPGA, rebuilt from data, against the original

1. Open `manual-test\TrafficLabPlus.exe`. The window shows **LPGA Traffic Lab** with the intro on top.
2. In another browser tab, open your original: https://claude.ai/artifact/UgD49FCedy4smDdEABRfud.
3. Compare them. The roads, the signals, the turn lanes, the costs and the $5M budget should match. The
   engine tests show that, with the same seed, the two move every car identically and give the same score.

**Expected differences** (all deliberate): the intro has a fourth step about **Submit plan**; the name
box says **optional**; road lengths are in **feet or miles**, not metres; the I-95 ramp labels are bold
like the arterials; there is a credit line at the bottom right.

### Test 2 — play it as a student, without a name

1. Leave the name blank and press **Start**. The page starts; the top right shows no name.
2. Raise **Demand** to Race Week. Roads turn orange and red.
3. Click a red intersection, buy a turn lane or retime the signal. The budget bar moves.
4. Press **Submit plan**. The page tests the plan (a progress bar, about 10–20 s) and then shows the
   **printout**, which says **Name not given**.
5. Press **Print or save as PDF**. Only the printout reaches the paper or PDF, not the map.

### Test 3 — with a name, in your browser

1. Press **Open in your browser** in the window. The same page opens in Edge or Chrome.
2. Type your name, Start, make a plan, Submit. The printout carries your name and a table of every
   approach, today against the plan.

### Test 4 — on a phone (optional)

Copy `Documents\TrafficLabPlus\Preview\lpga.html` to your phone, or wait for phase 7 (GitHub Pages),
and open it. It should work without any connection: it needs nothing from the internet.

**What to send back:** anything that differs from the above, anything you had to think about, and
whether the printout is what you want students to hand in.
