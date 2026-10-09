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

1. Open `manual-test\TrafficLabPlus.exe` and press **Open the LPGA example** on the start screen (since
   phase 3; before, the window opened on it). Then choose **5 Preview** on the left for the page full size.
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

Use **6 Publish ▸ Save the page as a web page file…** (since phase 3), copy the file to your phone, or wait for phase 7 (GitHub Pages),
and open it. It should work without any connection: it needs nothing from the internet.

**What to send back:** anything that differs from the above, anything you had to think about, and
whether the printout is what you want students to hand in.

---

## Phase 3 — the window and the study file (2026-10-09)

### Test 5 — change the LPGA example and watch the page follow

1. Start the program. The **start screen** says what TrafficLab+ is for and offers three ways in.
2. Press **Open the LPGA example**. **2 Network** opens: a drawing of the corridor on the left, the page
   on the right, with its instructions on top (as a player first sees them).
3. Click the **Williamson** signal on the drawing (or choose it in the **Change** list). Its form shows the
   name, the signal timing, the main-street roads and the protected lefts, each with a sentence on what
   it means and **"Where this came from: from the LPGA example"**.
4. Change **Cycle length** to 60 and press Tab. Within a second the page on the right reloads — this
   time straight to the map, without the instructions — and the line under the value says **typed by
   you**. The window title gets a **•** (unsaved).
5. Press **Ctrl+Z** (or Edit ▸ Undo). The cycle goes back to 120 and the page follows.
6. Drag an intersection on the drawing a little; the page's road follows when you let go. Ctrl+Z puts it back.
7. Try a value TrafficLab+ refuses (cycle 200). It says why beside the box and changes nothing.
8. **File ▸ Open an example ▸ Four-Way Intersection** (or its card on the start screen). One signal, no
   turn lanes, a $2M budget: press Busy day on the page and the side street fails. A good first puzzle.

### Test 6 — a new study, with its budget

1. **File ▸ New study…** (Ctrl+N). Title "Nova Road Traffic Lab", choose **Signals along a main street: 3**,
   main street "Nova Road", budget **3.5**, Create.
2. The drawing shows three signals a quarter mile apart; the page says **Fix this network on a $3.50M
   budget** and its instructions say **$3.50M budget**.
3. Rename a cross street (click it on the drawing, Street name). Add a **right-turn lane** on a road
   arriving at a signal. Both show on the page.
4. **4 Challenge**: change the budget to 4; the page follows. **3 Traffic**: each road end shows how many
   vehicles an hour come in there today — the page's own numbers.
5. **Ctrl+S** asks where to save (Documents\TrafficLabPlus\Studies). Save it, close the program, start it
   again: the start screen lists it under **Continue where you left off**; open it and everything is there.

### Test 7 — build a road, break it, fix it

1. In your Nova Road study, press **Add road end** and click on an empty spot of the drawing. A yellow bar
   appears at the top: the page cannot run, because the new road end has no road. The page on the right
   shows the same message.
2. Press **Add road**, click the new road end, then an intersection. If the intersection already has four
   roads, TrafficLab+ says so; pick one with three (or remove a road first). When the road is built, the
   yellow bar goes away and the page runs again.
3. Click a line in the yellow bar when it shows: it takes you to the thing to fix.

### Test 8 — save the page, About

1. **6 Publish ▸ Save the page as a web page file…**. Open the saved file in your browser: it is the same
   page, and it works with no internet.
2. **About**: your picture, "Created by Dr. Ron Eaglin", and the link to softwareplus.ai/trafficlab (that
   page does not exist yet; it comes with the Store release).

**What to send back:** anything that surprised you, anything you had to think about, and whether a
student could shape a real intersection with this before the map arrives (phase 4).
