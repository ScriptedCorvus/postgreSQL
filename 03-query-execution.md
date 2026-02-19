# 03 - Query Execution Agent

[← Volver al índice](agents.md)

---

## Objetivo
Desarrollar el motor de ejecución de consultas SQL con soporte para ejecución asíncrona, cancelación, timeout, manejo de grandes volúmenes de datos, análisis de rendimiento y exportación de resultados.

---

## Tareas Detalladas

### 3.1 Motor de ejecución de consultas
**Descripción:** Implementar el núcleo del motor de ejecución que reciba sentencias SQL y devuelva resultados de forma eficiente y asíncrona.

**Cómo hacerlo:**
- Crear interfaz `IQueryExecutor` con métodos:
  - `ExecuteQueryAsync(string sql, CancellationToken ct)` → devuelve `QueryResult`.
  - `ExecuteNonQueryAsync(string sql, CancellationToken ct)` → devuelve `int` (filas afectadas).
  - `ExecuteScalarAsync(string sql, CancellationToken ct)` → devuelve `object`.
- Clase `QueryResult` con:
  - `DataTable ResultSet`, `int RowsAffected`, `TimeSpan ExecutionTime`, `string[] Messages`, `QueryError[] Errors`.
- Implementar la ejecución usando `DbCommand.ExecuteReaderAsync()`:
  - Leer resultados con `DbDataReader` y poblar `DataTable` de forma streaming.
  - Para múltiples result sets, usar `reader.NextResultAsync()`.
- Soporte para múltiples sentencias separadas por `;`:
  - Parsear el texto y ejecutar cada sentencia secuencialmente.
  - Devolver un `QueryResult` por sentencia.
- Todo debe ejecutarse en un hilo secundario para no bloquear la UI.

### 3.2 Cancelación de consultas
**Descripción:** Permitir al usuario cancelar una consulta en ejecución en cualquier momento.

**Cómo hacerlo:**
- Usar `CancellationTokenSource` por cada ejecución de consulta.
- Botón "Cancelar" en la UI que llama a `cancellationTokenSource.Cancel()`.
- Además, llamar a `DbCommand.Cancel()` para enviar señal de cancelación al servidor.
- Para PostgreSQL: Npgsql soporta `NpgsqlCommand.Cancel()`.
- Para MySQL: MySqlConnector soporta `MySqlCommand.Cancel()`.
- Mostrar estado en la UI: "Ejecutando…" → "Cancelando…" → "Cancelada".
- Timeout configurable por consulta (por defecto 30 segundos, configurable globalmente y por pestaña).
- Implementar `CommandTimeout` en el `DbCommand`.

### 3.3 Consultas parametrizadas
**Descripción:** Soporte para ejecutar consultas con parámetros, evitando inyección SQL y mejorando el rendimiento.

**Cómo hacerlo:**
- Detectar parámetros en la consulta (sintaxis: `@param` o `:param` según proveedor).
- Mostrar un panel/diálogo donde el usuario introduce los valores de los parámetros antes de ejecutar.
- Crear `DbParameter` por cada parámetro detectado y asignarlo al `DbCommand`.
- Inferir tipo de dato del parámetro cuando sea posible.
- Opción de guardar conjuntos de parámetros como "presets" para reutilizar.

### 3.4 Ejecución de scripts SQL
**Descripción:** Soporte para ejecutar scripts completos con múltiples sentencias, transacciones y control de flujo.

**Cómo hacerlo:**
- Parsear el script SQL para separar sentencias individuales:
  - Respetar delimitadores (`;` por defecto, `DELIMITER` personalizado en MySQL).
  - Ignorar `;` dentro de strings, comentarios y bloques `BEGIN...END`.
- Opciones de ejecución:
  - Ejecutar todo el script.
  - Ejecutar solo la sentencia donde está el cursor.
  - Ejecutar la selección.
