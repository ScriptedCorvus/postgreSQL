# 04 - Data Management Agent

[← Volver al índice](agents.md)

---

## Objetivo
Proporcionar herramientas completas para la gestión de objetos de base de datos (tablas, vistas, índices, funciones, triggers, etc.), operaciones CRUD sobre datos, importación/exportación masiva, comparación, sincronización y migración entre bases de datos.

---

## Tareas Detalladas

### 4.1 Explorador de objetos de base de datos
**Descripción:** Permitir al usuario navegar y gestionar todos los objetos de una base de datos desde el panel de conexiones y pestañas dedicadas.

**Cómo hacerlo:**
- Implementar consultas de metadatos por proveedor para obtener objetos:
  - **PostgreSQL:** Usar `information_schema` y `pg_catalog` (`pg_tables`, `pg_views`, `pg_indexes`, `pg_proc`, `pg_trigger`).
  - **MySQL/MariaDB:** Usar `information_schema` (`TABLES`, `COLUMNS`, `STATISTICS`, `ROUTINES`, `TRIGGERS`).
  - **SQLite:** Usar `sqlite_master` y `pragma` (`table_info`, `index_list`, `foreign_key_list`).
- Objetos a listar por base de datos:
  - Tablas, Vistas, Funciones/Procedimientos almacenados, Triggers, Secuencias (PostgreSQL), Índices, Foreign Keys, Tipos personalizados, Extensiones (PostgreSQL).
- Para cada objeto, mostrar propiedades en un panel de detalles:
  - Tabla: columnas, índices, foreign keys, triggers, tamaño, número de filas estimado.
  - Vista: definición SQL.
  - Función: parámetros, cuerpo, lenguaje.
- Acciones disponibles por tipo de objeto (menú contextual y barra de herramientas).

### 4.2 Diseñador visual de tablas (Table Designer)
**Descripción:** Editor visual para crear y modificar la estructura de tablas sin escribir SQL manualmente.

**Cómo hacerlo:**
- Abrir como una pestaña dedicada (`TableDesignerTabViewModel`).
- Secciones del diseñador:
  - **Columnas:** Grid editable con: nombre, tipo de dato (combo con tipos del motor), longitud/precisión, nullable, valor por defecto, autoincremento, comentario.
  - **Primary Key:** Selección de columnas que forman la PK.
  - **Índices:** Grid para definir índices (nombre, columnas, tipo: BTREE/HASH/GIN/GiST, único).
  - **Foreign Keys:** Grid para definir FKs (columna local, tabla referenciada, columna referenciada, ON DELETE, ON UPDATE).
  - **Check Constraints:** Lista de expresiones de validación.
  - **Triggers:** Lista de triggers asociados.
  - **Opciones de tabla:** Engine (MySQL), tablespace (PostgreSQL), collation.
- Al guardar:
  - Si es tabla nueva: generar `CREATE TABLE` con todas las definiciones.
  - Si es tabla existente: generar `ALTER TABLE` con los cambios (añadir/modificar/eliminar columnas, índices, FKs).
  - Mostrar preview del SQL generado antes de ejecutar.
  - Opción de solo generar el script sin ejecutar.
- Validaciones:
  - No permitir nombres duplicados de columnas.
  - Verificar tipos de datos válidos para el motor.
  - Advertir sobre cambios destructivos (eliminar columna, cambiar tipo).

### 4.3 Visor y editor de datos de tablas
**Descripción:** Abrir el contenido de una tabla en una pestaña con grid editable para CRUD directo.

