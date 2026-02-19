# 01 - UI/UX Design Agent

[← Volver al índice](agents.md)

---

## Objetivo
Diseñar e implementar una interfaz de usuario moderna, intuitiva y responsive para la aplicación cliente de bases de datos, replicando la experiencia de Navicat con soporte de pestañas, paneles y personalización.

---

## Tareas Detalladas

### 1.1 Estructura principal de la ventana (Shell)
**Descripción:** Crear la ventana principal con layout de paneles redimensionables: barra de herramientas superior, panel lateral de conexiones (TreeView), área central de pestañas y barra de estado inferior.

**Cómo hacerlo:**
- Crear un proyecto WPF con .NET 10 usando MVVM (CommunityToolkit.Mvvm o Prism).
- Usar `Grid` con `GridSplitter` para dividir la ventana en paneles redimensionables.
- Implementar `DockPanel` o usar AvalonDock para un sistema de paneles acoplables estilo IDE.
- La barra de herramientas superior debe usar `ToolBar` con botones de acciones rápidas (nueva conexión, nueva consulta, ejecutar, etc.).
- La barra de estado inferior muestra información de la conexión activa, número de filas, tiempo de ejecución, etc.

**Paquetes NuGet recomendados:**
- `AvalonDock` (Xceed) para paneles acoplables.
- `MahApps.Metro` para estilos modernos de ventana.
- `MaterialDesignThemes` para iconografía y controles estilizados.

### 1.2 Sistema de pestañas (Tab System)
**Descripción:** Implementar un sistema de pestañas que permita abrir múltiples consultas, tablas y vistas simultáneamente, con soporte para cerrar, reordenar y restaurar pestañas.

**Cómo hacerlo:**
- Usar `TabControl` personalizado o AvalonDock `DocumentPane`.
- Cada pestaña se representa con un ViewModel que hereda de una clase base `TabViewModel`.
- Tipos de pestañas: `QueryTabViewModel`, `TableDataTabViewModel`, `TableDesignerTabViewModel`, `DiagramTabViewModel`.
- Implementar botón de cierre en cada pestaña con confirmación si hay cambios sin guardar.
- Soporte para arrastrar y reordenar pestañas (drag & drop en el `TabControl`).
- Menú contextual en las pestañas: cerrar, cerrar todas, cerrar otras, cerrar las de la derecha.

### 1.3 Panel de conexiones (Connection Tree)
**Descripción:** Panel lateral tipo TreeView que muestra las conexiones guardadas y su estructura jerárquica (servidor → base de datos → esquemas → tablas/vistas/funciones).

**Cómo hacerlo:**
- Usar `TreeView` con `HierarchicalDataTemplate` para cada nivel de la jerarquía.
- Modelo de datos jerárquico: `ConnectionNode` → `DatabaseNode` → `SchemaNode` → `TableNode`, `ViewNode`, `FunctionNode`, `IndexNode`, etc.
- Implementar carga diferida (lazy loading): los nodos hijos se cargan solo al expandir.
- Iconos diferenciados para cada tipo de objeto (usar `PackIcon` de MaterialDesign).
- Menú contextual por tipo de nodo:
  - Conexión: abrir, cerrar, editar, duplicar, eliminar.
  - Base de datos: nueva consulta, nueva tabla, importar, exportar.
  - Tabla: abrir datos, diseñar, truncar, eliminar, exportar.
- Doble clic para abrir datos de una tabla en una nueva pestaña.
- Filtro/búsqueda de objetos en el panel.

### 1.4 Editor de consultas SQL (Query Editor)
**Descripción:** Editor de texto avanzado con resaltado de sintaxis SQL, autocompletado, numeración de líneas y soporte para múltiples sentencias.

**Cómo hacerlo:**
- Integrar `AvalonEdit` como componente de editor de texto.
- Configurar resaltado de sintaxis SQL (AvalonEdit incluye definición de sintaxis personalizable vía XML).
- Implementar autocompletado con `CompletionWindow` de AvalonEdit:
  - Palabras clave SQL (`SELECT`, `FROM`, `WHERE`, etc.).
  - Nombres de tablas, columnas y funciones obtenidos del esquema de la conexión activa.
- Numeración de líneas habilitada por defecto.
- Soporte para ejecutar solo la sentencia seleccionada o todas las sentencias.
- Resaltado de la sentencia actual bajo el cursor.
- Historial de consultas ejecutadas.
- Atajos de teclado: `Ctrl+Enter` ejecutar, `Ctrl+S` guardar, `Ctrl+Shift+F` formatear SQL.

**Paquetes NuGet recomendados:**
- `AvalonEdit` para el editor de texto.

### 1.5 Grid de resultados (Data Grid)
**Descripción:** Tabla de datos para mostrar resultados de consultas y contenido de tablas, con soporte para ordenar, filtrar, editar inline y copiar datos.

