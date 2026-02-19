# Agents for .NET WPF Database Client Application

## Objective
This document outlines the agents and their responsibilities for creating a .NET 10 WPF client application that supports PostgreSQL, MySQL, MariaDB, and SQLite. The application will aim to replicate the features and usability of Navicat, including a tabbed interface and connection management system.

---

## Índice de Tareas Detalladas

| # | Agente | Archivo detallado |
|---|--------|--------------------|
| 1 | UI/UX Design | [01-ui-ux-design.md](01-ui-ux-design.md) |
| 2 | Database Connectivity | [02-database-connectivity.md](02-database-connectivity.md) |
| 3 | Query Execution | [03-query-execution.md](03-query-execution.md) |
| 4 | Data Management | [04-data-management.md](04-data-management.md) |
| 5 | Settings and Preferences | [05-settings-preferences.md](05-settings-preferences.md) |
| 6 | Testing and Debugging | [06-testing-debugging.md](06-testing-debugging.md) |

---

## Agents

### 1. **UI/UX Design Agent** → [Ver detalle](01-ui-ux-design.md)
**Responsibilities:**
- Design a modern, intuitive, and responsive user interface.
- Implement a tabbed interface for managing multiple database connections and queries.
- Ensure usability features similar to Navicat, such as drag-and-drop support, context menus, and customizable layouts.

**Key Features:**
- Tabbed interface for connections and queries.
- Connection management panel (TreeView jerárquico con lazy loading).
- Query editor with syntax highlighting and autocomplete (AvalonEdit).
- Data visualization tools (gráficos con LiveCharts).
- Diagram editor for database modeling (ER diagrams).
- Interfaz multilingüe (Español, Inglés).

---

### 2. **Database Connectivity Agent** → [Ver detalle](02-database-connectivity.md)
**Responsibilities:**
- Implement database connection logic for PostgreSQL, MySQL, MariaDB, and SQLite.
- Ensure secure and efficient connection handling.
- Support multiple simultaneous connections.

**Key Features:**
- Connection pooling (nativo de Npgsql y MySqlConnector).
- Secure authentication mechanisms (SCRAM-SHA-256, caching_sha2_password, etc.).
- Support for SSL/TLS connections.
- SSH Tunneling (SSH.NET).
- Error handling and automatic reconnection with exponential backoff.
- Support for cloud databases (Amazon RDS, Google Cloud SQL, Azure Database).
- Encrypted credential storage (DPAPI).

---

### 3. **Query Execution Agent** → [Ver detalle](03-query-execution.md)
**Responsibilities:**
- Execute SQL queries and return results efficiently.
- Provide support for query cancellation and timeout.
- Handle large datasets gracefully.

**Key Features:**
- Async query execution with progress indicators.
- Support for parameterized queries.
- Script execution (multiple statements, transactional mode).
- Query history (SQLite local storage).
- Saved queries / snippets.
- Export query results to various formats (CSV, Excel, JSON, XML, SQL, Markdown, HTML).
- Query performance analysis tools (EXPLAIN / Query Plan visualization).
- Intelligent autocomplete based on database schema.

---

### 4. **Data Management Agent** → [Ver detalle](04-data-management.md)
**Responsibilities:**
- Provide tools for managing database objects (tables, views, indexes, etc.).
- Support CRUD operations on data.
- Implement data import/export functionality.

**Key Features:**
- Database object explorer (tables, views, functions, triggers, sequences, indexes).
- Visual table designer (columns, PKs, indexes, FKs, constraints).
- Inline data editor with pagination and filtering.
- Data import wizard (CSV, Excel, JSON, XML, SQL dump).
- Data export wizard.
- Data comparison and synchronization tools.
- Schema comparison and synchronization.
- Migration tools for transferring data between different database engines.
- User and permission management.
- Task scheduler (backups, scripts, sync, exports).

---

### 5. **Settings and Preferences Agent** → [Ver detalle](05-settings-preferences.md)
**Responsibilities:**
- Manage application settings and user preferences.
- Provide options for customizing the UI and behavior.
- Implement a settings backup and restore feature.

**Key Features:**
- Centralized settings system (JSON persistence, migration between versions).
- Settings dialog with category navigation and search.
- Theme selection (light/dark/system) with accent color customization.
- Keyboard shortcuts customization with conflict detection.
- Connection templates (local + cloud providers).
- Collaboration features (export/import profiles, share connections and snippets).
- Backup and restore (automatic + manual).
- Notification system (toast, status bar, modals, badges).

