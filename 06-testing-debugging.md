# 06 - Testing and Debugging Agent

[← Volver al índice](agents.md)

---

## Objetivo
Garantizar la calidad, estabilidad, rendimiento y seguridad de la aplicación mediante pruebas exhaustivas, logging estructurado, herramientas de depuración y monitoreo del rendimiento de base de datos.

---

## Tareas Detalladas

### 6.1 Arquitectura de pruebas
**Descripción:** Establecer la infraestructura de testing del proyecto con frameworks, convenciones y CI/CD.

**Cómo hacerlo:**
- Crear proyectos de test separados en la solución:
  - `DbClient.Core.Tests` — Tests unitarios de la lógica de negocio.
  - `DbClient.Data.Tests` — Tests de integración con bases de datos.
  - `DbClient.UI.Tests` — Tests de la capa de presentación (ViewModels).
- **Framework:** NUnit 4 (o xUnit como alternativa).
- **Mocking:** Moq o NSubstitute para simular dependencias.
- **Assertions:** FluentAssertions para aserciones legibles.
- **Covertura:** Coverlet + ReportGenerator para informes de cobertura.
- Convenciones de naming: `[Método]_[Escenario]_[ResultadoEsperado]`.
  - Ej: `ExecuteQueryAsync_ValidSelect_ReturnsDataTable`.
- Estructura de carpetas espejando la del proyecto principal.

**Paquetes NuGet:**
- `NUnit`, `NUnit3TestAdapter`, `NUnit.Analyzers`
- `Moq` o `NSubstitute`
- `FluentAssertions`
- `Coverlet.collector`, `ReportGenerator`

### 6.2 Tests unitarios
**Descripción:** Escribir tests unitarios para toda la lógica de negocio que no dependa de recursos externos.

**Cómo hacerlo:**
- **Capa de proveedores (con mocks):**
  - Verificar que `DatabaseProviderFactory` devuelve el proveedor correcto según `DatabaseType`.
  - Testear mapeo de tipos de datos entre motores.
  - Testear generación de SQL en el Table Designer (CREATE TABLE, ALTER TABLE).
  - Testear parser de sentencias SQL (separar por `;`, respetar strings/comentarios).
- **ViewModels:**
  - Testear `ConnectionTreeViewModel`: añadir, editar, eliminar conexiones.
  - Testear `QueryTabViewModel`: estado de ejecución, cancelación, resultados.
  - Testear `TableDesignerViewModel`: validaciones de columnas, generación de script.
  - Testear `SettingsViewModel`: cambio de configuración, persistencia.
- **Servicios:**
  - Testear `ConnectionRepository`: CRUD de conexiones, cifrado/descifrado.
  - Testear `QueryHistoryService`: guardado, búsqueda, límite de entradas.
  - Testear `ExportService`: generación de CSV, JSON, SQL para datos de prueba.
  - Testear `ScheduledTaskService`: programación y ejecución de tareas.
- **Utilidades:**
  - Testear formateo de SQL.
  - Testear conversiones de tipos.
  - Testear cifrado/descifrado de contraseñas.

### 6.3 Tests de integración con bases de datos
**Descripción:** Verificar el funcionamiento real contra instancias de base de datos.

**Cómo hacerlo:**
- Usar **Docker** para levantar instancias temporales de cada motor:
  - PostgreSQL: `docker run -d -p 5432:5432 -e POSTGRES_PASSWORD=test postgres:16`
  - MySQL: `docker run -d -p 3306:3306 -e MYSQL_ROOT_PASSWORD=test mysql:8`
  - MariaDB: `docker run -d -p 3307:3306 -e MARIADB_ROOT_PASSWORD=test mariadb:11`
  - SQLite: archivo temporal en disco.