**Cómo hacerlo:**
- Al hacer doble clic en una tabla del TreeView, abrir pestaña con `DataGrid`.
- Cargar datos con `SELECT * FROM tabla LIMIT N OFFSET M` (paginación).
- Controles de paginación: primera, anterior, siguiente, última, ir a página.
- Número de filas por página configurable (25, 50, 100, 500, todas).
- **Edición inline:**
  - Al modificar una celda, marcar la fila como "modificada" (color amarillo).
  - Al añadir fila nueva, marcar como "nueva" (color verde).
  - Al eliminar, marcar como "eliminada" (color rojo, tachado).
  - Botón "Aplicar cambios" genera y ejecuta los `UPDATE`/`INSERT`/`DELETE` correspondientes.
  - Se necesita PK para generar `UPDATE` y `DELETE`; advertir si no hay PK.
- **Filtrado:**
  - Fila de filtros en la cabecera del grid.
  - Operadores: `=`, `!=`, `LIKE`, `>`, `<`, `IS NULL`, `IS NOT NULL`.
  - Aplicar filtro añadiendo `WHERE` a la consulta.
- **Ordenación:** Clic en cabecera para `ORDER BY` (ASC/DESC/ninguno).
- Menú contextual en celda/fila: copiar, copiar fila como INSERT, establecer NULL, eliminar fila.

### 4.4 Importación de datos
**Descripción:** Asistente (wizard) para importar datos desde archivos externos a tablas de la base de datos.

**Cómo hacerlo:**
- Formatos soportados: CSV, Excel (.xlsx), JSON, XML, SQL (dump).
- Wizard de 4 pasos:
  1. **Selección de archivo:** Elegir archivo y formato. Preview de las primeras filas.
  2. **Configuración de formato:** Delimitador (CSV), hoja (Excel), encoding, primera fila como cabecera.
  3. **Mapeo de columnas:** Asociar columnas del archivo con columnas de la tabla destino. Conversiones de tipo. Ignorar columnas. Crear tabla automáticamente si no existe.
  4. **Opciones de importación:** Modo (INSERT, INSERT IGNORE, UPSERT/ON CONFLICT), tamaño de lote, envolver en transacción, truncar tabla antes.
- Ejecución con barra de progreso, filas importadas/total, velocidad, tiempo estimado.
- Log de errores por fila (fila N: error de tipo en columna X).
- Resumen final: filas importadas, filas con error, tiempo total.

**Paquetes NuGet:**
- `CsvHelper`, `ClosedXML` o `EPPlus`

### 4.5 Exportación de datos
**Descripción:** Asistente para exportar datos de tablas o consultas a archivos.

**Cómo hacerlo:**
- Similar al descrito en 03-query-execution (3.7), pero orientado a tablas completas.
- Wizard de 3 pasos:
  1. **Selección de origen:** Tabla, vista o consulta personalizada. Filtro WHERE opcional.
  2. **Configuración de formato:** Formato destino y opciones específicas.
  3. **Selección de columnas:** Elegir qué columnas exportar, orden.
- Formatos: CSV, Excel, JSON, XML, SQL (INSERT/COPY), HTML, Markdown, PDF.
- Barra de progreso con filas exportadas.

### 4.6 Comparación y sincronización de datos
**Descripción:** Comparar los datos entre dos tablas (misma BD o distintas BDs) y sincronizarlos.

**Cómo hacerlo:**
- Wizard de configuración:
  1. Seleccionar conexión/BD/tabla origen y destino.
  2. Seleccionar columnas clave para la comparación (normalmente PK).
  3. Seleccionar columnas a comparar.
- Motor de comparación:
  - Leer datos de ambas tablas en lotes.
  - Clasificar filas en: solo en origen, solo en destino, en ambas pero diferentes, idénticas.
  - Mostrar resultados en grid con colores: verde (solo origen), rojo (solo destino), amarillo (diferente), blanco (igual).
- Sincronización:
  - Generar script SQL para sincronizar destino con origen (INSERT + UPDATE + DELETE).
  - Opciones: solo insertar nuevas, solo actualizar existentes, solo eliminar sobrantes, o combinaciones.
  - Preview del script antes de ejecutar.
  - Ejecutar con progreso.