- Modo transaccional (opcional):
  - Envolver todo el script en `BEGIN TRANSACTION ... COMMIT`.
  - Hacer `ROLLBACK` si alguna sentencia falla.
- Mostrar progreso: "Ejecutando sentencia 3 de 15…".
- Panel de mensajes/salida que muestra mensajes del servidor y errores por sentencia.

### 3.5 Historial de consultas
**Descripción:** Registrar todas las consultas ejecutadas para poder consultarlas, reutilizarlas y analizar el historial.

**Cómo hacerlo:**
- Crear modelo `QueryHistoryEntry`:
  - `DateTime ExecutedAt`, `string Sql`, `string ConnectionName`, `string Database`
  - `TimeSpan Duration`, `int RowsAffected`, `bool Success`, `string ErrorMessage`
- Almacenar en una base de datos SQLite local (`%AppData%/DbClient/history.db`).
- UI: Panel/pestaña de historial con:
  - Lista de consultas ordenadas por fecha (más reciente primero).
  - Filtros: por conexión, por fecha, búsqueda en texto SQL.
  - Doble clic para copiar al editor activo.
  - Botón "Re-ejecutar".
- Límite configurable de entradas (por defecto 10,000).
- Opción de limpiar historial.

### 3.6 Favoritos / Snippets de consultas
**Descripción:** Permitir guardar consultas frecuentes como favoritos o snippets reutilizables.

**Cómo hacerlo:**
- Modelo `QuerySnippet`: `Name`, `Sql`, `Description`, `Tags`, `DatabaseType`.
- Almacenar en JSON o SQLite local.
- UI: Panel lateral o diálogo de snippets:
  - Organización por carpetas/tags.
  - Búsqueda por nombre o contenido.
  - Insertar snippet en el editor con doble clic o atajo de teclado.
- Snippets predefinidos por motor de BD:
  - PostgreSQL: listar tablas, tamaño de BD, conexiones activas, etc.
  - MySQL: `SHOW TABLES`, `SHOW PROCESSLIST`, etc.
  - SQLite: `pragma table_info()`, etc.

### 3.7 Exportación de resultados
**Descripción:** Exportar los resultados de consultas a múltiples formatos.

**Cómo hacerlo:**
- Formatos soportados:
  - **CSV:** Usar `CsvHelper` o implementación propia. Opciones: delimitador, entrecomillado, encoding.
  - **Excel (.xlsx):** Usar `ClosedXML` o `EPPlus`. Formateo de tipos, anchos de columna auto.
  - **JSON:** Array de objetos. Opciones: formato pretty/compact.
  - **XML:** Formato DataSet XML estándar.
  - **SQL (INSERT):** Generar sentencias `INSERT INTO` para cada fila.
  - **Markdown:** Tabla con formato Markdown.
  - **HTML:** Tabla HTML con estilos básicos.
- Diálogo de exportación con:
  - Selector de formato.
  - Opciones específicas por formato.
  - Selección de columnas a exportar.
  - Preview de las primeras filas.
  - Ruta de destino y nombre de archivo.
- Soporte para exportar selección o todos los resultados.

**Paquetes NuGet:**
- `CsvHelper`
- `ClosedXML` o `EPPlus`

### 3.8 Análisis de rendimiento de consultas (EXPLAIN / Query Plan)
**Descripción:** Herramientas para analizar el plan de ejecución de consultas y optimizar rendimiento.

**Cómo hacerlo:**
- **PostgreSQL:** Ejecutar `EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)` y parsear el resultado.
- **MySQL/MariaDB:** Ejecutar `EXPLAIN FORMAT=JSON` y parsear.
- **SQLite:** Ejecutar `EXPLAIN QUERY PLAN`.
- Visualización del plan:
  - Vista de árbol con nodos del plan (Seq Scan, Index Scan, Hash Join, etc.).
  - Colorear nodos según costo (verde = rápido, rojo = lento).
  - Mostrar estimaciones vs. valores reales (ANALYZE).
  - Tiempo y filas por nodo.