- Usar **Testcontainers for .NET** para automatizar la creación/destrucción de contenedores.
- Tests a realizar por motor:
  - Conexión y desconexión.
  - Conexión SSL (si aplica).
  - Obtener lista de bases de datos, esquemas, tablas, columnas.
  - Ejecutar SELECT, INSERT, UPDATE, DELETE.
  - Ejecutar script con múltiples sentencias.
  - Crear y eliminar tabla.
  - Importar datos CSV a una tabla.
  - Exportar datos a CSV/JSON.
  - Cancelar consulta larga.
  - Reconexión ante desconexión forzada.
- Tests de comparación/sincronización:
  - Comparar esquema entre dos BDs.
  - Sincronizar datos entre dos tablas.
- Tests de migración:
  - Migrar tabla de MySQL a PostgreSQL.
  - Verificar mapeo de tipos.

**Paquetes NuGet:**
- `Testcontainers` + módulos por motor (`Testcontainers.PostgreSql`, `Testcontainers.MySql`, `Testcontainers.MariaDb`)

### 6.4 Tests de UI (ViewModels y comportamientos)
**Descripción:** Testear la capa de presentación sin necesidad de renderizar UI real.

**Cómo hacerlo:**
- Testear ViewModels aisladamente usando mocks de los servicios.
- Verificar:
  - Bindings se actualizan correctamente (`INotifyPropertyChanged`).
  - Commands se habilitan/deshabilitan según el estado.
  - Navegación entre pestañas funciona.
  - Diálogos/popups se disparan con los datos correctos.
- Para tests de comportamiento visual (opcional avanzado):
  - Usar **Appium** o **FlaUI** para automatizar la UI de WPF.
  - Tests de flujo: crear conexión → abrir tabla → editar dato → guardar.
  - Tests de regresión visual (screenshots comparativos).

**Paquetes NuGet (opcional):**
- `FlaUI.Core`, `FlaUI.UIA3`

### 6.5 Sistema de logging
**Descripción:** Implementar logging estructurado para depuración, auditoría y diagnóstico de problemas.

**Cómo hacerlo:**
- Usar `Microsoft.Extensions.Logging` como abstracción + `Serilog` como implementación.
- Configurar sinks (destinos):
  - **Archivo rotativo:** `logs/dbclient-{date}.log` (máximo 7 días o 50MB).
  - **Consola de debug:** Solo en modo DEBUG.
  - **Visor de logs en la app:** Panel opcional que muestra logs en tiempo real.
- Niveles de log:
  - `Verbose`: SQL ejecutado, parámetros, tiempos de cada operación.
  - `Debug`: Eventos internos, cambios de estado.
  - `Information`: Acciones del usuario (conectar, ejecutar, importar).
  - `Warning`: Reconexiones, queries lentas, datos truncados.
  - `Error`: Excepciones capturadas, fallos de conexión.
  - `Fatal`: Errores no recuperables.
- Información contextual en cada log:
  - Timestamp, nivel, mensaje, excepción (si aplica).
  - ConnectionId, Database, Username (para logs de BD).
  - QueryId, SQL (truncado), Duration (para logs de queries).
- Configuración:
  - Nivel mínimo de log configurable por el usuario (default: Information).
  - Habilitar/deshabilitar log de SQL completo (privacidad).
- NO loguear contraseñas ni datos sensibles nunca.

**Paquetes NuGet:**
- `Serilog`, `Serilog.Extensions.Logging`
- `Serilog.Sinks.File`, `Serilog.Sinks.Console`

### 6.6 Reporte de errores al usuario
**Descripción:** Presentar errores al usuario de forma clara, accionable y no técnica.

**Cómo hacerlo:**
- Crear un `ErrorHandler` global que capture excepciones no controladas:
  - `Application.DispatcherUnhandledException` para excepciones de UI.
  - `TaskScheduler.UnobservedTaskException` para tareas asíncronas.
  - `AppDomain.CurrentDomain.UnhandledException` como último recurso.
