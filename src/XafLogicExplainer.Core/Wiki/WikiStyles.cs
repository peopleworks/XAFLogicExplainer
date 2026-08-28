using XafLogicExplainer.Core.Generators;

namespace XafLogicExplainer.Core.Wiki;

/// <summary>
/// What the wiki adds to the look of a single-application explainer.
/// </summary>
/// <remarks>
/// The explainer paints anything the framework provides in graphite and anything belonging to the
/// application in colour, so a reader who never reads a caption still learns which half is the part
/// nobody else can tell them about. A wiki has a third category the explainer has no word for:
/// something that appears in <em>more than one</em> application. That gets its own colour, and the
/// legend says so once at the top.
/// </remarks>
internal static class WikiStyles
{
    /// <summary>
    /// The explainer stylesheet, plus what a corpus needs on top of it.
    /// </summary>
    public static string Css => HtmlExplainerStyles.Css + Delta;

    private const string Delta = """

        /* ---------------------------------------------------- the third colour */
        :root { --shared: #0f766e; --shared-soft: #e4f4f1; }
        @media (prefers-color-scheme: dark) {
          :root:not([data-theme="light"]) { --shared: #2dd4bf; --shared-soft: #0e2b28; }
        }
        :root[data-theme="dark"] { --shared: #2dd4bf; --shared-soft: #0e2b28; }

        .pill--shared { color: var(--shared); background: var(--shared-soft); border-color: var(--shared); }
        /* Deliberately the quietest state on the page: it marks what is not a finding. */
        .pill--template { color: var(--faint); border-style: dashed; }
        .shared-ink { color: var(--shared); }

        /* ---------------------------------------------------- filtering by application */
        .filters { display: flex; flex-wrap: wrap; gap: .4rem; align-items: center; margin: .9rem 0 0; }
        .filters__label { font-size: .78rem; color: var(--faint); margin-right: .2rem; }
        .chip {
          font: inherit; font-size: .78rem; line-height: 1; cursor: pointer;
          padding: .38rem .62rem; border-radius: 999px;
          border: 1px solid var(--line); background: var(--surface); color: var(--mute);
        }
        .chip:hover { border-color: var(--shared); color: var(--shared); }
        .chip.on { border-color: var(--shared); background: var(--shared-soft); color: var(--shared); font-weight: 600; }

        /* ---------------------------------------------------- one class, several applications */
        /* A corpus grows a column per application, so the comparison is the one table here that
           genuinely cannot be made to fit. It scrolls inside its own card rather than pushing the
           page sideways — and rather than silently cropping the last application, which is the
           failure that matters: a reader would have believed the column they could see. */
        .scroller { overflow-x: auto; margin-top: .7rem; }
        .matrix { width: 100%; min-width: max-content; border-collapse: collapse; }
        .matrix th, .matrix td { border-bottom: 1px solid var(--line-soft); padding: .34rem .5rem; text-align: left; }
        .matrix th { font-size: .72rem; text-transform: uppercase; letter-spacing: .04em; color: var(--faint); font-weight: 600; }
        .matrix th.app { text-align: center; }
        .matrix td.mark { text-align: center; font-family: var(--mono); }
        .matrix td.has { color: var(--shared); }
        .matrix td.hasnt { color: var(--line); }
        .matrix tr.partial td:first-child { border-left: 2px solid var(--yours); padding-left: .4rem; }

        /* ---------------------------------------------------- where a finding lives */
        .sites { display: flex; flex-wrap: wrap; gap: .35rem; margin-top: .55rem; }
        .site {
          font-size: .76rem; padding: .28rem .55rem; border-radius: 6px;
          border: 1px solid var(--line); background: var(--surface-2); color: var(--mute);
        }
        .site b { color: var(--ink); font-weight: 600; }
        .site a { text-decoration: none; }
        .cite { font-family: var(--mono); color: var(--faint); font-size: .72rem; }

        /* ---------------------------------------------------- the applications */
        .apps { display: grid; grid-template-columns: repeat(auto-fill, minmax(268px, 1fr)); gap: .8rem; margin-top: .8rem; }
        .appcard { display: block; text-decoration: none; color: inherit; }
        .appcard:hover { border-color: var(--shared); }
        .appcard h3 { margin: 0 0 .2rem; font-size: 1rem; color: var(--ink); }
        .appcard .path { font-family: var(--mono); font-size: .72rem; color: var(--faint); word-break: break-all; }
        .appcard .counts { margin-top: .55rem; font-size: .78rem; color: var(--mute); }

        /* ---------------------------------------------------- honesty */
        .caveat {
          border-left: 3px solid var(--warn); background: var(--surface-2);
          padding: .55rem .8rem; margin: .6rem 0; border-radius: 0 8px 8px 0;
          font-size: .84rem; color: var(--mute);
        }
        .caveat b { color: var(--warn); }
        /* With one entry per application the nav is a list, not a strip; wrapping beats a
           scrollbar the reader has to discover. */
        nav ul { flex-wrap: wrap; overflow-x: visible; }

        h3.sub { margin: 1.6rem 0 .2rem; font-size: .95rem; color: var(--ink); }
        h3.sub:first-of-type { margin-top: .4rem; }

        /* Arriving from the map, so the card you were sent to is the one you look at. */
        @keyframes flash { from { border-color: var(--shared); box-shadow: 0 0 0 3px var(--shared-soft); } }
        .card.flash { animation: flash 1.5s ease-out; }
        @media (prefers-reduced-motion: reduce) { .card.flash { animation: none; } }

        /* ---------------------------------------------------- the map */
        .cmap { background: var(--surface); border: 1px solid var(--line); border-radius: 12px;
                padding: .5rem; box-shadow: var(--shadow); margin-top: .9rem; }
        .cmap svg { display: block; width: 100%; height: auto; max-height: 66vh; }

        .anode circle { fill: var(--yours-soft); stroke: var(--yours); stroke-width: 1.7; cursor: pointer; transition: fill .15s, opacity .15s; }
        .anode text { font-size: 12px; font-weight: 600; fill: var(--ink); pointer-events: none; transition: opacity .15s; }
        .anode:hover circle, .anode.on circle { fill: var(--yours); }
        .anode:focus { outline: none; }
        .anode:focus circle { stroke-width: 3; }

        .cnode circle { fill: var(--shared-soft); stroke: var(--shared); stroke-width: 1.5; cursor: pointer; transition: fill .15s, opacity .15s; }
        /* Named on the picture only where several applications meet. Every other label is one hover
           away, which keeps the middle readable instead of a word cloud. */
        .cnode text { font-family: var(--mono); font-size: 10px; fill: var(--mute); pointer-events: none; opacity: 0; transition: opacity .15s; }
        .cnode.is-named text { opacity: 1; }
        .cnode:hover circle, .cnode.on circle { fill: var(--shared); }
        .cnode:hover text, .cnode.on text { opacity: 1; fill: var(--ink); font-weight: 700; }
        .cnode:focus { outline: none; }
        .cnode:focus circle { stroke-width: 3; }
        .cnode:focus text { opacity: 1; }

        .link { fill: none; stroke: var(--graphite); stroke-width: 1; opacity: .34; transition: opacity .15s, stroke .15s; }
        .link.lit { stroke: var(--shared); stroke-width: 1.9; opacity: 1; }
        .cmap--dim .link:not(.lit) { opacity: .07; }
        .cmap--dim .anode:not(.on) circle, .cmap--dim .cnode:not(.on) circle { opacity: .26; }
        .cmap--dim .anode:not(.on) text, .cmap--dim .cnode:not(.on) text { opacity: .22; }

        /* The explainer legend draws a line; this one draws a dot. */
        .legend i.dot { width: .72rem; height: .72rem; border-radius: 50%; border: 1.6px solid var(--graphite); }
        .legend i.dot--app { background: var(--yours-soft); border-color: var(--yours); }
        .legend i.dot--class { background: var(--shared-soft); border-color: var(--shared); }
        .legend i.dot--mid { background: transparent; border-style: dashed; border-color: var(--faint); }

        /* ---------------------------------------------------- which two are most alike */
        .heat { border-collapse: separate; border-spacing: 3px; min-width: max-content; margin-top: .7rem; }
        .heat th { font-size: .7rem; font-weight: 600; color: var(--faint); text-transform: uppercase;
                   letter-spacing: .04em; padding: .2rem .35rem; }
        .heat th.app { font-family: var(--mono); font-size: .74rem; }
        .heat th.rowhead { text-align: right; white-space: nowrap; text-transform: none;
                           font-size: .78rem; color: var(--mute); letter-spacing: 0; }
        .heat th.rowhead .idx { display: inline-block; min-width: 1.1rem; margin-right: .45rem;
                                font-family: var(--mono); color: var(--faint); }
        .heat td { width: 2.7rem; height: 2.2rem; text-align: center; border-radius: 6px;
                   font-family: var(--mono); font-size: .8rem; color: var(--faint); background: var(--surface-2); }
        .heat td.is-shared { cursor: pointer; color: var(--ink);
                             background: color-mix(in srgb, var(--shared) calc(var(--i) * 76%), var(--surface-2)); }
        .heat td.is-shared:hover { outline: 2px solid var(--shared); }
        .heat td.is-shared.on { outline: 2px solid var(--shared); font-weight: 700; }
        .heat td.self { background: transparent; border: 1px dashed var(--line); }

        /* ---------------------------------------------------- the releases you are on */
        .spread { display: flex; align-items: flex-start; min-width: max-content; margin-top: .8rem; }
        .stop { flex: 1 1 0; min-width: 8.5rem; padding: 0 .7rem; text-align: center; }
        .stop__v { font-family: var(--mono); font-size: .86rem; font-weight: 700; color: var(--mute); }
        .stop.is-catalog .stop__v { color: var(--shared); }
        .stop__rule { height: 2px; background: var(--line); margin: .5rem 0 .7rem; position: relative; }
        .stop__rule::before { content: ''; position: absolute; left: 50%; top: -3px; width: 8px; height: 8px;
                              margin-left: -4px; border-radius: 50%; background: var(--yours); }
        .stop.is-catalog .stop__rule::before { background: var(--shared); }
        .stop__app { display: block; font-size: .77rem; color: var(--mute); text-decoration: none; padding: .13rem 0; }
        .stop__app:hover { color: var(--yours); }
        """;

