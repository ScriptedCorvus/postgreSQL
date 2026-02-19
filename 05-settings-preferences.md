# 05 - Settings and Preferences Agent

[← Volver al índice](agents.md)

---

## Objetivo
Gestionar todas las configuraciones de la aplicación y preferencias del usuario, incluyendo temas visuales, atajos de teclado, plantillas de conexión, colaboración entre equipos y backup/restore de configuración.

---

## Tareas Detalladas

### 5.1 Sistema de configuración centralizado
**Descripción:** Crear un sistema robusto de configuración que almacene todas las preferencias del usuario de forma persistente y permita acceso desde cualquier parte de la aplicación.

**Cómo hacerlo:**
- Crear clase `AppSettings` con todas las propiedades de configuración, organizada en secciones:
  - `GeneralSettings`: idioma, comportamiento al iniciar, actualizaciones automáticas.
  - `EditorSettings`: tamaño de fuente, familia de fuente, tabulación, mostrar números de línea, word wrap.
  - `GridSettings`: filas por página, formato de NULL, formato de fechas, truncar texto largo.
  - `ConnectionSettings`: timeout por defecto, reconexión automática, pool size.
  - `ExportSettings`: formato por defecto, encoding, delimitador CSV.
  - `ShortcutSettings`: mapeo de acciones a atajos de teclado.
- Almacenar en `%AppData%/DbClient/settings.json` con `System.Text.Json`.
- Implementar patrón `IOptions<T>` o un `SettingsService` singleton con:
  - `Load()`, `Save()`, `Reset()`, `GetSection<T>()`.
  - Evento `SettingsChanged` para que los componentes reaccionen a cambios.
- Valores por defecto sensatos para toda la configuración.
- Migración de configuración entre versiones de la aplicación.

### 5.2 Ventana de configuración (Settings Dialog)
**Descripción:** Diálogo de configuración con navegación por categorías, similar al de VS Code o Navicat.

**Cómo hacerlo:**
- Ventana modal con panel lateral de categorías (TreeView o ListBox):
  - General, Editor, Grid de datos, Conexiones, Exportación, Atajos de teclado, Apariencia, Avanzado.
- Cada categoría muestra un panel de opciones a la derecha.
- Controles apropiados por tipo de configuración:
  - ComboBox para selección (idioma, tema).
  - Slider/NumericUpDown para valores numéricos (tamaño de fuente, timeout).
  - CheckBox para booleanos (word wrap, números de línea).
  - TextBox para rutas/strings.
- Botones: "Aceptar" (guardar y cerrar), "Cancelar" (descartar), "Aplicar" (guardar sin cerrar), "Restaurar valores por defecto".
- Búsqueda de configuración por texto (filtrar opciones).
- Preview en tiempo real para opciones visuales (fuente, tema).

### 5.3 Temas y apariencia (Light/Dark mode)
**Descripción:** Soporte para temas claro y oscuro, con posibilidad de personalización de colores.

**Cómo hacerlo:**
- Usar `ResourceDictionary` de WPF para definir temas.
- Temas base:
  - **Light:** Fondo blanco/gris claro, texto oscuro.
  - **Dark:** Fondo gris oscuro/negro, texto claro.
  - **System:** Seguir la configuración del sistema operativo.
- Con MahApps.Metro:
  - Usar `ThemeManager.Current.ChangeTheme()` para cambiar tema en runtime.
  - Temas de acento (azul, verde, rojo, púrpura, etc.).
- Con MaterialDesign:
  - `BundledTheme` con `BaseTheme` y `PrimaryColor`/`SecondaryColor`.
- Colores personalizables por el usuario:
  - Color de acento principal.
  - Colores del editor SQL (sintaxis).
  - Colores del grid (filas alternas, selección).
- Persistir tema seleccionado en settings.
- Cambio de tema sin reiniciar la aplicación.

### 5.4 Personalización de atajos de teclado
**Descripción:** Permitir al usuario personalizar los atajos de teclado para todas las acciones de la aplicación.

