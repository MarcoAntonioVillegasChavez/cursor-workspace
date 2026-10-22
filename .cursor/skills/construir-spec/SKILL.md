---
name: construir-spec
description: Construye, una spec a la vez, solo el incremento que pide un archivo de docs/specs (carpeta del curso CursoPlayGround/entregas/docs/specs) siguiendo docs/flujo-multiagente.md. Usar cuando el usuario pida desarrollar, implementar, continuar o construir una spec, la siguiente funcionalidad, el catálogo, las reservas, o una ruta de specs.
---

# Construir una spec

El chat principal es el coordinador. La spec elegida es el único contrato. `docs/flujo-multiagente.md` es el proceso. Esta skill no sustituye ese protocolo ni se vuelve un requisito para aplicarlo.

## Elegir una sola spec

La carpeta de specs es `docs/specs/` en la raíz del repositorio. Si existe la ruta que indique el usuario, usa esa.

1. Si nombra un archivo, esa es la única spec de entrada.
2. Si pide la siguiente, lista los nombres de `docs/specs/`. Lee solo el título y la línea de estado de cada una. Elige la primera cuyo comportamiento todavía no está en el código. Después lee completa únicamente esa spec.
3. Si pide la ruta completa, el orden es el nombre de archivo. Aplica el ciclo de abajo a cada etapa y no abras la siguiente hasta integrar y revisar la anterior.

No leas ni ejecutes otras specs. Consulta otro contrato solo cuando la spec elegida declara una dependencia y hace falta para resolverla. Si esa dependencia no está hecha, dilo y acuerda el tratamiento antes de ampliar el alcance.

`00-vision-y-alcance.md` define el producto. En esa etapa no hay código: se revisan decisiones y se registran acuerdos.

## Antes de editar

Lee, en este orden: la spec elegida, `README.md`, `.cursor/rules/curso.mdc`, `docs/flujo-multiagente.md` y el código que esa spec toca. Conserva los cambios ya presentes en el checkout.

Invoca al subagente `planificador` en solo lectura. Si la herramienta no puede invocarlo, dilo y planifica en esta sesión sin simular otro agente.

Presenta el plan y espera revisión humana antes de editar. Si esta conversación ya aceptó ese plan y el alcance no cambió, continúa. El plan incluye comportamiento, criterios con su comprobación, archivos por responsable y qué puede ir en paralelo.

| Responsable | Escribe |
|---|---|
| Coordinador | Raíz, solución, scripts y configuración asignados en el plan |
| Planificador | Nada |
| Implementador | Solo `src/` asignado |
| QA | Solo `tests/` asignado |
| Revisor | Nada |

Un archivo tiene un solo propietario a la vez. No compiles mientras otro siga editando el mismo checkout.

## Construir

Delega con el formato de tarea de `docs/flujo-multiagente.md`: objetivo, spec, contrato, criterios, contexto, archivos de lectura, archivos de escritura, dependencias, límites y entrega. No supongas que el especialista leyó este chat.

- Implementador y QA pueden trabajar en paralelo solo con archivos distintos.
- Construye únicamente lo que la spec incluye. Sus exclusiones se quedan fuera.
- Conserva rutas y campos JSON salvo que la spec los cambie.
- No cambies una prueba para ocultar un fallo.
- No añadas paquetes ni servicios que la spec no pida.
- Reutiliza lo que ya cumple el contrato. No regeneres el proyecto para simular un inicio desde cero.

Cuando ambos terminen, integra y ejecuta la comprobación de la spec. En este repositorio es:

```bash
dotnet test CursoCursor.slnx
```

Relaciona cada criterio con una prueba o una comprobación observable y con el resultado real. Una prueba no ejecutada no cuenta como aceptación.

Pasa la spec, el diff y esas salidas al subagente `revisor` en solo lectura. Corrige solo los hallazgos confirmados, con el propietario del archivo, y vuelve a ejecutar las comprobaciones afectadas. Si no hay una segunda sesión para revisar, dilo: una autorrevisión no es una revisión independiente.

## Entrega

Responde con:

1. Comportamiento y archivos.
2. Tabla criterio, prueba o comprobación, y resultado real.
3. Comando, salida real y límites del entorno.
4. Especialistas invocados, o la modalidad secuencial si no hubo subagentes.
5. Revisión y correcciones.
6. Pendientes.

Para ahí. La spec siguiente espera un pedido nuevo, salvo que el humano ya haya pedido la ruta completa. Commits, push, comentarios y merge solo si lo pide de forma expresa.

## Ejemplo

Pedido: «Desarrolla la spec del catálogo».

- Entrada: `docs/specs/02-catalogo-de-talleres.md`. No abras las specs de reservas.
- Plan con CAT-01…CAT-08, archivos de `src/` para implementador y de `tests/` para QA.
- Tras la aceptación del plan, ambos escriben, luego `dotnet test CursoCursor.slnx`, luego revisor.
- La entrega muestra cada CAT con su resultado. No sigas con otra spec.
