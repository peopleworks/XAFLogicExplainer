# promo/

Promotional material generated from **measured facts about this repository and the applications
it has been run against**. Nothing here is a marketing number: every figure is produced by the
tool itself, and each piece carries its own provenance so a reader can check it.

The generator beside each piece is the deliverable that matters. Numbers go stale — the corpus
totals in this repository's own notes were a controller and an action out of date within four
days — so a piece nobody can regenerate is a piece that quietly becomes wrong.

## Pieces

| Piece | What it says | Formats |
| --- | --- | --- |
| [`foundation/`](foundation/) | Six real XAF applications by one author share 16 class names and 60 property names, and **zero** base classes | PNG 2160×3840 · MP4 8.4s · GIF · HTML |

## Regenerating

Each piece has a `build-*.py` that writes the HTML. Rendering needs the
[Epic Infographics](https://github.com/OrRon/EpicInfographics) skill (MIT), which brings
Playwright and drives headless Chromium; the MP4 and GIF also need `ffmpeg` on PATH.

```bash
python promo/build-foundation.py

SKILL=~/.claude/plugins/cache/epic-infographics/epic-infographics/0.2.0/skills/epic-infographics
node $SKILL/scripts/check.mjs   promo/foundation/infographic.html --preset story
node $SKILL/scripts/render.mjs  promo/foundation/infographic.html promo/foundation/infographic.png --preset story
node $SKILL/scripts/animate.mjs promo/foundation/infographic.html promo/foundation/infographic.mp4 --preset story
```

`check.mjs` measures real glyph geometry and refuses to continue on a text collision, so it runs
before every render rather than after a bad one.

### Where the numbers come from

`build-foundation.py` holds them in one table at the top, re-measured 2026-08-28 by running
`xaflogic wiki` over the six module directories named in the drawing's title block. To refresh
them, run the wiki over the same six and edit that table — the geometry recomputes itself, and
the scale factor printed in the title block moves with it.

## How this relates to `docs/Blog/video/`

They are different lanes and should stay that way until someone decides otherwise.

`docs/Blog/video/` is the **film and its eight shorts**: one palette in `scene_kit.py`, narration
in `guion.video.*.json`, and a hard rule that a short must land with the sound off.

`promo/` is **one measured fact per piece**, built to be true rather than to be watched. It has no
narration track of its own and does not share the film's palette.

### Two constraints this piece does not meet, recorded so nobody is surprised

1. **Safe band.** `build-shorts.py` reserves `SAFE_TOP, SAFE_BOTTOM = 300, 400` because TikTok,
   Reels and Shorts draw their own interface over the top and bottom of the frame. This piece puts
   its title near the top edge and its title block near the bottom, so **posting the MP4 as a
   platform short would hide both**. It is built as a poster and a still: safe as a feed image, an
   OG card, a slide, or a print handout, and safe as video only where the frame is not covered.
2. **Palette.** It is drawn in the Epic Infographics `blueprint` language (cyanotype blue, brass
   accent), not in the house palette from `scene_kit.py` (`#07090c` ground, `#ff8a3d` accent). That
   file's own comment is the argument against mixing them: a short that does not match the film it
   promotes looks like somebody else made it.

Meeting both would mean recomposing inside a 1220px band and re-skinning to the house palette —
a different piece, not a setting. Worth doing if these are to sit beside the eight shorts; not
worth doing if they stay posters.
