namespace XafLogicExplainer.Core.Generators;

/// <summary>
/// The stylesheet and behaviour embedded in a generated explainer.
/// </summary>
/// <remarks>
/// Kept apart from the markup so the generator reads as document structure rather than as a wall
/// of CSS. Everything is inline in the output: an explainer has to survive being emailed, opened
/// from a network share, and read on a machine with no internet.
/// </remarks>
internal static class HtmlExplainerStyles
{
    /// <summary>
    /// The stylesheet.
    /// </summary>
    /// <remarks>
    /// The palette carries the argument. Anything the <em>framework</em> provides is drawn in
    /// graphite; anything from <em>this application</em> is in colour. A reader who never reads a
    /// caption still learns which half is the part nobody else can tell them about.
    /// </remarks>
    public const string Css = """
        :root {
          --bg: #f7f8fa; --surface: #ffffff; --surface-2: #f0f2f5;
          --ink: #14181d; --mute: #5a6672; --faint: #8b95a1;
          --line: #dfe3e8; --line-soft: #ecEFF3;

          --yours: #c2410c;        /* this application */
          --yours-soft: #fdf0e9;
          --alt: #6d28d9;          /* structure and calculation */
          --alt-soft: #f3edfd;
          --graphite: #97a1ac;     /* the framework */
          --ok: #15803d;
          --warn: #b45309;

          --mono: ui-monospace, "Cascadia Mono", "SF Mono", Menlo, Consolas, monospace;
          --sans: system-ui, -apple-system, "Segoe UI", Roboto, "Helvetica Neue", sans-serif;
          --shadow: 0 1px 2px rgba(20,24,29,.05), 0 8px 24px -14px rgba(20,24,29,.18);
        }

        @media (prefers-color-scheme: dark) {
          :root:not([data-theme="light"]) {
            --bg: #0b0e13; --surface: #12161c; --surface-2: #171c24;
            --ink: #e8edf3; --mute: #98a4b3; --faint: #6b7684;
            --line: #232a34; --line-soft: #1b212a;
            --yours: #ff8a3d; --yours-soft: #2a1a10;
            --alt: #a78bfa; --alt-soft: #1e1830;
            --graphite: #4d5866;
            --ok: #4ade80; --warn: #fbbf24;
            --shadow: 0 1px 2px rgba(0,0,0,.4), 0 8px 28px -16px rgba(0,0,0,.8);
          }
        }
        :root[data-theme="dark"] {
          --bg: #0b0e13; --surface: #12161c; --surface-2: #171c24;
          --ink: #e8edf3; --mute: #98a4b3; --faint: #6b7684;
          --line: #232a34; --line-soft: #1b212a;
          --yours: #ff8a3d; --yours-soft: #2a1a10;
          --alt: #a78bfa; --alt-soft: #1e1830;
          --graphite: #4d5866;
          --ok: #4ade80; --warn: #fbbf24;
          --shadow: 0 1px 2px rgba(0,0,0,.4), 0 8px 28px -16px rgba(0,0,0,.8);
        }

        * { box-sizing: border-box; }
        body {
          margin: 0; background: var(--bg); color: var(--ink);
          font-family: var(--sans); font-size: 15.5px; line-height: 1.6;
          -webkit-font-smoothing: antialiased;
        }
        .wrap { max-width: 72rem; margin: 0 auto; padding: 0 1.5rem; }

        /* ---------------------------------------------------------- header */
        header { border-bottom: 1px solid var(--line); background: var(--surface); }
        .head { display: flex; flex-wrap: wrap; gap: 1rem; align-items: flex-start; padding: 2rem 0 1.4rem; }
        .head__id { flex: 1 1 20rem; min-width: 0; }
        h1 { margin: 0; font-size: clamp(1.6rem, 3.4vw, 2.3rem); letter-spacing: -.025em; }
        .head__sub { margin: .35rem 0 0; color: var(--mute); font-size: .95rem; }
        .head__tools { display: flex; gap: .5rem; align-items: center; }

        #q {
          width: min(22rem, 60vw); padding: .55rem .9rem;
          border: 1px solid var(--line); border-radius: 999px;
          background: var(--bg); color: var(--ink); font: inherit; font-size: .9rem;
        }
        #q:focus { outline: 2px solid var(--yours); outline-offset: 1px; }
        .iconbtn {
          width: 2.3rem; height: 2.3rem; flex: none;
          border: 1px solid var(--line); border-radius: 999px;
          background: var(--bg); color: var(--mute); cursor: pointer; font-size: .95rem;
        }
        .iconbtn:hover { color: var(--ink); }

        .stats { display: flex; flex-wrap: wrap; gap: 1.6rem; padding-bottom: 1.6rem; }
        .stat b { display: block; font-size: 1.5rem; letter-spacing: -.02em; line-height: 1.2; }
        .stat span { font-family: var(--mono); font-size: .7rem; letter-spacing: .1em; text-transform: uppercase; color: var(--faint); }

        nav { position: sticky; top: 0; z-index: 20; background: color-mix(in srgb, var(--surface) 92%, transparent);
              backdrop-filter: blur(10px); border-bottom: 1px solid var(--line); }
        nav ul { display: flex; gap: .3rem; overflow-x: auto; list-style: none; margin: 0; padding: .5rem 0; }
        nav a { display: block; padding: .35rem .75rem; border-radius: 999px; white-space: nowrap;
                color: var(--mute); text-decoration: none; font-size: .85rem; }
        nav a:hover { background: var(--surface-2); color: var(--ink); }

        /* --------------------------------------------------------- sections */
        section { padding: 3rem 0 1rem; }
        section > h2 { margin: 0 0 .35rem; font-size: 1.45rem; letter-spacing: -.02em; }
        .lede { margin: 0 0 1.6rem; color: var(--mute); max-width: 46rem; }

        .card {
          background: var(--surface); border: 1px solid var(--line); border-radius: 12px;
          padding: 1.1rem 1.25rem; margin-bottom: .9rem; box-shadow: var(--shadow);
          /* A seed-data table with a dozen columns is wider than any window. It scrolls inside its
             own card; without this it widens the document and every section acquires a horizontal
             scrollbar because of one table far below. */
          overflow-x: auto;
        }
        .card__head { display: flex; flex-wrap: wrap; gap: .6rem; align-items: baseline; }
        .card__name { font-family: var(--mono); font-size: 1.02rem; font-weight: 650; color: var(--yours); }
        .card__meta { font-size: .82rem; color: var(--faint); }
        .card__desc { margin: .5rem 0 0; color: var(--mute); font-size: .92rem; }

        .pill {
          display: inline-block; padding: .1rem .5rem; border-radius: 999px;
          font-family: var(--mono); font-size: .68rem; letter-spacing: .04em;
          border: 1px solid var(--line); color: var(--faint); background: var(--surface-2);
        }
        .pill--key { color: var(--alt); border-color: color-mix(in srgb, var(--alt) 40%, transparent); background: var(--alt-soft); }
        .pill--req { color: var(--warn); border-color: color-mix(in srgb, var(--warn) 40%, transparent); }
        .pill--calc { color: var(--alt); border-color: color-mix(in srgb, var(--alt) 40%, transparent); }
        .pill--own { color: var(--yours); border-color: color-mix(in srgb, var(--yours) 45%, transparent); background: var(--yours-soft); }
        .pill--fw { color: var(--graphite); }

        table { border-collapse: collapse; width: 100%; margin-top: .9rem; font-size: .89rem; }
        th, td { text-align: left; padding: .45rem .6rem; border-bottom: 1px solid var(--line-soft); vertical-align: top; }
        th { font-family: var(--mono); font-size: .68rem; letter-spacing: .09em; text-transform: uppercase; color: var(--faint); font-weight: 500; }
        td.mono, .mono { font-family: var(--mono); }
        td .t { color: var(--mute); }

        details { margin-top: .9rem; }
        summary { cursor: pointer; font-size: .86rem; color: var(--mute); }
        summary:hover { color: var(--ink); }

        pre {
          margin: .7rem 0 0; padding: .9rem 1rem; overflow-x: auto;
          background: var(--surface-2); border: 1px solid var(--line-soft); border-radius: 9px;
          font-family: var(--mono); font-size: .82rem; line-height: 1.65; color: var(--ink);
        }
        code { font-family: var(--mono); font-size: .88em; }
        .crit { color: var(--alt); background: var(--alt-soft); padding: .08em .35em; border-radius: 4px; }

        .empty { color: var(--faint); font-style: italic; }

        /* -------------------------------------------------------- the map */
        .map { background: var(--surface); border: 1px solid var(--line); border-radius: 12px; padding: .5rem; box-shadow: var(--shadow); }
        /* Capped against the viewport as well as the container: a wide window would otherwise
           scale the diagram until a reader has to scroll to see a circle whole. */
        .map svg { display: block; width: 100%; height: auto; max-height: 68vh; }
        .node circle { fill: var(--yours-soft); stroke: var(--yours); stroke-width: 1.6; cursor: pointer; transition: fill .15s; }
        .node text { font-family: var(--mono); font-size: 11px; fill: var(--ink); pointer-events: none; }
        .node:hover circle, .node.on circle { fill: var(--yours); }
        .node:hover text, .node.on text { font-weight: 700; }
        .edge { fill: none; stroke: var(--graphite); stroke-width: 1.3; opacity: .55; transition: opacity .15s, stroke .15s; }
        .edge.own { stroke: var(--alt); stroke-width: 1.8; opacity: .75; }
        .edge.lit { opacity: 1; stroke: var(--yours); stroke-width: 2.4; }
        .map--dim .edge:not(.lit) { opacity: .12; }
        .map--dim .node:not(.on) circle { opacity: .32; }
        .map--dim .node:not(.on) text { opacity: .35; }
        .legend { display: flex; flex-wrap: wrap; gap: 1.2rem; padding: .6rem .8rem 0; font-size: .8rem; color: var(--faint); }
        .legend i { display: inline-block; width: 1.4rem; height: 0; border-top: 2px solid var(--graphite); vertical-align: middle; margin-right: .35rem; }
        .legend i.own { border-color: var(--alt); }

        /* ----------------------------------------------------------- search */
        .hidden { display: none !important; }
        #count { font-size: .85rem; color: var(--faint); padding: .3rem 0 0; }

        footer { border-top: 1px solid var(--line); margin-top: 3rem; padding: 2rem 0 3rem; color: var(--faint); font-size: .85rem; }
        footer a { color: var(--mute); }

        @media print {
          nav, .head__tools, #count { display: none; }
          .card { break-inside: avoid; box-shadow: none; }
          details { display: none; }
        }
        """;