- Traducir excepciones técnicas a mensajes amigables:
  - `NpgsqlException` con código `28P01` → "Contraseña incorrecta para el usuario X".
  - `SocketException` → "No se pudo conectar al servidor. Verifica que el host y puerto son correctos."
  - `TimeoutException` → "La consulta excedió el tiempo límite de X segundos."
  - Diccionario de mapeo `ExceptionCode → UserMessage` por proveedor.
- Diálogo de error con:
  - Mensaje amigable principal.
  - Botón "Detalles" que expande información técnica (tipo de excepción, stack trace).
  - Botón "Copiar al portapapeles" para reportar el error.
  - Botón "Abrir log" para ver el archivo de log.
- Panel de mensajes en cada pestaña de consulta para errores de SQL:
  - Número de línea del error, código de error, mensaje.
  - Clic en el error posiciona el cursor en la línea correspondiente del editor.

### 6.7 Herramientas de monitoreo de base de datos
**Descripción:** Panel de monitoreo que muestra el estado y rendimiento de la base de datos conectada.

**Cómo hacerlo:**
- **Dashboard de monitoreo** como tipo de pestaña especial (`MonitorTabViewModel`):
- **PostgreSQL:**
  - Conexiones activas: `SELECT * FROM pg_stat_activity`.
  - Consultas lentas: `pg_stat_statements` (si extensión habilitada).
  - Tamaño de BD y tablas: `pg_database_size()`, `pg_total_relation_size()`.
  - Locks activos: `pg_locks`.
  - Replicación: `pg_stat_replication`.
  - Cache hit ratio: consulta sobre `pg_stat_user_tables`.
- **MySQL/MariaDB:**
  - Procesos activos: `SHOW PROCESSLIST`.
  - Variables de estado: `SHOW GLOBAL STATUS`.
  - Tamaño de BDs: `information_schema.TABLES`.
  - Slow query log: configuración y lectura.
  - InnoDB status: `SHOW ENGINE INNODB STATUS`.
- **SQLite:**
  - Tamaño del archivo.
  - `pragma integrity_check`.
  - `pragma page_count`, `pragma page_size`.
  - WAL status.
- Visualización:
  - Gráficos en tiempo real (LiveCharts): conexiones activas, queries/segundo, uso de memoria.
  - Refresh automático configurable (cada 5s, 10s, 30s, manual).
  - Tabla de procesos con botón "Kill" para terminar consultas bloqueadas.
  - Alertas visuales si se superan umbrales (ej: >80% conexiones usadas).

### 6.8 Profiling de rendimiento de la aplicación
**Descripción:** Herramientas internas para identificar cuellos de botella en el rendimiento de la aplicación.

**Cómo hacerlo:**
- **Medición de tiempos:**
  - Instrumentar operaciones clave con `Stopwatch`:
    - Tiempo de conexión.
    - Tiempo de ejecución de consulta.
    - Tiempo de carga de esquema.
    - Tiempo de renderizado de grid.
    - Tiempo de importación/exportación.
  - Mostrar tiempos en barra de estado y logs.
- **Monitor de memoria:**
  - Mostrar uso de memoria de la aplicación en barra de estado.
  - Botón "Forzar GC" en modo debug.
  - Alertas si el uso de memoria supera un umbral.
- **Diagnóstico de consultas lentas:**
  - Si una consulta tarda más de N segundos (configurable), mostrar sugerencia de EXPLAIN.
  - Log automático de consultas que superan el umbral.
- **Panel de diagnóstico** (accesible desde menú Ayuda → Diagnóstico):
  - Versión de la aplicación y del runtime .NET.
  - Información del sistema operativo.
  - Versiones de los proveedores de BD.
  - Uso de memoria y CPU.
  - Conexiones abiertas y su estado.
  - Últimos errores del log.

### 6.9 Pipeline de CI/CD
**Descripción:** Configurar integración continua para ejecutar tests automáticamente y generar builds.

