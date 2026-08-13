# Recording sheet — en   (voice: Rachel)

Save each take as `audio/en/<id>.mp3` — the id is the heading, exactly as
written. `.wav`, `.m4a` and `.ogg` work too; the extension is not the part that
matters, the stem is.

The scene lengths in the guion are an estimate from the word count until a take
exists. Once these files are here:

```
python docs/Blog/video/build-scenes.py --retime    # lengths from the real audio
python docs/Blog/video/build-scenes.py --video     # re-render at the new lengths
python docs/Blog/video/build-scenes.py --assemble  # one film, with the voice on it
```

`--retime` prints the chapter list to paste into PUBLICACION.md. Nothing here is
read aloud except the quoted line: the heading is a filename, not a cue.

---

## intro

*No narration — 3.5s of title card. Skip it.*

## 01-gancho

*45 words · about 16s at a normal pace*

You ask your A I assistant to add a rule to Invoice. It writes perfect X A F. And then it references a property your class doesn't have. It isn't hallucinating X A F. It knows X A F. It has just never seen yours.

## 02-la-brecha

*35 words · about 13s at a normal pace*

DevExpress already closed part of this. There are official agent skills for how the framework works, and a docs server for the reference. Both are good. Neither has read a single line of your codebase.

## 03-dos-minutos

*36 words · about 13s at a normal pace*

So: install the tool, point it at your module, and it writes AGENTS dot M D, CLAUDE dot M D and Copilot instructions at your solution root. No account, no A P I key, nothing uploaded.

## 04-oculto

*35 words · about 13s at a normal pace*

But here's what surprised me. The valuable part isn't the entities. It's everything that is not in your business classes — and an X A F application keeps a remarkable amount of itself outside them.

## 05-editores

*49 words · about 18s at a normal pace*

A string property that renders as a barcode scanner. The custom editor lives in the platform project, beside the module, so nobody reading the business objects ever meets it. Neither does your agent. And the JavaScript it can't work without is in neither C sharp nor X M L.

## 06-migraciones

*57 words · about 21s at a normal pace*

And this one. A block guarded by current D B version ran once, on somebody's production database, three years ago, and never again. Reading today's code cannot recover what it did. So when you ask why a column holds that value, the agent invents a reason. The tool keeps the migration — and the comment explaining why.

## 07-pantallas

*65 words · about 24s at a normal pace*

And then the question nothing in the repository answers: what runs when you open this screen. X A F generates a list, a detail and a lookup view for every business class, so the screens are in no file either. Fourteen classes, fifty four screens, none of them written down. And which controllers load onto one is four conditions the framework decides at run time.

## 08-roslyn

*46 words · about 17s at a normal pace*

All of it is read as syntax, with Roslyn. Your project never compiles and no DevExpress assembly is ever referenced. So it works on a branch that doesn't build, it needs no licence, and its two hundred and seventy tests run free on a public runner.

## 09-niveles

*42 words · about 15s at a normal pace*

AGENTS dot M D is read on every single request, so most of the documentation is deliberately not in it. Eleven kilobytes always loaded, seventy opened on demand. And the smallest part matters most: if it isn't listed, it does not exist.

## 10-mapa

*47 words · about 17s at a normal pace*

The same extraction also writes one self-contained page for a person — whoever just inherited the application. Including a map of your domain model that most teams have never seen, because it lives in one person's head. Which is exactly the knowledge that leaves when they do.

## 11-cierre

*39 words · about 14s at a normal pace*

It's free, M I T, and on NuGet. Point it at your X A F application, and when it misreads a pattern yours uses, open an issue. Your agent already knows X A F. Let's teach it your application.