    /// <summary>
    /// Search, the map's highlighting, and the theme toggle.
    /// </summary>
    /// <remarks>
    /// Everything is legible with scripting disabled: search only ever <em>hides</em> cards that
    /// are already on the page, and the map is drawn server-side. Nothing here creates content.
    /// </remarks>
    public const string Js = """
        (function () {
          var root = document.documentElement;
          var toggle = document.getElementById('theme');

          try {
            var saved = localStorage.getItem('xaflogic-explainer-theme');
            if (saved) root.setAttribute('data-theme', saved);
          } catch (e) { /* private mode */ }

          toggle.addEventListener('click', function () {
            var dark = root.getAttribute('data-theme') === 'dark' ||
                       (!root.hasAttribute('data-theme') &&
                        window.matchMedia('(prefers-color-scheme: dark)').matches);
            var next = dark ? 'light' : 'dark';
            root.setAttribute('data-theme', next);
            try { localStorage.setItem('xaflogic-explainer-theme', next); } catch (e) {}
          });

          // ---- search -------------------------------------------------------
          var box = document.getElementById('q');
          var counter = document.getElementById('count');
          var cards = Array.prototype.slice.call(document.querySelectorAll('[data-search]'));

          function filter() {
            var term = box.value.trim().toLowerCase();

            if (!term) {
              cards.forEach(function (c) { c.classList.remove('hidden'); });
              document.querySelectorAll('section').forEach(function (s) { s.classList.remove('hidden'); });
              counter.textContent = '';
              return;
            }

            var shown = 0;
            cards.forEach(function (c) {
              var hit = c.getAttribute('data-search').indexOf(term) !== -1;
              c.classList.toggle('hidden', !hit);
              if (hit) shown++;
            });

            // A section whose every card is hidden is just a heading over nothing.
            document.querySelectorAll('section').forEach(function (s) {
              var own = s.querySelectorAll('[data-search]');
              if (!own.length) return;
              var any = Array.prototype.some.call(own, function (c) { return !c.classList.contains('hidden'); });
              s.classList.toggle('hidden', !any);
            });

            counter.textContent = shown + (shown === 1 ? ' match' : ' matches') + ' for "' + box.value.trim() + '"';
          }

          box.addEventListener('input', filter);
          box.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') { box.value = ''; filter(); }
          });

          // ---- the map ------------------------------------------------------
          var map = document.getElementById('map');
          if (!map) return;

          function light(name) {
            map.classList.toggle('map--dim', !!name);
            map.querySelectorAll('.node').forEach(function (n) {
              n.classList.toggle('on', !!name && n.getAttribute('data-name') === name);
            });
            map.querySelectorAll('.edge').forEach(function (e) {
              var touches = !!name &&
                (e.getAttribute('data-from') === name || e.getAttribute('data-to') === name);
              e.classList.toggle('lit', touches);
              if (touches) {
                map.querySelectorAll('.node').forEach(function (n) {
                  var other = n.getAttribute('data-name');
                  if (other === e.getAttribute('data-from') || other === e.getAttribute('data-to')) {
                    n.classList.add('on');
                  }
                });
              }
            });
          }

          map.querySelectorAll('.node').forEach(function (node) {
            var name = node.getAttribute('data-name');
            node.addEventListener('mouseenter', function () { light(name); });
            node.addEventListener('mouseleave', function () { light(null); });
            node.addEventListener('click', function () {
              var target = document.getElementById('entity-' + name);
              if (target) target.scrollIntoView({ behavior: 'smooth', block: 'start' });
            });
          });
        })();
        """;
}