**Cómo hacerlo:**
- Definir un catálogo de acciones (`ActionId`, `DefaultShortcut`, `Category`):
  - Generales: Nueva conexión, Nueva consulta, Guardar, Guardar todo, Cerrar pestaña.
  - Editor: Ejecutar consulta, Ejecutar selección, Formatear SQL, Comentar/descomentar, Autocompletar.
  - Navegación: Siguiente pestaña, Pestaña anterior, Ir al panel de conexiones, Ir al editor.
  - Datos: Aplicar cambios, Insertar fila, Eliminar fila, Refrescar datos.
- UI en Settings:
  - Grid con columnas: Acción, Categoría, Atajo actual, Atajo por defecto.
  - Para cambiar: clic en la celda de atajo y presionar la nueva combinación de teclas.
  - Detección de conflictos (dos acciones con el mismo atajo).
  - Botón "Restaurar por defecto" individual y global.
- Implementar con `InputBindings` y `KeyBindings` de WPF:
  - `InputBindingManager` que registra/actualiza bindings dinámicamente.
  - Al cambiar un atajo, actualizar los `InputBindings` de la ventana principal.
- Persistir mapeo en settings.

### 5.5 Plantillas de conexión
**Descripción:** Plantillas predefinidas y personalizables para crear conexiones rápidamente para diferentes entornos y proveedores.

**Cómo hacerlo:**
- Plantillas predefinidas (incluidas con la aplicación):
  - PostgreSQL local (localhost:5432)
  - MySQL local (localhost:3306)
  - MariaDB local (localhost:3306)
  - SQLite nuevo archivo
  - Amazon RDS PostgreSQL
  - Amazon RDS MySQL
  - Google Cloud SQL PostgreSQL
  - Google Cloud SQL MySQL
  - Azure Database for PostgreSQL
  - Azure Database for MySQL
- Cada plantilla define valores por defecto para: host, puerto, SSL, opciones adicionales.
- El usuario puede:
  - Crear sus propias plantillas desde una conexión existente ("Guardar como plantilla").
  - Editar y eliminar plantillas personalizadas.
  - Las plantillas predefinidas no se pueden eliminar pero sí ocultar.
- Al crear nueva conexión: diálogo presenta lista de plantillas como punto de partida.
- Almacenar plantillas personalizadas en `%AppData%/DbClient/templates.json`.

### 5.6 Colaboración y compartir configuraciones
**Descripción:** Facilitar el trabajo en equipo permitiendo compartir conexiones, snippets y configuraciones.

**Cómo hacerlo:**
- **Exportar/Importar perfil:**
  - Exportar toda la configuración (settings + conexiones + snippets + plantillas) a un archivo `.dbcprofile` (ZIP con JSONs).
  - Las contraseñas se cifran con una clave maestra que el usuario define al exportar.
  - Importar perfil: pedir clave maestra, fusionar o reemplazar configuración existente.
- **Compartir conexiones individuales:**
  - Exportar una conexión seleccionada a archivo `.dbcconnection` (JSON cifrado).
  - Importar conexión: arrastrar archivo a la aplicación o desde menú.
- **Compartir snippets:**
  - Exportar snippets seleccionados a archivo `.dbcsnippets`.
  - Importar snippets con opción de fusionar o reemplazar.
- **Proyecto compartido (avanzado):**
  - Definir un directorio de proyecto (ej: carpeta en repo Git).
  - Archivos compartidos: conexiones del proyecto (sin contraseñas), scripts SQL, snippets.
  - Los miembros del equipo abren el proyecto y usan sus propias credenciales.

### 5.7 Backup y restore de configuración
**Descripción:** Permitir respaldo y restauración completa de toda la configuración de la aplicación.

**Cómo hacerlo:**
- **Backup automático:**
  - Al actualizar la aplicación, crear backup automático de la configuración.
  - Mantener últimos N backups (configurable, por defecto 5).
  - Almacenar en `%AppData%/DbClient/backups/`.