---

### 6. **Testing and Debugging Agent** → [Ver detalle](06-testing-debugging.md)
**Responsibilities:**
- Ensure the application is thoroughly tested for functionality, performance, and security.
- Provide debugging tools for developers.

**Key Features:**
- Test architecture (NUnit, Moq, FluentAssertions, Coverlet).
- Unit tests for all business logic, ViewModels, and services.
- Integration tests with real databases (Testcontainers + Docker).
- UI tests for ViewModels (optional FlaUI for visual testing).
- Structured logging (Serilog with file + console sinks).
- User-friendly error reporting with error translation per provider.
- Database monitoring dashboard (connections, queries, sizes, locks).
- Application performance profiling (timing, memory, slow query detection).
- CI/CD pipeline (GitHub Actions: build, test, coverage, release).

---

## Development Plan
1. **Phase 1: UI/UX Design**
   - Create wireframes and prototypes.
   - Implement the tabbed interface and connection management panel.

2. **Phase 2: Database Connectivity**
   - Set up connection logic for all supported databases.
   - Test connections with sample databases.

3. **Phase 3: Query Execution**
   - Develop the query editor and execution engine.
   - Add support for exporting query results.

4. **Phase 4: Data Management**
   - Implement tools for managing database objects and data.
   - Add import/export functionality.

5. **Phase 5: Settings and Preferences**
   - Develop the settings panel and customization options.
   - Implement backup and restore for settings.

6. **Phase 6: Testing and Debugging**
   - Conduct thorough testing of all features.
   - Optimize performance and fix bugs.

---

## Tools and Technologies
- **Framework:** .NET 10, WPF
- **Database Libraries:** Npgsql (PostgreSQL), MySqlConnector (MySQL/MariaDB), System.Data.SQLite (SQLite)
- **UI Libraries:** MahApps.Metro, MaterialDesignInXAML
- **Testing Tools:** NUnit, Moq
- **Version Control:** Git

---

## Lessons Learned / Aprendizajes del Proyecto

### ✅ Qué SÍ hacer (Do's)

1. **Usar `.slnx` en .NET 10:** El formato de solución es `.slnx` (XML), no el antiguo `.sln`. Al ejecutar `dotnet build`, referenciar `DatabaseClient.slnx` explícitamente.
2. **Agregar `using System.IO;` explícitamente en WPF:** Los implicit usings de WPF no incluyen `System.IO`. Si usas `File`, `Path`, `Directory`, etc., agrega el using manualmente.
3. **Agregar `using System.Data;`** para `DataTable.AsEnumerable()` y el paquete NuGet `System.Data.DataSetExtensions`.
4. **Cambiar XAML Y code-behind al usar MetroWindow:** Para MahApps.Metro, hay que cambiar la raíz del XAML (`<mah:MetroWindow>`) Y la herencia en el `.cs` (`: MetroWindow`). Olvidar uno de los dos causa errores en runtime.
5. **AvalonEdit Text → Code-behind bridge:** La propiedad `Text` de AvalonEdit no es bindable directamente. Usar code-behind para sincronizar con el ViewModel (suscribirse a `TextChanged` y actualizar AvalonEdit en `DataContextChanged`).
6. **PasswordBox → Code-behind sync:** `PasswordBox.Password` no soporta binding por seguridad. Sincronizar vía evento `PasswordChanged` en code-behind.
7. **Delegate pattern para diálogos VM→View:** Usar un `Func<>` o `Action<>` delegate (ej: `ShowConnectionDialog`) que la View asigna en code-behind, permitiendo que el ViewModel solicite abrir diálogos sin acoplar a la UI.
8. **MaterialDesign PackIcon con `Kind` binding:** Para iconos dinámicos en TreeView, usar `{Binding DatabaseTypeIcon}` con propiedad `PackIconKind` en el ViewModel.
9. **Organizar ViewModels en jerarquías claras:** `ViewModelBase` → `TabViewModelBase` → `QueryTabViewModel`; `ConnectionNodeViewModel` → `DatabaseNodeViewModel`, etc.
10. **Usar `ConcurrentDictionary` en ConnectionManager:** Para manejar múltiples conexiones simultáneas de forma thread-safe.
11. **DPAPI (`ProtectedData`) para credenciales:** Cifrar contraseñas en `connections.json` con `DataProtectionScope.CurrentUser`. Es seguro y no requiere gestión de claves.
12. **Registrar TODO en servicios DI:** Cada nuevo servicio (`ISettingsService`, `IQueryHistoryService`, etc.) debe registrarse en `App.xaml.cs` como Singleton.