### 4.7 Comparación y sincronización de esquemas
**Descripción:** Comparar la estructura (esquema) entre dos bases de datos y generar scripts de migración.

**Cómo hacerlo:**
- Wizard de configuración:
  1. Seleccionar conexión/BD origen y destino.
  2. Seleccionar tipos de objetos a comparar (tablas, vistas, funciones, índices, etc.).
- Motor de comparación:
  - Leer metadatos de ambas BDs.
  - Comparar objeto por objeto: existencia, columnas, tipos, índices, constraints.
  - Clasificar: solo en origen, solo en destino, diferente, idéntico.
- Resultado visual:
  - Árbol con objetos coloreados según estado.
  - Panel de detalle con diff del SQL de cada objeto.
- Generación de script de migración:
  - `CREATE TABLE/VIEW/FUNCTION` para objetos solo en origen.
  - `ALTER TABLE` para diferencias de columnas/índices.
  - `DROP` para objetos solo en destino (configurable).
  - Ordenar sentencias respetando dependencias (FKs).
- Preview y ejecución del script.

### 4.8 Herramientas de migración entre motores
**Descripción:** Migrar datos y esquemas entre distintos motores de base de datos (ej: MySQL → PostgreSQL).

**Cómo hacerlo:**
- Wizard de migración:
  1. Seleccionar conexión origen y destino (pueden ser motores distintos).
  2. Seleccionar objetos a migrar (tablas, vistas, datos).
  3. Mapeo de tipos de datos entre motores:
     - Mapeo automático con tabla de equivalencias predefinida.
     - Editable por el usuario para casos especiales.
     - Ej: MySQL `INT AUTO_INCREMENT` → PostgreSQL `SERIAL`, MySQL `DATETIME` → PostgreSQL `TIMESTAMP`.
  4. Opciones: migrar datos, migrar estructura, truncar destino, crear BD destino.
- Ejecución:
  - Crear esquema en destino.
  - Transferir datos tabla por tabla con progreso.
  - Log de errores y resumen final.
- Tabla de mapeo de tipos predefinida para todas las combinaciones:
  - PostgreSQL ↔ MySQL/MariaDB
  - PostgreSQL ↔ SQLite
  - MySQL/MariaDB ↔ SQLite

### 4.9 Gestión de usuarios y permisos
**Descripción:** Interfaz para administrar usuarios, roles y permisos de la base de datos.

**Cómo hacerlo:**
- **PostgreSQL:**
  - Listar roles con `pg_roles`.
  - Crear/modificar roles: `CREATE ROLE`, `ALTER ROLE`.
  - Asignar permisos: `GRANT`/`REVOKE` sobre objetos.
- **MySQL/MariaDB:**
  - Listar usuarios con `mysql.user`.
  - Crear/modificar: `CREATE USER`, `ALTER USER`.
  - Permisos: `GRANT`/`REVOKE`.
- **SQLite:** No aplica (sin gestión de usuarios).
- UI:
  - Lista de usuarios/roles en el TreeView bajo cada conexión.
  - Formulario de edición de usuario: nombre, contraseña, permisos por objeto (tabla/grid de checkboxes).
  - Visualización de permisos efectivos.

### 4.10 Programación de tareas (Scheduler)
**Descripción:** Permitir programar tareas automáticas como backups, ejecución de scripts y sincronizaciones.

**Cómo hacerlo:**
- Modelo `ScheduledTask`:
  - `Name`, `Type` (Backup, Script, Sync), `Schedule` (Cron expression o intervalo), `ConnectionId`, `Config` (JSON con parámetros específicos).
  - `LastRun`, `NextRun`, `LastStatus`, `Enabled`.
- Tipos de tareas:
  - **Backup:** Ejecutar `pg_dump`, `mysqldump` o copia de archivo SQLite.
  - **Script SQL:** Ejecutar un archivo .sql guardado.
  - **Sincronización:** Ejecutar una sincronización de datos configurada.
  - **Exportación:** Exportar tabla/consulta a archivo.