- Botón "Explain" junto a "Execute" en la barra de herramientas del editor.
- Pestaña "Query Plan" junto a "Results" y "Messages".

### 3.9 Autocompletado inteligente en el editor
**Descripción:** Proveer autocompletado contextual basado en el esquema de la base de datos conectada.

**Cómo hacerlo:**
- Mantener un caché del esquema de la BD activa: tablas, columnas, funciones, tipos.
- Refrescar el caché al abrir una conexión y bajo demanda.
- Reglas de autocompletado contextual:
  - Después de `SELECT`: sugerir columnas de las tablas en el `FROM`.
  - Después de `FROM` / `JOIN`: sugerir tablas y vistas.
  - Después de `WHERE`: sugerir columnas.
  - Después de `.` (alias o tabla): sugerir columnas de esa tabla.
  - Palabras clave SQL siempre disponibles.
  - Funciones del motor de BD.
- Usar `CompletionWindow` de AvalonEdit.
- Trigger: al escribir o con `Ctrl+Space`.

---

## Checklist de Progreso

- [x] 3.1 Motor de ejecución de consultas
  - [x] Interfaz `IQueryExecutor`
  - [x] Clase `QueryResult`
  - [x] Ejecución asíncrona con `DbDataReader`
  - [x] Soporte para múltiples result sets
  - [x] Soporte para múltiples sentencias
  - [x] Ejecución en hilo secundario
- [x] 3.2 Cancelación de consultas
  - [x] `CancellationTokenSource` por ejecución
  - [x] Botón cancelar en UI
  - [x] `DbCommand.Cancel()` por proveedor
  - [x] Timeout configurable
  - [x] Indicador de estado en UI
- [x] 3.3 Consultas parametrizadas
  - [x] Detección de parámetros en consulta
  - [x] Diálogo de entrada de parámetros
  - [x] Creación de `DbParameter`
  - [x] Inferencia de tipo de dato
  - [x] Presets de parámetros
- [x] 3.4 Ejecución de scripts SQL
  - [x] Parser de sentencias (respetar strings, comentarios, bloques)
  - [x] Ejecución total / selección / sentencia actual
  - [x] Modo transaccional opcional
  - [x] Indicador de progreso por sentencia
  - [x] Panel de mensajes/salida
- [x] 3.5 Historial de consultas
  - [x] Modelo QueryHistoryEntry
  - [x] Almacenamiento en SQLite local
  - [x] UI de historial con filtros y búsqueda
  - [x] Re-ejecutar desde historial
  - [x] Límite y limpieza de historial
- [x] 3.6 Favoritos / Snippets de consultas
  - [x] Modelo `QuerySnippet`
  - [x] UI de gestión de snippets
  - [x] Snippets predefinidos por motor
  - [x] Insertar snippet en editor
- [x] 3.7 Exportación de resultados
  - [x] Exportar a CSV
  - [x] Exportar a Excel (.xlsx)
  - [x] Exportar a JSON
  - [x] Exportar a XML
  - [x] Exportar a SQL (INSERT)
  - [x] Exportar a Markdown
  - [x] Exportar a HTML
  - [x] Diálogo de exportación con opciones
- [x] 3.8 Análisis de rendimiento (EXPLAIN)
  - [x] EXPLAIN para PostgreSQL
  - [x] EXPLAIN para MySQL/MariaDB
  - [x] EXPLAIN QUERY PLAN para SQLite
  - [x] Visualización en árbol del plan
  - [x] Coloreo por costo
  - [x] Pestaña "Query Plan" en resultados
- [x] 3.9 Autocompletado inteligente
  - [x] Caché del esquema de BD
  - [x] Autocompletado contextual (SELECT, FROM, WHERE, alias)
  - [x] Palabras clave SQL
  - [x] Funciones del motor
  - [x] Trigger manual (Ctrl+Space) y automático