### ❌ Qué NO hacer (Don'ts)

1. **No duplicar `[RelayCommand]` en herencia:** CommunityToolkit.Mvvm genera error `MVVMTK0023` si un método con `[RelayCommand]` en una clase derivada tiene el mismo nombre que uno en la clase base. Definir el comando en UNA sola clase.
2. **No usar `.sln` para .NET 10:** El SDK espera `.slnx`. `dotnet new sln` ya crea `.slnx` por defecto.
3. **No intentar bindear AvalonEdit.Text en XAML:** No funciona. Siempre usar code-behind bridge.
4. **No intentar bindear PasswordBox.Password en XAML:** No funciona por diseño (seguridad). Siempre usar code-behind sync.
5. **No olvidar `System.Data.DataSetExtensions`:** Sin este paquete, `DataTable.AsEnumerable()` no compila. Agregarlo al `.csproj` donde se use.
6. **No asumir que `implicit usings` incluye todo:** WPF projects no incluyen `System.IO`, `System.Data`, ni `System.Text.Json` automáticamente. Verificar siempre.
7. **No hacer `async void` excepto en event handlers:** Siempre `async Task`. Los event handlers WPF (Click, etc.) son la única excepción aceptable.
8. **No crear archivos `.sln` manualmente:** Usar `dotnet new sln` (genera `.slnx`) y `dotnet sln add` para agregar proyectos.
9. **Evitar `System.Text.Json` como NuGet package en .NET 10:** Ya viene incluido en el framework. Agregar el paquete genera warning `NU1510`.
10. **No ignorar `CA1416` (DPAPI Windows-only):** Documentar que la app es Windows-only (WPF) y suprimir el warning si es necesario con `[SupportedOSPlatform("windows")]`.

### ✅ Do's (añadidos sesión 3+)

13. **Verificar interfaces antes de codificar:** Antes de usar métodos de `IConnectionManager` o `IDatabaseProvider`, leer la interfaz real. Los nombres de métodos difieren de patrones comunes (ej: `GetProviderAsync` no `GetProvider`, `ExecuteQueryAsync` no `ExecuteReader`).
14. **`QueryResult.ResultSet` es el DataTable:** La propiedad se llama `ResultSet`, NO `Data`. Verificar siempre nombres de propiedades en el modelo real.
15. **Wizards con patrón Step (CurrentStep int):** Para wizards multi-paso (Import, Export), usar un `int CurrentStep` con `Next()`/`Previous()` commands y paneles con `Visibility` bindeado a converters `IntEqualsToVisConverter`. Reutilizar converters existentes.
16. **Reutilizar converters entre diálogos:** Los converters como `StepFontWeightConverter`, `IntEqualsToVisConverter`, `InverseBoolConverter` ya están en App.xaml. No crear nuevos duplicados.
17. **Patrón Delegate para abrir diálogos desde ViewModels hijos:** Para nodos del TreeView (DatabaseNodeViewModel, TableNodeViewModel), usar delegates (`Action<...>`) que se propagan desde MainViewModel → ConnectionNodeViewModel → DatabaseNodeViewModel → TableNodeViewModel. Cada nivel debe pasar el delegate hacia abajo.
18. **SaveFileDialog con filtros por formato:** En wizards de exportación, construir el filtro del `SaveFileDialog` dinámicamente según el formato seleccionado para mejor UX.
19. **Keyboard shortcut service con catálogo estático:** Definir acciones predeterminadas en el constructor del servicio, persistir solo las customizaciones (delta) en settings.json.

### ❌ Don'ts (añadidos sesión 3+)

11. **No usar `result.Data` en QueryResult:** La propiedad es `ResultSet`. `Data` no existe y causa error de compilación.
12. **No asumir métodos sync en IConnectionManager:** Todas las operaciones son async (`GetProviderAsync`, `CloseConnectionAsync`). No existen versiones sync.
13. **No crear `CreateConnection()` en IDatabaseProvider:** El provider ya tiene una `Connection` property gestionada internamente. No expone factory methods para crear conexiones nuevas.
14. **No duplicar converters en XAML de diálogos:** Si un converter ya está registrado en App.xaml (como `NullOrEmptyToVisibilityConverter`), usarlo directamente con `{StaticResource}` sin redeclararlo.

