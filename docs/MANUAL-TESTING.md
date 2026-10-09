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

---

## Phase 4 — the map and OpenStreetMap (2026-10-09)

These need the internet: the map, the search and the roads come from OpenStreetMap.

### Test 9 — LPGA & Williamson from the map

1. On the start screen press **New study from the map** (or **1 Map** on the left — it works with no
   study open).
2. Search **LPGA Boulevard, Daytona Beach** and pick a result. (Searching for a crossing by its two
   street names finds nothing — OpenStreetMap's search does not do crossings; step 3 does.)
3. Drag and zoom until the stretch from I-95 to Williamson fills the map, then **Load the roads
   shown on the map**. Dots appear: green for a signal, grey for none on OpenStreetMap.
4. Type **Williamson** in *Find by street name*, tick **LPGA Boulevard & Williamson Boulevard**. It
   turns yellow with a **1** on the map.
5. **Make the study from this intersection…**, keep or change the title, budget 2, Create. A message
   lists what to check; the study opens in Network with the real roads: LPGA with 3 lanes each way,
   Williamson with 2, the turn lanes OpenStreetMap shows, and "Where this came from: from
   OpenStreetMap" under them. The page on the right says **Intersection overview** and credits
   OpenStreetMap at the bottom.

### Test 10 — the LPGA corridor, saved and reopened

1. In the Map, choose the five intersections of your LPGA lab along the corridor (the east I-95 ramp
   terminal, Outlet, Williamson, Williamson & Cornerstone, Cornerstone & Outlet) and make the study.
   Use the arrows beside a chosen one to change the order.
2. Compare it with the LPGA example — `docs/OSM-LPGA-COMPARISON.md` lists what should differ and why.
3. Save it, close TrafficLab+, open the study again and go to **1 Map**: it shows the roads saved with
   the study, without loading them again.

**What to send back:** whether a student could find their intersection this way, and what in the
made study you would want different.

---

## Phase 5 — traffic volumes and FDOT counts (2026-10-09)

### Test 11 — FDOT counts for the LPGA corridor

1. Open the LPGA corridor study you made from the map in test 10 (or make it again), and go to
   **3 Traffic**. At the top: *Traffic counts from FDOT*, with what AADT, K and D mean.
2. **Look up FDOT counts for these roads.** Five of the eight road ends get a count: LPGA Blvd west
   and east (site 797025, 29,000 a day), Williamson north and south, and the I-95 off-ramp. Outlet,
   Cornerstone and the short ramp say they have none and keep their estimates.
3. Change **K** on one road to 10 and leave the box: its vehicles an hour go up. Untick one count.
4. **Use the ticked counts.** The road ends below now show their volumes "from FDOT traffic counts",
   the page reloads busier (the design hour), and its credit line says *Traffic counts: Florida
   Department of Transportation…*. Ctrl+Z puts the old numbers back.
5. Save, close, reopen: the counts are still listed, without asking FDOT again.
6. Open the **Four-Way Intersection** example and go to Traffic: it says counts need a study made from
   the map, and what to do instead.

**What to send back:** anything about the counts that a student would get wrong.

---

## Steps and help (2026-10-09)

### Test 12 — Previous and Next, and the help on each step

1. Start TrafficLab+ and go to **1 Map** with no study open. The bar at the foot says *Step 1 of 6: Map*;
   **Next: Network →** is greyed out, and the line under the title says to make a study first.
2. Open the LPGA example, go to **1 Map**, and press **Next** five times: Network, Traffic, Challenge,
   Preview, Publish. **← Previous** goes back the same way. On Publish there is no Next.
3. On each step, read the box at the top: *What you need to do here* and *Optional*. Open **Help for this
   step: every option explained**: every setting on the page is described. Choose another road in
   Network: the help stays open.
4. Look at the tags beside the settings: **needs a value** (it always has one; change it only if it is
   wrong) and **optional**. Clear the Title in Challenge and leave the box: it is refused, in words, and the old title
   is kept.

**What to send back:** any setting tagged wrongly, and anything the help does not answer.

---

## Phase 6 — the AI (2026-10-09)

### Test 13 — the AI proposes, you choose

1. **Settings ▸ AI settings…**: your key is already there if you set one up in another of your programs
   (the store is shared). The line above the buttons says which provider and model will answer.
2. Open the LPGA example. **2 Network ▸ Fill in with the AI…**. Type what you know about a real
   intersection ("Williamson has dual lefts onto LPGA; LPGA is 45 mph") and press **Ask the AI**. In
   10–40 seconds: the proposed changes, each with *before → after* and why, all ticked; the AI's notes;
   anything it could not use, with why. Untick one, press **Apply**. The values say "suggested by the
   AI"; **Ctrl+Z** takes them all back.
3. **3 Traffic ▸ Estimate traffic with the AI…** with nothing typed: an estimate for each road end.
4. **4 Challenge ▸ Write the challenge with the AI…**: "a tight budget, roundabouts too expensive".
5. **5 Preview**: press **Ask the coach…** before any test — it says to run a test first. Run a traffic
   test on the page, then ask: the coach explains your results and suggests what to try.
6. **Settings ▸ What the AI has cost…** shows what these cost.

### Test 14 — the icon and the privacy policy

1. The window, the taskbar and the .exe show the new icon: a traffic signal on a "+" (the two other
   candidates are in `resources/images/icon-candidates.png`).
2. Read https://softwareplus.ai/trafficlab/privacy/ and https://softwareplus.ai/trafficlab/ (About ▸ the
   link goes there).

**What to send back:** which icon, and anything the policy should say differently.

---

## Phase 7 — publish and hand in (2026-10-09)

### Test 15 — publish your studies and hand in a link

1. Save a study or two (Ctrl+S; the LPGA example saves as your own copy). Go to **6 Publish**. Under
   **1 Your studies** every saved study is listed with a tick; untick one.
2. **GitHub token…**: follow its steps (it opens the right GitHub page and checks the token). The account
   fills in.
3. **Publish**. The first time it creates the **TrafficLab** repository and switches GitHub Pages on.
   Then: the summary page's link and each study's link, each with **Copy the link** and **Open**. Wait a
   minute or two the first time, then open the summary page on your phone: a card per study; tap one and
   play it.
4. Tick the study you left out, untick another, publish again: you are told first which study comes off,
   by name. The addresses of the others do not change.
5. Come back another day: the links are still in 1 Your studies and 3 Publish, ready to copy.

**What to send back:** whether a student new to GitHub could get through this alone.
