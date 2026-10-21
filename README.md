# Proyecto de práctica: API de talleres y reservas

Este proyecto forma parte de nuestro curso de desarrollo asistido por
inteligencia artificial con Cursor.

Partiremos de una idea inicial y construiremos una API paso a paso,
utilizando especificaciones, planificación, agentes, pruebas y revisión.

## Estado actual

Actualmente contamos únicamente con la definición inicial del proyecto:
la idea, los usuarios, el objetivo y las tecnologías que utilizaremos.

Todavía no hemos creado la solución .NET, implementado endpoints ni
preparado pruebas o scripts de ejecución.

Las siguientes especificaciones se irán definiendo conforme avancemos.
No debemos asumir que un archivo o una funcionalidad existen sin
comprobarlo en el proyecto.

## Qué queremos construir

Una API que permita:

- Consultar el catálogo y el detalle de los talleres.
- Consultar los lugares disponibles.
- Reservar un lugar proporcionando nombre y correo.
- Evitar reservas duplicadas en un mismo taller.
- Rechazar reservas cuando no haya cupo.
- Cancelar una reserva y recuperar el lugar.
- Mantener estas reglas ante solicitudes simultáneas.

Los contratos HTTP, las validaciones detalladas y los criterios de
aceptación se definirán en las especificaciones de cada funcionalidad.

## Tecnologías previstas

| Área | Tecnología |
|---|---|
| Lenguaje | C# |
| Plataforma | .NET 10 |
| API | ASP.NET Core con Minimal APIs |
| Comunicación | HTTP y JSON |
| Almacenamiento | Colecciones en memoria |
| Concurrencia | Bloqueo compartido mediante `lock` |
| Validación | DataAnnotations y `AddValidation` |
| Errores | ProblemDetails |
| Documentación de la API | OpenAPI |
| Pruebas | xUnit y WebApplicationFactory |
| Automatización local | PowerShell y .NET CLI |
| Control de versiones | Git |
| Integración continua | GitHub Actions |
| Desarrollo asistido | Cursor y agentes especializados |

Bugbot se utilizará en la práctica de revisión cuando esté disponible
en el repositorio del instructor. No es necesario para ejecutar la API.

## Cómo trabajaremos

El desarrollo seguirá este recorrido:

1. Revisar la idea y delimitar el alcance.
2. Definir una especificación con resultados verificables.
3. Preparar un plan y repartir responsabilidades.
4. Implementar el cambio y sus pruebas.
5. Integrar y ejecutar las comprobaciones.
6. Revisar el resultado y corregir los problemas encontrados.
7. Presentar evidencia para la aceptación humana.

El equipo multiagente tendrá estas responsabilidades:

| Rol | Responsabilidad |
|---|---|
| Coordinador | Reunir contexto, repartir tareas, integrar y verificar. |
| Planificador | Analizar el requisito y proponer un plan. |
| Implementador | Escribir el código del comportamiento acordado. |
| QA | Preparar pruebas de los criterios de aceptación. |
| Revisor | Inspeccionar el cambio y señalar problemas verificables. |

Las definiciones de los agentes y el protocolo se comprobarán antes de
utilizarlos. Describir estos roles no significa que ya estén configurados
o que hayan sido invocados.

Los agentes ayudan a construir la aplicación; no forman parte de la API.

## Reglas de trabajo

- Leer la definición inicial y el requisito de la tarea antes de editar.
- Comprobar qué archivos y funcionalidades existen realmente.
- Trabajar únicamente dentro del alcance acordado.
- Asignar un responsable de escritura por archivo.
- Esperar a que terminen las ediciones antes de compilar y probar.
- No cambiar las expectativas de una prueba para ocultar un defecto.
- Distinguir lo planeado, lo implementado y lo comprobado.
- Reportar los resultados reales y los pendientes.
- Mantener la revisión y las decisiones del humano durante el proceso.

## Límites del proyecto

Será una demostración local con datos en memoria.

No construiremos por ahora una interfaz web, autenticación, pagos,
persistencia en base de datos ni despliegue público.

Los datos se perderán al reiniciar. El control de concurrencia protegerá
una sola instancia de la aplicación.

## Primer paso

Revisar la definición inicial y confirmar el objetivo, las tecnologías
y los límites. Después prepararemos la especificación para crear
la solución y la estructura inicial del proyecto.

Los comandos de ejecución y pruebas se documentarán cuando existan
los proyectos correspondientes.

## Participación en el curso

Puedes realizar las prácticas localmente o seguir la demostración.
No se requieren entregas de archivos, reportes o proyectos ni
se asignan calificaciones.