### ✅ Do's (añadidos sesión 4+)

20. **`GetTablesAsync` retorna `DatabaseObjectInfo`, no `string`:** Usar `.Name` para obtener el nombre de tabla. No asumir que devuelve `IReadOnlyList<string>`.
21. **Row coloring en DataGrid con `LoadingRow` event:** Para colorear filas según estado (comparación, etc.), usar el evento `LoadingRow` en code-behind. Leer el valor de la columna Status desde `DataRowView` y asignar `e.Row.Background`.
22. **Propiedades computadas sobre `CurrentStep`:** Para textos dinámicos como `NextButtonText` o `ShowPreviousButton`, declarar como propiedades computed y llamar `OnPropertyChanged(nameof(...))` en `partial void OnCurrentStepChanged`.

### ❌ Don'ts (añadidos sesión 4+)

15. **No asumir que `GetTablesAsync` retorna strings:** Retorna `IReadOnlyList<DatabaseObjectInfo>`. Acceder a `.Name` para el nombre de tabla.
16. **No usar `IntEqualsToVisConverter` para "NOT equals":** El converter solo soporta igualdad. Para ocultar en step 1 y mostrar en 2+, usar una propiedad bool (ej: `ShowPreviousButton`) con `BoolToVisConverter`.

### ✅ Do's (añadidos sesión 5+)

23. **`ConnectionInfo.Id` es `Guid`, no `string`:** Cualquier modelo que referencie una conexión debe usar `Guid ConnectionId`, no `string`. La comparación `==` entre `Guid` y `string` no compila.
24. **Verificar constructores de ViewModels al instanciar desde MainWindow:** Antes de pasar 3 argumentos, verificar que el constructor acepta exactamente esos parámetros. Usar el mismo patrón que el ViewModel define.
25. **`ExecuteNonQueryAsync` retorna `int` directamente:** No retorna `QueryResult`. El `int` indica filas afectadas. No usar `.RowsAffected` sobre el resultado.

### ❌ Don'ts (añadidos sesión 5+)

17. **No usar `string` para `ConnectionId` en modelos:** `ConnectionInfo.Id` es `Guid`. Usar `Guid ConnectionId` para poder comparar con `==`.
18. **No pasar `IConnectionManager` a ViewModels que no lo necesitan:** Verificar constructor real del ViewModel antes de instanciarlo.

### ✅ Do's (añadidos sesión 6+)

26. **`SnackbarNotificationService` ahora requiere `ISettingsService` en constructor:** Inyectar vía DI. Lee `ShowToastNotifications` y `ToastDurationSeconds` de settings.
27. **`OpenFolderDialog` disponible en .NET 10 WPF:** No es necesario referenciar `System.Windows.Forms` para abrir diálogo de carpetas. Usar `Microsoft.Win32.OpenFolderDialog` directamente.
28. **ConnectionDialog rediseñado con TabControl (General/SSL/SSH):** Organizar propiedades de conexión en pestañas. Ocultar pestañas SSL y SSH cuando `DatabaseType == SQLite`.
29. **LiveCharts2 para gráficos:** Usar `LiveChartsCore.SkiaSharpView.WPF` (v2.0.0-rc4.5). CartesianChart/PieChart con Series binding. Cambiar entre tipos en runtime.
30. **ER Diagram con Canvas + code-behind:** Dibujar tablas como `Border` con `StackPanel` en Canvas. Usar `Canvas.SetLeft/SetTop` para posicionamiento. Mouse events para drag de tablas.
31. **Delegate pattern para abrir chart desde QueryTab:** Usar `Action<DataTable, string> OpenChartDelegate` en QueryTabViewModel, wired por MainViewModel vía `WireQueryTabDelegates()`.
32. **i18n con ResourceManager + .resx:** `Strings.resx` (inglés), `Strings.es.resx` (español). `LocalizationManager` singleton con `INotifyPropertyChanged` para actualizar bindings WPF al cambiar idioma.
33. **Row coloring en DataGrid con `LoadingRow` event:** Para estados (modified=amarillo, deleted=rojo, new=verde), usar el evento `LoadingRow` en code-behind con `SolidColorBrush` estáticas.
34. **`AutoGeneratingColumn` para NULL display:** Reemplazar columnas auto-generadas con `DataGridTemplateColumn` usando `TargetNullValue = "NULL"` en binding.
35. **FK queries por motor DB:** PostgreSQL usa `information_schema.table_constraints + key_column_usage + constraint_column_usage`. MySQL usa `INFORMATION_SCHEMA.KEY_COLUMN_USAGE WHERE REFERENCED_TABLE_NAME IS NOT NULL`. SQLite usa `PRAGMA foreign_key_list()`.

