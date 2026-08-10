---
title: "Tu agente de código sabe XAF. Nunca ha visto tu aplicación."
description: "Un agente de IA puede recitarte la documentación de XAF y aun así equivocarse con total seguridad sobre tu aplicación, porque buena parte de lo que hace una app XAF no está en las clases de negocio. Esto es lo que construí, y los cuatro sitios donde se esconde el comportamiento."
canonical_url: "https://peopleworksgpt.com/tu-agente-sabe-xaf-pero-no-tu-aplicacion/"
cover_image: "https://raw.githubusercontent.com/peopleworks/XAFLogicExplainer/main/docs/assets/explainer-editors.png"
tags: [dotnet, devexpress, xaf, ia]
author: "Pedro Hernández (PeopleWorks)"
lang: es
---

# Tu agente de código sabe XAF. Nunca ha visto tu aplicación.

Pídele a tu asistente de IA que añada una regla de validación a `Invoice` y observa. Escribe XAF impecable: `RuleCriteria`, el contexto correcto, un `CustomMessageTemplate`, todo en su sitio. Y entonces referencia `Invoice.TotalAmount`, cuando tu clase tiene `Total`. O filtra por `[Status] = 'Approved'` y tu aplicación guarda un enum. O añade una columna que el Model Editor va a ocultar en cuanto arranque la app.

No está alucinando XAF. Sabe XAF. Lo que no ha visto nunca es **tu** XAF.