- **Backup manual:**
  - Botón en Settings → "Crear backup ahora".
  - Seleccionar qué incluir: settings, conexiones, historial, snippets, plantillas.
  - Guardar como archivo comprimido con fecha.
- **Restore:**
  - Botón en Settings → "Restaurar desde backup".
  - Lista de backups disponibles (automáticos y manuales).
  - Seleccionar qué restaurar.
  - Confirmación antes de sobrescribir configuración actual.
- **Reset completo:**
  - Botón "Restaurar toda la configuración a valores de fábrica".
  - Crear backup automático antes de resetear.

### 5.8 Notificaciones y sistema de alertas
**Descripción:** Sistema de notificaciones en la aplicación para informar al usuario de eventos importantes.

**Cómo hacerlo:**
- Tipos de notificación:
  - **Toast/Snackbar:** Mensaje temporal en esquina inferior (éxito, info).
  - **Barra de estado:** Mensaje persistente breve en barra inferior.
  - **Diálogo modal:** Para errores críticos o confirmaciones.
  - **Badge/Icono:** Indicador en pestañas o panel (ej: tarea completada).
- Eventos que generan notificación:
  - Consulta ejecutada con éxito / con error.
  - Conexión establecida / perdida / reconectada.
  - Importación/exportación completada.
  - Tarea programada ejecutada.
  - Backup automático realizado.
- Configuración:
  - Activar/desactivar tipos de notificación.
  - Duración de las notificaciones toast.
  - Sonido de notificación (opcional).
- Usar `Snackbar` de MaterialDesign para notificaciones toast.

---

## Checklist de Progreso

- [x] 5.1 Sistema de configuración centralizado
  - [x] Clase AppSettings con todas las secciones
  - [x] Almacenamiento en JSON
  - [x] SettingsService singleton
  - [x] Evento SettingsChanged
  - [x] Valores por defecto
  - [x] Migración entre versiones
- [x] 5.2 Ventana de configuración
  - [x] Layout con categorías laterales
  - [x] Panel de opciones por categoría
  - [x] Controles apropiados por tipo
  - [x] Botones Aceptar/Cancelar/Aplicar/Reset
  - [x] Búsqueda de configuración
  - [x] Preview en tiempo real
- [x] 5.3 Temas y apariencia
  - [x] Tema Light
  - [x] Tema Dark
  - [x] Seguir tema del sistema
  - [x] Color de acento personalizable
  - [x] Colores de sintaxis del editor
  - [x] Cambio de tema sin reinicio
- [x] 5.4 Personalización de atajos de teclado
  - [x] Catálogo de acciones con atajos por defecto
  - [x] UI de edición de atajos
  - [x] Captura de combinación de teclas
  - [x] Detección de conflictos
  - [x] `InputBindingManager` dinámico
  - [x] Persistencia en settings
- [x] 5.5 Plantillas de conexión
  - [x] Plantillas predefinidas (local + cloud)
  - [x] Crear plantilla desde conexión existente
  - [x] Editar/eliminar plantillas personalizadas
  - [x] Diálogo de nueva conexión con selector de plantilla
  - [x] Almacenamiento en JSON
- [x] 5.6 Colaboración y compartir configuraciones
  - [x] Exportar/importar perfil completo (.dbcprofile)
  - [x] Cifrado con clave maestra
  - [x] Compartir conexiones individuales
  - [x] Compartir snippets
  - [x] Proyecto compartido (directorio)
- [x] 5.7 Backup y restore de configuración
  - [x] Backup automático al actualizar
  - [x] Backup manual con selección de contenido
  - [x] Restaurar desde backup
  - [x] Reset a valores de fábrica
  - [x] Gestión de backups (listar, eliminar)
- [x] 5.8 Notificaciones y sistema de alertas
  - [x] Toast/Snackbar con MaterialDesign
  - [x] Mensajes en barra de estado
  - [x] Diálogos modales para errores
  - [x] Badges/indicadores en pestañas
  - [x] Configuración de notificaciones