- Motor de planificación:
  - Usar `System.Threading.Timer` o `Quartz.NET` para disparar tareas según programación.
  - Ejecutar en segundo plano.
- UI:
  - Lista de tareas programadas con estado (activa, pausada, error).
  - Editor de tarea con formulario de configuración.
  - Log de ejecuciones pasadas.
  - Notificaciones de éxito/error (barra de estado o popup).

**Paquetes NuGet (opcional):**
- `Quartz` (scheduler avanzado)

---

## Checklist de Progreso

- [x] 4.1 Explorador de objetos de base de datos
  - [x] Consultas de metadatos para PostgreSQL
  - [x] Consultas de metadatos para MySQL/MariaDB
  - [x] Consultas de metadatos para SQLite
  - [x] Listado de todos los tipos de objetos
  - [x] Panel de propiedades por objeto
  - [x] Acciones contextuales por tipo de objeto
- [x] 4.2 Diseñador visual de tablas
  - [x] Grid de columnas editable
  - [x] Editor de Primary Key
  - [x] Editor de índices
  - [x] Editor de Foreign Keys
  - [x] Editor de Check Constraints
  - [x] Generación de CREATE TABLE
  - [x] Generación de ALTER TABLE (diff)
  - [x] Preview de SQL antes de ejecutar
  - [x] Validaciones de esquema
- [x] 4.3 Visor y editor de datos de tablas
  - [x] Carga paginada de datos
  - [x] Controles de paginación
  - [x] Edición inline (INSERT, UPDATE, DELETE)
  - [x] Marcado visual de cambios pendientes
  - [x] Aplicar cambios (generar SQL)
  - [x] Filtrado por columna con operadores
  - [x] Ordenación por columna
- [x] 4.4 Importación de datos
  - [x] Wizard paso a paso
  - [x] Soporte CSV
  - [x] Soporte Excel (.xlsx)
  - [x] Soporte JSON
  - [x] Soporte XML
  - [x] Soporte SQL dump
  - [x] Mapeo de columnas
  - [x] Modos de importación (INSERT, UPSERT)
  - [x] Barra de progreso y log de errores
- [x] 4.5 Exportación de datos
  - [x] Wizard de exportación
  - [x] Todos los formatos soportados
  - [x] Selección de columnas
  - [x] Filtro WHERE opcional
  - [x] Barra de progreso
- [x] 4.6 Comparación y sincronización de datos
  - [x] Wizard de configuración
  - [x] Motor de comparación por lotes
  - [x] Visualización con colores (nuevo, eliminado, diferente)
  - [x] Generación de script de sincronización
  - [x] Preview y ejecución
- [x] 4.7 Comparación y sincronización de esquemas
  - [x] Lectura de metadatos de ambas BDs
  - [x] Comparación objeto por objeto
  - [x] Diff visual de SQL
  - [x] Generación de script de migración
  - [x] Ordenación por dependencias
- [x] 4.8 Herramientas de migración entre motores
  - [x] Wizard de migración
  - [x] Tabla de mapeo de tipos entre motores
  - [x] Migración de estructura
  - [x] Migración de datos con progreso
  - [x] Log de errores y resumen
- [x] 4.9 Gestión de usuarios y permisos
  - [x] Listar usuarios/roles (PostgreSQL)
  - [x] Listar usuarios (MySQL/MariaDB)
  - [x] Crear/modificar usuarios
  - [x] Asignar/revocar permisos
  - [x] UI de gestión con grid de permisos
- [x] 4.10 Programación de tareas (Scheduler)
  - [x] Modelo `ScheduledTask`
  - [x] Tarea de Backup
  - [x] Tarea de Script SQL
  - [x] Tarea de Sincronización
  - [x] Tarea de Exportación
  - [x] Motor de planificación
  - [x] UI de gestión de tareas
  - [x] Log de ejecuciones
  - [x] Notificaciones