DevExpress ha hecho un trabajo excelente cerrando parte de esta brecha. Hay [skills oficiales para agentes](https://github.com/DevExpress/agent-skills) que le enseñan cómo funciona el framework, y un servidor MCP de documentación que le da la referencia oficial. Los dos son buenos de verdad. Ninguno ha leído una sola línea de tu código.

Así que construí la tercera pieza, gratis y con licencia MIT: **[XAF Logic Explainer](https://github.com/peopleworks/XAFLogicExplainer)**.

| Le enseña al agente… | Herramienta |
| --- | --- |
| Cómo funciona XAF en general | `agent-skills` de DevExpress |
| Qué dice la documentación oficial | MCP de documentación de DevExpress |
| **Qué hace TU aplicación** | **XAF Logic Explainer** |

Se complementan. Ninguna sustituye a las otras.

## Dos minutos

```bash
dotnet tool install -g XafLogicExplainer.Cli
xaflogic agents --project "C:\MiSolucion\MiApp.Module"
```

Eso escribe `AGENTS.md`, `CLAUDE.md` y `.github/copilot-instructions.md` en la raíz de tu solución. Sin cuenta, sin API key, sin servidor, sin subir nada a ninguna parte. El agente que uses entiende la aplicación en su siguiente pregunta.

O sáltate los ficheros y deja que pregunte directamente, por MCP:

```json
{ "mcpServers": { "xaf": { "command": "dnx", "args": ["XafLogicExplainer.Mcp", "--yes"] } } }
```

Nueve herramientas, en vivo contra tu código. Arrancado desde la carpeta de la solución encuentra el módulo XAF él solo, así que no hay ninguna ruta que configurar.

## Lo que no esperaba: dónde se esconde de verdad el comportamiento

Yo daba por hecho que lo interesante serían las entidades y los controladores. No lo era. La extracción valiosa resultó ser todo lo que **no está en las clases de negocio** — y una aplicación XAF guarda una cantidad notable de sí misma fuera de ellas.

**El Model Editor.** Títulos, visibilidad, orden de columnas, valores por defecto: todo en `.xafml`, nada en ningún `.cs`. Un agente leyendo tu C# te describirá una pantalla que no existe. XAF fusiona el `Model.DesignedDiffs.xafml` del módulo con el `Model.xafml` del proyecto de plataforma, así que la herramienta los fusiona igual antes de contar nada.

**Editores de propiedad y de lista propios.** Una propiedad `string` que se pinta como un lector de códigos de barras no se comporta como una caja de texto, y la clase de negocio no dice ni una palabra al respecto. Peor: el editor vive en el proyecto de **plataforma** — `MiApp.Blazor.Server`, `MiApp.Win` — al lado del módulo y no dentro. Quien lea los objetos de negocio no se lo encuentra jamás.

![La sección de editores personalizados de un explainer generado](https://raw.githubusercontent.com/peopleworks/XAFLogicExplainer/main/docs/assets/explainer-editors.png)

*Leído del proyecto de plataforma. La constante del alias se declara en el módulo, así que la herramienta resuelve constantes en toda la solución: leyendo cualquiera de los dos proyectos por separado no se resuelve nada.*

Aquí hay un matiz que me lo aclaró la documentación de DevExpress, y que cambió el diseño. Registrar un editor con `isDefault: true` sustituye el editor por defecto de ese tipo **en toda la aplicación**; con `false` solo queda *seleccionable* en el Model Editor. Mi primera versión se saltaba la distinción y anunciaba tan contenta que seis entidades «usan el lector de códigos de barras» porque tenían propiedades `string`. Era sencillamente falso. Ahora solo `true` vincula un editor a entidades por tipo.

**El JavaScript sin el que un editor no funciona.** Un mapa, un pad de firma, un escáner: el C# es una cáscara y el comportamiento está en `wwwroot/js/`. No está ni en C# ni en XML, y es la razón por la que un control se rompe en silencio cuando alguien renombra un fichero. La herramienta registra esos ficheros como parte del editor.

**Editores integrados reconfigurados en tiempo de ejecución.** Este no tiene ninguna clase propia que encontrar. Un controlador se mete en el modelo de componente de un editor integrado con `View.CustomizeViewItemControl<T>()` y le cambia el comportamiento. Nada en la entidad lo menciona. Nada en el Model Editor lo menciona. Se descubre leyendo controladores, que es exactamente lo que nadie hace cuando intenta entender un dominio.

**Migraciones que se ejecutaron una vez.** Esta es mi favorita, porque es la que hace que los agentes inventen historia. Todo equipo XAF tiene un updater lleno de bloques así:

```csharp
if (CurrentDBVersion < new Version("1.1.0.0") && CurrentDBVersion > new Version("0.0.0.0")) {
    BackfillPrescriptionExpiry();
}
```

Eso se ejecutó **una vez**, en la base de datos de producción de alguien, hace tres años, y nunca más. Leyendo el código que corre hoy no hay forma de recuperar qué hizo. Así que cuando alguien pregunta «¿por qué las filas de 2023 tienen ese valor?», el agente razona desde el código actual y se inventa una causa con total aplomo.

![La sección de migraciones, con versión, fase de esquema, condición y el código que se ejecutó](https://raw.githubusercontent.com/peopleworks/XAFLogicExplainer/main/docs/assets/explainer-migrations.png)

La herramienta guarda a qué versión se actualizaba, la cota de «solo bases de datos existentes», **en qué fase del esquema corrió** — un bloque que se ejecuta antes de que cambie el esquema no puede tocar las columnas nuevas —, los métodos que llama, el código, y **el comentario que hay encima del bloque**. Ese comentario suele ser el único registro que sobrevive del *porqué*, y el *porqué* es justo la pregunta que tiene cualquiera que lee una migración.

Los datos semilla se mantienen separados en todo momento. Los datos semilla dicen qué contiene una base de datos nueva; las migraciones dicen qué le pasó a todas las que no lo eran. Mezclarlos falsea las dos cosas.

## La decisión sobre la que se sostiene todo lo demás

La extracción es **análisis sintáctico con Roslyn**. La herramienta parsea tu código como texto. Nunca compila tu proyecto y nunca referencia un ensamblado de DevExpress.

Suena a limitación. Es la propiedad sobre la que se sostiene el proyecto entero:

- **Funciona en una rama que no compila** — que es, muchas veces, justo cuando necesitas saber qué hace la aplicación.
- **No hace falta licencia de DevExpress.** Quien quiera contribuir al extractor puede hacerlo sin suscripción, y CI corre gratis en un runner público de Ubuntu. La suite son 176 tests sobre fixtures XAF sintéticos que referencian tipos de DevExpress que no están instalados, porque nunca se compila nada.
- **Es rápido.** Parsear con Roslyn un módulo grande son segundos, no una compilación.

El precio es que las verdades que solo se ven por reflexión no están disponibles. Me pareció un buen intercambio, y tres años de uso en producción no me han hecho cambiar de opinión.

## Por qué la mayor parte de la documentación *no* está en AGENTS.md

`AGENTS.md` se antepone a **todas** las peticiones que hace un agente en ese repositorio. Su tamaño es un impuesto que se paga en cada pregunta, para siempre. Meter ahí 70 KB de detalle de entidades desplazaría a la pregunta real del usuario.

Por eso la salida va por niveles: un índice de ~11 KB que se carga siempre, y ~70 KB de detalle en `.xaflogic/` que solo se abre cuando una pregunta lo necesita.

La parte más valiosa es la más pequeña. El índice empieza con **reglas base**: que esta aplicación usa XPO y nunca EF Core, así que esas APIs aquí no existen; que los inventarios son *completos*, así que lo que no aparece de verdad no existe; y que parte del comportamiento vive en el Model Editor y no en C#. Esos pocos párrafos cortan casi toda la invención confiada.

La afirmación de mundo cerrado es la que se gana su sitio. Convierte la ausencia de evidencia en evidencia de ausencia, y es la razón por la que la respuesta útil es esta:

> No existe ninguna entidad llamada `PurchaseOrder` en esta aplicación. Esta es la lista completa de las 19 entidades, extraída de todo el árbol de código: …

## La misma extracción, para una persona

Los agentes no son los únicos lectores. `xaflogic explain` escribe una única página HTML autocontenida para quien acaba de heredar una aplicación XAF de diez años, o tiene que entregársela a alguien. Sin servidor, sin compilación, sin una sola petición a la red: se abre desde un adjunto de correo en una máquina sin internet, que es como ocurren los traspasos de verdad.

Su pieza central es un mapa de tu modelo de dominio, dibujado a partir de los atributos de asociación repartidos por veinte ficheros. La mayoría de los equipos nunca han visto el suyo. Existe en la cabeza de una persona, que es exactamente el conocimiento que se va cuando esa persona se va.

![El mapa del modelo de dominio: al pasar sobre una entidad se apaga todo lo que no toca](https://raw.githubusercontent.com/peopleworks/XAFLogicExplainer/main/docs/assets/domain-map.gif)

*Pasa el ratón por una entidad y se atenúa todo lo que no toca. El naranja significa que borrar el padre borra al hijo.*

El trazado se calcula al generar la página, no en el navegador, así que el mismo código dibuja siempre el mismo diagrama y regenerarla produce un diff legible.

## La herramienta me pilló mintiendo

Esta es la parte que preferiría no escribir, y la razón por la que la escribo.

El repositorio incluye una aplicación demo sintética para que los diagramas y las capturas enseñen algo realista que no es de ningún cliente. Haciendo capturas para la web me llamó la atención que las fichas de entidad se veían extrañamente pobres. La demo escribía sus atributos XAF sobre los **campos de respaldo** en lugar de sobre las propiedades — y así no se escribe XPO. Las clases persistentes del propio DevExpress atribuyen la propiedad; el analizador lee la propiedad.

La demo llevaba tiempo declarando **12 relaciones cuando declara 24, y 5 reglas cuando tiene 9**. Durante semanas, el mapa, el README y la web mostraron una aplicación con la mitad de riqueza que la del repositorio. Todos los tests pasaban, porque ninguno fijaba la forma de la demo.

Poco después, publicando el proyecto en un directorio de MCP, vi la ficha anunciando **v0.9.0, 7 herramientas y 129 tests**. Los números reales eran 0.11.0, 9 y 176. La sección de estado del README se había congelado meses atrás, y NuGet, el registro MCP y todos los directorios que replican un README lo estaban repitiendo.

Los dos casos son el mismo fallo, y es justo el que esta herramienta existe para atacar: **una afirmación sobre una base de código que nada obliga a seguir siendo cierta**. Así que ahora un test fija la forma de la demo, y otro deriva del código la versión, el número de herramientas y el de tests, y rompe la compilación cuando el README se desvía. Si un inventario de mundo cerrado merece generarse para tu código, merece exigirse a mi propia documentación.

## Dónde está

MIT, en GitHub: **[peopleworks/XAFLogicExplainer](https://github.com/peopleworks/XAFLogicExplainer)**. Tres paquetes en NuGet, un servidor MCP en el registro oficial, y un plugin de Claude Code:

```
/plugin marketplace add peopleworks/XAFLogicExplainer
/plugin install xaf-logic-explainer@peopleworks-xaf
```

Hay una [página de presentación](https://peopleworks.github.io/XAFLogicExplainer/) con los diagramas y salida real.

Sigue en **0.x** a propósito. El motor de extracción está probado en producción — corre contra aplicaciones XAF reales —, pero el 1.0.0 se gana cuando el extractor haya leído bases de código que no escribí yo. Y esa es la petición: apúntalo a tu aplicación XAF, y cuando lea mal un patrón que tú usas, abre un issue de [extraction gap](https://github.com/peopleworks/XAFLogicExplainer/issues/new/choose). Un patrón mal leído más un fixture suele ser la corrección entera, y así esa regresión ya no puede volver en silencio.

Tu agente ya sabe XAF. Vamos a enseñarle tu aplicación.
