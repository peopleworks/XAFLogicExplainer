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

          var box = document.getElementById('q');
          var counter = document.getElementById('count');
          var cards = Array.prototype.slice.call(document.querySelectorAll('[data-search]'));
          var chips = Array.prototype.slice.call(document.querySelectorAll('.chip'));
          var app = '';
          var appLabel = '';

          function touches(card) {
            if (!app) return true;
            var owners = (card.getAttribute('data-app') || '').split(' ');
            return owners.indexOf(app) !== -1;
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

            if (!term && !app) { counter.textContent = ''; return; }

            var what = shown + (shown === 1 ? ' match' : ' matches');
            if (term) what += ' for "' + box.value.trim() + '"';
            if (app) what += ' in ' + appLabel;
            counter.textContent = what;
          }

          if (box) {
            box.addEventListener('input', filter);
            box.addEventListener('keydown', function (e) {
              if (e.key === 'Escape') { box.value = ''; filter(); }
            });
          }

          chips.forEach(function (chip) {
            chip.addEventListener('click', function () {
              var slug = chip.getAttribute('data-slug') || '';
              app = (app === slug) ? '' : slug;
              appLabel = app ? chip.textContent : '';
              chips.forEach(function (c) {
                c.classList.toggle('on', !!app && c.getAttribute('data-slug') === app);
              });
              filter();
            });
          });
        })();
        """;
}
