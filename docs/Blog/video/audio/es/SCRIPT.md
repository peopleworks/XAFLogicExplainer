# Recording sheet — es   (voice: Marcela)

Save each take as `audio/es/<id>.mp3` — the id is the heading, exactly as
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

Le pides a tu asistente que añada una regla a Invoice. Escribe X A F impecable. Y entonces referencia una propiedad que tu clase no tiene. No está alucinando X A F. Sabe X A F. Lo que no ha visto nunca es el tuyo.

## 02-la-brecha

*36 words · about 13s at a normal pace*

DevExpress ya cerró parte de esto. Hay skills oficiales que enseñan cómo funciona el framework, y un servidor de documentación con la referencia. Los dos son buenos. Ninguno ha leído una sola línea de tu código.

## 03-dos-minutos

*37 words · about 13s at a normal pace*

Entonces: instalas la herramienta, la apuntas a tu módulo, y escribe AGENTS punto M D, CLAUDE punto M D y las instrucciones de Copilot en la raíz de tu solución. Sin cuenta, sin clave, sin subir nada.

## 04-oculto

*41 words · about 15s at a normal pace*

Pero esto es lo que no esperaba. Lo valioso no son las entidades. Es todo lo que no está en tus clases de negocio — y una aplicación X A F guarda una cantidad notable de sí misma fuera de ellas.

## 05-editores

*61 words · about 22s at a normal pace*

Una propiedad de texto que se pinta como un lector de códigos de barras. El editor vive en el proyecto de plataforma, al lado del módulo, así que quien lee los objetos de negocio no se lo encuentra jamás. Tu agente tampoco. Y el JavaScript sin el que no funciona no está ni en C sharp ni en X M L.

## 06-migraciones

*70 words · about 25s at a normal pace*

Y esta. Un bloque protegido por current D B version se ejecutó una vez, en la base de datos de producción de alguien, hace tres años, y nunca más. Leyendo el código de hoy no hay forma de recuperar qué hizo. Así que cuando preguntas por qué una columna tiene ese valor, el agente se inventa una razón. La herramienta guarda la migración, y el comentario que explica el porqué.

## 07-pantallas

*75 words · about 27s at a normal pace*

Y luego la pregunta que nada en el repositorio responde: qué se ejecuta cuando abres esta pantalla. X A F genera una vista de lista, una de detalle y una de búsqueda por cada clase de negocio, así que las pantallas tampoco están en ningún archivo. Catorce clases, cincuenta y cuatro pantallas, ninguna escrita en ninguna parte. Y qué controladores se cargan en una son cuatro condiciones que el framework decide en tiempo de ejecución.

## 08-roslyn

*42 words · about 15s at a normal pace*

Todo se lee como sintaxis, con Roslyn. Tu proyecto nunca se compila y nunca se referencia un ensamblado de DevExpress. Por eso funciona en una rama que no compila, no hace falta licencia, y sus tests corren gratis en un runner público.

## 09-niveles

*50 words · about 18s at a normal pace*

AGENTS punto M D se lee en cada petición, así que la mayor parte de la documentación queda fuera a propósito. Once kilobytes siempre cargados, setenta que se abren solo si hacen falta. Y lo más pequeño es lo que más pesa: si no está en la lista, no existe.

## 10-mapa

*54 words · about 20s at a normal pace*

La misma extracción escribe además una página autocontenida para una persona: quien acaba de heredar la aplicación. Con un mapa de tu modelo de dominio que la mayoría de equipos nunca ha visto, porque vive en la cabeza de una persona. Que es justo el conocimiento que se va cuando esa persona se va.

## 11-cierre

*41 words · about 15s at a normal pace*

Es gratis, es M I T, y está en NuGet. Apúntalo a tu aplicación X A F, y cuando lea mal un patrón que tú usas, abre un issue. Tu agente ya sabe X A F. Vamos a enseñarle tu aplicación.