### ❌ Don'ts (añadidos sesión 6+)

19. **No usar `DataGridRow.TextBlock` — no existe:** Para strikethrough en filas eliminadas, usar `e.Row.IsEnabled = false` o modificar estilo con trigger. No existe propiedad `TextBlock` en `DataGridRow`.
20. **No mezclar `SelectionUnit="FullRow"` con edición de celdas:** Si se necesita editar celdas individuales, usar `SelectionUnit="CellOrRowHeader"`.
21. **No dibujar relaciones ER sin verificar que ambas tablas existen:** Siempre hacer `FirstOrDefault()` para source y target table antes de dibujar la línea.

### ✅ Do's (añadidos sesión 7 — Testing & Monitoring)

36. **`AppSettings.Theme` es `ThemeMode` enum (Light/Dark/System), no string:** En tests, comparar con `ThemeMode.Light`, no con `"Dark"`. Default es `ThemeMode.Light`.
37. **`AppSettings.EditorFontSize` (no `FontSize`):** La propiedad correcta es `EditorFontSize` (int, default 14) y `GridFontSize` (int, default 12). `FontSize` no existe.
38. **`QueryPlan.Nodes` es una lista, no `RootNode`:** Usar `plan.Nodes[0]` y `.Should().NotBeEmpty()`. No existe propiedad `RootNode`.
39. **`QueryResult.IsSuccessful` es computed (`=> Errors.Count == 0`):** Un `new QueryResult()` vacío es `IsSuccessful = true`. Solo se vuelve `false` al agregar errores.
40. **Mock `IsConnected` para tests de ConnectionManager:** `CloseConnectionAsync` verifica `provider.IsConnected` antes de cerrar. Si no se configura el mock con `.Returns(true)`, el close es un no-op.
41. **`SshTunnelManager` está en Data, no en Core:** `ConnectionManager` solo acepta `IDatabaseProviderFactory`. No pasar `SshTunnelManager` al constructor.
42. **Custom Serilog sink para log viewer:** Implementar `ILogEventSink` como singleton, usar `SetHandler(Action<LogEvent>)` para conectar UI con Thread Dispatcher.
43. **`System.Timers.Timer` para auto-refresh en dashboards:** Usar con `Dispatcher.InvokeAsync()` para actualizar UI desde el timer elapsed event.
44. **`GC.GetTotalMemory(false)` para memory monitor:** No fuerza GC. Actualizar cada 5 segundos con timer. Mostrar en status bar.
45. **Monitoring queries por motor:** PostgreSQL usa `pg_stat_activity`, `pg_locks`, `pg_database_size()`. MySQL usa `information_schema.PROCESSLIST`, `GLOBAL STATUS`. SQLite usa PRAGMAs.

### ❌ Don'ts (añadidos sesión 7)

22. **No asumir que `QueryResult.IsSuccessful` es `false` por defecto:** Es computed. Un resultado vacío sin errores es exitoso.
23. **No usar `SshTunnelManager` en Core tests:** Solo existe en el proyecto Data. ConnectionManager.Tests debe mockear solo `IDatabaseProviderFactory`.
24. **No olvidar `providerMock.Setup(p => p.IsConnected).Returns(true)` en tests:** Sin esto, `CloseConnectionAsync` no hace nada porque el provider reporta estar desconectado.

### 🔧 Tips técnicos