    /// <summary>
    /// Theme, search, and the application filter.
    /// </summary>
    /// <remarks>
    /// The explainer's script drives a map that a wiki does not have, so this is its own rather than
    /// a fork of it. The one behaviour worth naming: filtering by an application shows every finding
    /// that <em>touches</em> it, including corpus findings shared with other applications, because a
    /// developer asking about one project wants to be told what it has in common with the rest.
    /// </remarks>
    public const string Js = """
        (function () {
          var root = document.documentElement;
          var toggle = document.getElementById('theme');

          try {
            var saved = localStorage.getItem('xaflogic-wiki-theme');
            if (saved) root.setAttribute('data-theme', saved);
          } catch (e) { /* private mode */ }

          if (toggle) toggle.addEventListener('click', function () {
            var dark = root.getAttribute('data-theme') === 'dark' ||
                       (!root.hasAttribute('data-theme') &&
                        window.matchMedia('(prefers-color-scheme: dark)').matches);
            var next = dark ? 'light' : 'dark';
            root.setAttribute('data-theme', next);
            try { localStorage.setItem('xaflogic-wiki-theme', next); } catch (e) {}
          });

          // ---- filtering ----------------------------------------------------
          var box = document.getElementById('q');
          var counter = document.getElementById('count');
          var cards = Array.prototype.slice.call(document.querySelectorAll('[data-search]'));
          var chips = Array.prototype.slice.call(document.querySelectorAll('.chip'));
          var cells = Array.prototype.slice.call(document.querySelectorAll('.heat td.is-shared'));

          // Every slug in here has to be on the card. One is an application; two is a pair, which
          // is what clicking a cell of the overlap grid asks for.
          var required = [];
          var label = '';

          function touches(card) {
            if (!required.length) return true;
            var owners = (card.getAttribute('data-app') || '').split(' ');
            for (var i = 0; i < required.length; i++) {
              if (owners.indexOf(required[i]) === -1) return false;
            }
            return true;
          }

          function filter() {
            var term = box ? box.value.trim().toLowerCase() : '';
            var shown = 0;

            cards.forEach(function (c) {
              var hit = touches(c) &&
                        (!term || c.getAttribute('data-search').indexOf(term) !== -1);
              c.classList.toggle('hidden', !hit);
              if (hit) shown++;
            });

            // A heading over nothing is worse than no heading.
            document.querySelectorAll('section').forEach(function (s) {
              var own = s.querySelectorAll('[data-search]');
              if (!own.length) return;
              var any = Array.prototype.some.call(own, function (c) {
                return !c.classList.contains('hidden');
              });
              s.classList.toggle('hidden', !any);
            });

            if (!counter) return;

            if (!term && !required.length) { counter.textContent = ''; return; }

            var what = shown + (shown === 1 ? ' match' : ' matches');
            if (term) what += ' for "' + box.value.trim() + '"';
            if (required.length) what += ' in ' + label;
            counter.textContent = what;
          }

          function setScope(slugs, text) {
            var same = slugs.join(' ') === required.join(' ');
            required = same ? [] : slugs;
            label = required.length ? text : '';

            chips.forEach(function (c) {
              c.classList.toggle('on', required.length === 1 && c.getAttribute('data-slug') === required[0]);
            });
            cells.forEach(function (c) {
              c.classList.toggle('on', required.length === 2 &&
                                       c.getAttribute('data-pair') === required.join(' '));
            });

            filter();
          }

          if (box) {
            box.addEventListener('input', filter);
            box.addEventListener('keydown', function (e) {
              if (e.key === 'Escape') { box.value = ''; filter(); }
            });
          }

          chips.forEach(function (chip) {
            chip.addEventListener('click', function () {
              setScope([chip.getAttribute('data-slug') || ''], chip.textContent);
            });
          });

          cells.forEach(function (cell) {
            cell.addEventListener('click', function () {
              setScope((cell.getAttribute('data-pair') || '').split(' '),
                       cell.getAttribute('data-label') || '');
            });
          });

          // ---- the map ------------------------------------------------------
          var cmap = document.getElementById('cmap');
          if (!cmap) return;

          var links = Array.prototype.slice.call(cmap.querySelectorAll('.link'));
          var apps = Array.prototype.slice.call(cmap.querySelectorAll('.anode'));
          var shared = Array.prototype.slice.call(cmap.querySelectorAll('.cnode'));

          function clear() {
            cmap.classList.remove('cmap--dim');
            links.forEach(function (l) { l.classList.remove('lit'); });
            apps.forEach(function (a) { a.classList.remove('on'); });
            shared.forEach(function (k) { k.classList.remove('on'); });
          }

          function light(attribute, value) {
            clear();
            cmap.classList.add('cmap--dim');

            var slugs = {}, names = {};

            links.forEach(function (l) {
              if (l.getAttribute(attribute) !== value) return;
              l.classList.add('lit');
              slugs[l.getAttribute('data-slug')] = true;
              names[l.getAttribute('data-class')] = true;
            });

            apps.forEach(function (a) {
              if (slugs[a.getAttribute('data-slug')]) a.classList.add('on');
            });
            shared.forEach(function (k) {
              if (names[k.getAttribute('data-class')]) k.classList.add('on');
            });
          }

          apps.forEach(function (node) {
            var slug = node.getAttribute('data-slug');
            node.addEventListener('mouseenter', function () { light('data-slug', slug); });
            node.addEventListener('focus', function () { light('data-slug', slug); });
            node.addEventListener('mouseleave', clear);
            node.addEventListener('blur', clear);
            node.addEventListener('click', function () {
              var target = document.getElementById('app-' + slug);
              if (target) target.scrollIntoView({ behavior: 'smooth', block: 'start' });
            });
          });

          shared.forEach(function (node) {
            var name = node.getAttribute('data-class');
            node.addEventListener('mouseenter', function () { light('data-class', name); });
            node.addEventListener('focus', function () { light('data-class', name); });
            node.addEventListener('mouseleave', clear);
            node.addEventListener('blur', clear);
            // Clicking a class goes to its comparison. Putting the name in the search box would
            // answer the question by hiding everything the answer needs to be read against.
            node.addEventListener('click', function () {
              var card = document.getElementById('shared-' + name);
              if (!card) return;
              card.scrollIntoView({ behavior: 'smooth', block: 'center' });
              card.classList.remove('flash');
              // Reading offsetWidth restarts the animation when the same class is clicked twice.
              void card.offsetWidth;
              card.classList.add('flash');
            });
          });
        })();
        """;
}