**Cómo hacerlo:**
- **GitHub Actions** (o Azure DevOps):
  - Workflow para cada push/PR:
    1. Restaurar paquetes NuGet.
    2. Build en modo Release.
    3. Ejecutar tests unitarios.
    4. Ejecutar tests de integración (con servicios Docker).
    5. Generar informe de cobertura.
    6. Publicar artefactos (instalador, portable).
  - Workflow de release:
    1. Tag de versión.
    2. Build Release.
    3. Crear instalador (MSIX o InnoSetup).
    4. Crear release en GitHub con artefactos.
- **Archivo de configuración:**
  - `.github/workflows/ci.yml` para CI.
  - `.github/workflows/release.yml` para releases.
- **Calidad de código:**
  - Integrar analizadores de Roslyn (`.editorconfig`).
  - Ejecutar `dotnet format` como paso de CI.
  - Reportar warnings como errores en CI.

---

## Checklist de Progreso

- [x] 6.1 Arquitectura de pruebas
  - [x] Crear proyectos de test en la solución
  - [x] Configurar NUnit + Moq + FluentAssertions
  - [x] Configurar Coverlet + ReportGenerator
  - [x] Definir convenciones de naming y estructura
- [x] 6.2 Tests unitarios
  - [x] Tests de `DatabaseProviderFactory`
  - [x] Tests de mapeo de tipos entre motores
  - [x] Tests de generación SQL (CREATE/ALTER TABLE)
  - [x] Tests del parser de sentencias SQL
  - [x] Tests de ViewModels principales
  - [x] Tests de servicios (ConnectionRepository, QueryHistory, Export)
  - [x] Tests de utilidades (formateo, cifrado, conversiones)
- [ ] 6.3 Tests de integración con bases de datos
  - [ ] Configurar Testcontainers
  - [ ] Tests con PostgreSQL real
  - [ ] Tests con MySQL real
  - [ ] Tests con MariaDB real
  - [ ] Tests con SQLite
  - [ ] Tests de comparación/sincronización
  - [ ] Tests de migración entre motores
- [x] 6.4 Tests de UI
  - [x] Tests de ViewModels con mocks
  - [x] Verificar bindings y commands
  - [x] Tests de navegación entre pestañas
  - [ ] (Opcional) Tests con FlaUI/Appium
- [x] 6.5 Sistema de logging
  - [x] Configurar Serilog con sinks (archivo, consola)
  - [x] Definir niveles y formato de log
  - [x] Información contextual (ConnectionId, QueryId)
  - [x] Visor de logs en la aplicación
  - [ ] Configuración de nivel de log por el usuario
  - [x] Protección de datos sensibles
- [x] 6.6 Reporte de errores al usuario
  - [x] `ErrorHandler` global para excepciones no controladas
  - [x] Diccionario de traducción de errores por proveedor
  - [x] Diálogo de error amigable con detalles expandibles
  - [x] Panel de mensajes de error en pestaña de consulta
  - [x] Navegación a línea de error en el editor
- [x] 6.7 Herramientas de monitoreo de base de datos
  - [x] Dashboard de monitoreo (pestaña especial)
  - [x] Métricas PostgreSQL (pg_stat_activity, sizes, locks)
  - [x] Métricas MySQL/MariaDB (processlist, status, InnoDB)
  - [x] Métricas SQLite (integrity, pages, WAL)
  - [ ] Gráficos en tiempo real
  - [x] Tabla de procesos con Kill
  - [ ] Alertas por umbrales
- [x] 6.8 Profiling de rendimiento
  - [x] Instrumentación de tiempos con Stopwatch
  - [x] Monitor de memoria en barra de estado
  - [ ] Detección de consultas lentas
  - [x] Panel de diagnóstico (info sistema, conexiones, errores)
- [x] 6.9 Pipeline de CI/CD
  - [x] GitHub Actions workflow de CI
  - [x] Ejecución de tests unitarios en CI
  - [ ] Ejecución de tests de integración con Docker
  - [x] Informe de cobertura
  - [x] Workflow de release con instalador
  - [ ] Analizadores de Roslyn y formateo