- **Build command:** `dotnet build DatabaseClient.slnx`
- **Run command:** `dotnet run --project src/DatabaseClient.App`
- **AppData path:** `%AppData%/DatabaseClient/` para settings.json, connections.json, history, backups
- **MaterialDesign + MahApps coexistencia:** Incluir ambos ResourceDictionaries en App.xaml. MahApps primero, luego MaterialDesign.
- **SelectTop SQL por engine:** PostgreSQL/SQLite usa `LIMIT 100`, MySQL/MariaDB usa `LIMIT 100`. Generar SQL específico en `TableNodeViewModel`.
- **NULL highlighting en DataGrid:** Reemplazar columnas auto-generadas con `DataGridTemplateColumn` en el evento `AutoGeneratingColumn`. Usar `DataTrigger` con `TargetNullValue` no funciona bien con DBNull.
- **Warnings aceptados:** `NU1510` (System.Text.Json innecesario), `CA1416` (DPAPI Windows-only) y `CS0067` (eventos no usados en ConnectionManager y QueryTabView) son esperados y documentados.
- **Export formats implementados:** CSV, Excel (.xlsx via ClosedXML), JSON, XML, SQL INSERT, Markdown, HTML — tanto desde QueryTab como desde DataExportWizard.
- **Keyboard shortcuts:** 23 acciones predeterminadas en 5 categorías. Customizaciones se guardan como `Dictionary<string,string>` en `AppSettings.KeyboardShortcuts`.
- **DI container pattern:** Todos los servicios se registran como Singleton en `App.xaml.cs`. Los ViewModels reciben servicios opcionales (`= null`) para facilitar testing y evolución.
- **LiveCharts2 NuGet:** `LiveChartsCore.SkiaSharpView.WPF` v2.0.0-rc4.5 en DatabaseClient.App.csproj. Soporta Bar, Line, Pie, Column, Scatter.
- **i18n resource files:** `src/DatabaseClient.App/Resources/Strings.resx` (en) y `Strings.es.resx` (es). 50+ claves traducidas.
- **ER Diagram Canvas rendering:** Se re-dibuja completo en cada cambio (Tables/Relationships CollectionChanged). Usa `DrawingVisual` + `RenderTargetBitmap` para exportar.
- **Test projects:** 3 proyectos: `DatabaseClient.Core.Tests` (114 tests), `DatabaseClient.Data.Tests` (5 tests), `DatabaseClient.UI.Tests` (9 tests). Total: 128 tests, todos pasando.
- **Log Viewer:** `LogViewerSink` (custom Serilog ILogEventSink) → `LogViewerTabViewModel` con filtrado por nivel, búsqueda, auto-scroll, cap de 10K entradas.
- **Error Panel en QueryTab:** `QueryErrorEntry` con Severity/Line/SqlState/Message/Detail. DataGrid en Messages tab con badge de errores. Double-click navega a la línea en AvalonEdit.
- **Monitoring Dashboard:** `MonitorTabViewModel` con queries específicas por motor (PG: pg_stat_activity, pg_locks; MySQL: PROCESSLIST, GLOBAL STATUS; SQLite: PRAGMAs). Auto-refresh configurable. Kill process.
- **Diagnostics Panel:** `DiagnosticsTabViewModel` con info de sistema (.NET, OS, versiones de providers, memoria, GC collections, conexiones activas). Botón "Copy All" para soporte.
- **Memory Monitor:** `GC.GetTotalMemory(false)` cada 5s en `MainViewModel._memoryTimer`. Se muestra en la barra de estado junto a conexiones activas.
- **CI/CD:** `.github/workflows/ci.yml` (build + test + coverage en push/PR) y `release.yml` (build + publish + GitHub Release en tag `v*`).

---


## Notes
- The application should prioritize performance and security.
- Ensure cross-platform compatibility where possible.
- Follow best practices for WPF development and MVVM architecture.

---

## Versioning System

Cada vez que se realice un cambio en el código o estructura del proyecto, se debe:

1. Incrementar el número de build de la versión (major.minor.build).
2. Registrar el cambio en el fichero `versions.json` en formato JSON:
    - version: string (ejemplo: "0.1.1")
    - build: int
    - date: string (YYYY-MM-DD)
    - changes: array de strings (descripción breve de los cambios)
3. El log de versiones debe ser incluido y visible en el programa (pantalla de About o similar).

Ejemplo de entrada en `versions.json`:
```
[
   {
      "version": "0.1.1",
      "build": 1,
      "date": "2026-02-19",
      "changes": [
         "Sistema de versionado inicial",
         "Registro de cambios en JSON"
      ]
   }
]
```

**Importante:**
- Cada modificación debe aumentar el build y registrar los cambios.
- El proceso debe ser documentado en cada commit.
- El sistema de versión es obligatorio para todos los agentes y tareas.