**Cómo hacerlo:**
- Usar `DataGrid` de WPF personalizado o integrar una librería avanzada.
- Columnas auto-generadas según el resultado de la consulta.
- Soporte para ordenar por columna (clic en cabecera).
- Filtro por columna (campo de texto en la cabecera).
- Edición inline de celdas con validación de tipo.
- Paginación virtual para grandes volúmenes de datos (UI Virtualization).
- Menú contextual en celdas: copiar celda, copiar fila, copiar como INSERT, copiar como CSV.
- Resaltado de valores NULL con estilo visual diferenciado.
- Barra de estado del grid: número de filas, filas seleccionadas, tiempo de carga.

### 1.6 Visualización de datos y gráficos
**Descripción:** Herramientas para visualizar resultados de consultas en forma de gráficos (barras, líneas, tartas, etc.).

**Cómo hacerlo:**
- Integrar `LiveCharts2` o `OxyPlot` para generación de gráficos.
- Pestaña de visualización junto a la de resultados en cada consulta.
- El usuario selecciona columnas para ejes X/Y y tipo de gráfico.
- Soporte para exportar gráficos como imagen (PNG, SVG).

**Paquetes NuGet recomendados:**
- `LiveChartsCore.SkiaSharpView.WPF` o `OxyPlot.Wpf`.

### 1.7 Editor de diagramas ER
**Descripción:** Editor visual para crear y editar diagramas Entidad-Relación a partir del esquema de la base de datos.

**Cómo hacerlo:**
- Crear un canvas personalizado (`Canvas` o `ItemsControl` con `Canvas` como panel).
- Cada tabla es un control visual arrastrable que muestra nombre y columnas.
- Las relaciones (foreign keys) se representan con líneas/conectores entre tablas.
- Reverse engineering: generar diagrama automáticamente desde el esquema de la base de datos.
- Forward engineering: generar script SQL desde el diagrama.
- Exportar diagrama como imagen (PNG, SVG, PDF).
- Zoom y pan en el canvas.

### 1.8 Interfaz multilingüe
**Descripción:** Soporte para múltiples idiomas en la interfaz de usuario.

**Cómo hacerlo:**
- Usar archivos de recursos `.resx` para cada idioma soportado.
- Implementar un `LocalizationManager` que permita cambiar el idioma en tiempo de ejecución.
- Binding en XAML a recursos localizados: `{x:Static props:Resources.MenuFile}`.
- Idiomas iniciales: Español, Inglés.
- Permitir al usuario seleccionar el idioma desde Configuración.

---

## Checklist de Progreso

- [x] 1.1 Estructura principal de la ventana (Shell)
  - [x] Crear proyecto WPF .NET 10 con estructura MVVM
  - [x] Implementar layout con paneles redimensionables
  - [x] Barra de herramientas superior
  - [x] Barra de estado inferior
  - [x] Integrar MahApps.Metro y MaterialDesign
- [x] 1.2 Sistema de pestañas
  - [x] TabControl personalizado o AvalonDock
  - [x] ViewModels base para pestañas
  - [x] Botón de cierre con confirmación
  - [x] Drag & drop para reordenar
  - [x] Menú contextual en pestañas
- [x] 1.3 Panel de conexiones (Connection Tree)
  - [x] TreeView jerárquico con HierarchicalDataTemplate
  - [x] Modelo de nodos (Connection, Database, Schema, Table, View, etc.)
  - [x] Lazy loading de nodos hijos
  - [x] Iconos por tipo de objeto
  - [x] Menús contextuales por tipo de nodo
  - [x] Filtro/búsqueda de objetos
- [x] 1.4 Editor de consultas SQL
  - [x] Integrar AvalonEdit
  - [x] Resaltado de sintaxis SQL
  - [x] Autocompletado (palabras clave + objetos de BD)
  - [x] Ejecución de sentencia seleccionada o todas
  - [x] Historial de consultas
  - [x] Atajos de teclado
- [x] 1.5 Grid de resultados
  - [x] DataGrid con columnas auto-generadas
  - [x] Ordenación y filtrado por columna
  - [x] Edición inline con validación
  - [x] Paginación virtual
  - [x] Menú contextual (copiar como INSERT, CSV, etc.)
  - [x] Resaltado de NULLs
- [x] 1.6 Visualización de datos y gráficos
  - [x] Integrar librería de gráficos
  - [x] Selección de ejes y tipo de gráfico
  - [x] Exportar gráficos como imagen
- [x] 1.7 Editor de diagramas ER
  - [x] Canvas con tablas arrastrables
  - [x] Conectores de relaciones (foreign keys)
  - [x] Reverse engineering desde esquema
  - [x] Forward engineering a script SQL
  - [x] Exportar diagrama como imagen
  - [x] Zoom y pan
- [x] 1.8 Interfaz multilingüe
  - [x] Archivos de recursos .resx por idioma
  - [x] LocalizationManager con cambio en runtime
  - [x] Idiomas: Español, Inglés
