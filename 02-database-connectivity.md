# 02 - Database Connectivity Agent

[← Volver al índice](agents.md)

---

## Objetivo
Implementar la lógica de conexión a bases de datos PostgreSQL, MySQL, MariaDB y SQLite, con gestión segura de credenciales, connection pooling, soporte SSL/TLS y compatibilidad con bases de datos en la nube.

---

## Tareas Detalladas

### 2.1 Abstracción de proveedores de base de datos
**Descripción:** Crear una capa de abstracción que unifique el acceso a los distintos motores de base de datos, permitiendo que el resto de la aplicación trabaje sin conocer el proveedor concreto.

**Cómo hacerlo:**
- Definir una interfaz `IDatabaseProvider` con métodos comunes:
  - `OpenConnectionAsync()`, `CloseConnectionAsync()`
  - `GetDatabasesAsync()`, `GetSchemasAsync()`, `GetTablesAsync()`, `GetColumnsAsync()`
  - `ExecuteQueryAsync()`, `ExecuteNonQueryAsync()`, `ExecuteScalarAsync()`
  - `GetServerVersionAsync()`, `TestConnectionAsync()`
- Crear implementaciones concretas:
  - `PostgreSqlProvider` (usando Npgsql)
  - `MySqlProvider` (usando MySqlConnector, compatible con MySQL y MariaDB)
  - `SqliteProvider` (usando Microsoft.Data.Sqlite)
- Registrar proveedores en un `DatabaseProviderFactory` que devuelve la implementación correcta según el tipo de conexión.
- Usar inyección de dependencias (DI) con `Microsoft.Extensions.DependencyInjection`.

**Paquetes NuGet:**
- `Npgsql` (PostgreSQL)
- `MySqlConnector` (MySQL/MariaDB)
- `Microsoft.Data.Sqlite` (SQLite)
- `Microsoft.Extensions.DependencyInjection`

### 2.2 Modelo de conexión y persistencia
**Descripción:** Definir el modelo de datos para las conexiones y almacenarlas de forma segura en disco.

**Cómo hacerlo:**
- Crear clase `ConnectionInfo` con propiedades:
  - `Id` (Guid), `Name`, `DatabaseType` (enum: PostgreSQL, MySQL, MariaDB, SQLite)
  - `Host`, `Port`, `Username`, `Password` (cifrado), `DefaultDatabase`
  - `UseSsl`, `SslCertificatePath`, `SshTunnel` (configuración opcional)
  - `Color` (etiqueta visual), `Group` (carpeta/grupo)
- Almacenar conexiones en un archivo JSON cifrado en `%AppData%/DbClient/connections.json`.
- Cifrar contraseñas usando `ProtectedData` (DPAPI) en Windows.
- Implementar `IConnectionRepository` con métodos CRUD para gestionar conexiones.
- Soporte para importar/exportar conexiones (archivo JSON portable con contraseñas cifradas con clave maestra).

### 2.3 Connection Pooling
**Descripción:** Gestionar un pool de conexiones para cada servidor, reutilizando conexiones abiertas y limitando el número máximo.

**Cómo hacerlo:**
- Npgsql y MySqlConnector ya incluyen connection pooling nativo. Configurar:
  - `Min Pool Size`, `Max Pool Size`, `Connection Lifetime`, `Connection Idle Lifetime`.
- Para SQLite, gestionar una única conexión por archivo (modo WAL para concurrencia).
- Crear un `ConnectionManager` que:
  - Mantiene un diccionario de conexiones activas por `ConnectionInfo.Id`.
  - Ofrece `GetConnectionAsync(connectionId)` que devuelve una conexión del pool.
  - Detecta conexiones rotas y las reconecta automáticamente.
  - Emite eventos: `ConnectionOpened`, `ConnectionClosed`, `ConnectionError`.

### 2.4 Soporte SSL/TLS
**Descripción:** Permitir conexiones seguras mediante SSL/TLS a servidores que lo requieran.

**Cómo hacerlo:**
- **PostgreSQL (Npgsql):**
  - Configurar `SslMode` en el connection string: `Require`, `VerifyCA`, `VerifyFull`.
  - Soporte para certificado de cliente: `SSL Certificate`, `SSL Key`, `Root Certificate`.
- **MySQL/MariaDB (MySqlConnector):**
  - Configurar `SslMode`: `Required`, `VerifyCA`, `VerifyFull`.
  - Propiedades: `CACertificateFile`, `SslCert`, `SslKey`.
- **SQLite:** No aplica (local).
- En el diálogo de conexión, añadir pestaña "SSL" con:
  - Checkbox "Usar SSL".
  - Selector de modo SSL.
  - Campos para rutas de certificados (CA, cliente, clave).
  - Botón "Probar conexión SSL".

### 2.5 Autenticación avanzada
**Descripción:** Soportar distintos métodos de autenticación según el motor de base de datos.

**Cómo hacerlo:**
- **PostgreSQL:**
  - Password, MD5, SCRAM-SHA-256 (Npgsql lo maneja automáticamente).
  - Soporte para autenticación con archivo `.pgpass`.
  - Soporte para Kerberos/GSSAPI (si se requiere).
- **MySQL/MariaDB:**
  - `mysql_native_password`, `caching_sha2_password`.
  - MySqlConnector soporta automáticamente los métodos comunes.
- **SQLite:**
  - Soporte para bases de datos cifradas con contraseña (SQLCipher si se desea).
- Implementar un `AuthenticationHandler` por proveedor que gestione el flujo de autenticación.

### 2.6 SSH Tunneling
**Descripción:** Permitir conectarse a bases de datos a través de un túnel SSH.

**Cómo hacerlo:**
- Integrar la librería `SSH.NET` para crear túneles SSH.
- Modelo `SshTunnelConfig`: `SshHost`, `SshPort`, `SshUsername`, `SshPassword` o `SshPrivateKeyPath`, `LocalPort` (auto-asignado).
- Flujo:
  1. Abrir túnel SSH: `SshClient.Connect()` → `ForwardedPortLocal(localPort, dbHost, dbPort)`.
  2. Conectar a la base de datos usando `localhost:localPort`.
  3. Al cerrar la conexión de BD, cerrar el túnel SSH.
- En el diálogo de conexión, añadir pestaña "SSH" con los campos correspondientes.
- Soporte para autenticación por contraseña y por clave privada (PEM, OpenSSH).

**Paquetes NuGet:**
- `SSH.NET`

### 2.7 Soporte para bases de datos en la nube
**Descripción:** Facilitar la conexión a servicios de base de datos en la nube.

**Cómo hacerlo:**
- **Amazon RDS:** Conexión estándar con SSL obligatorio. Plantilla de conexión con endpoint RDS.
- **Google Cloud SQL:** Conexión vía Cloud SQL Auth Proxy o IP pública con SSL.
- **Azure Database:** Conexión estándar con SSL. Plantilla con formato de host `*.database.azure.com`.
- Crear plantillas de conexión predefinidas para cada proveedor en la nube:
  - Rellenan automáticamente el puerto, SSL y formato de host.
- Documentar en la UI las instrucciones para cada proveedor.

### 2.8 Reconexión automática y manejo de errores
**Descripción:** Detectar pérdidas de conexión y reconectar automáticamente cuando sea posible.

**Cómo hacerlo:**
- Implementar un `ConnectionHealthMonitor` que periódicamente (cada N segundos configurable) hace ping a las conexiones activas.
- Al detectar desconexión:
  - Intentar reconexión automática con backoff exponencial (1s, 2s, 4s, 8s… hasta max 60s).
  - Notificar al usuario con barra de estado amarilla: "Reconectando…".
  - Tras éxito: "Reconectado".
  - Tras agotar reintentos: "Conexión perdida. Reconectar manualmente".
- Capturar excepciones específicas de cada proveedor y traducirlas a mensajes amigables.
- Log de todos los eventos de conexión/desconexión.

---

## Checklist de Progreso

- [x] 2.1 Abstracción de proveedores de base de datos
  - [x] Definir interfaz `IDatabaseProvider`
  - [x] Implementar `PostgreSqlProvider` (Npgsql)
  - [x] Implementar `MySqlProvider` (MySqlConnector)
  - [x] Implementar `SqliteProvider` (Microsoft.Data.Sqlite)
  - [x] Crear `DatabaseProviderFactory`
  - [x] Configurar inyección de dependencias
- [x] 2.2 Modelo de conexión y persistencia
  - [x] Crear clase `ConnectionInfo`
  - [x] Implementar almacenamiento JSON cifrado
  - [x] Cifrado de contraseñas con DPAPI
  - [x] `IConnectionRepository` con CRUD
  - [x] Importar/exportar conexiones
- [x] 2.3 Connection Pooling
  - [x] Configurar pooling nativo de Npgsql
  - [x] Configurar pooling nativo de MySqlConnector
  - [x] Gestión de conexión SQLite (WAL)
  - [x] Implementar `ConnectionManager`
  - [x] Eventos de conexión (abierta, cerrada, error)
- [x] 2.4 Soporte SSL/TLS
  - [x] SSL para PostgreSQL (Npgsql)
  - [x] SSL para MySQL/MariaDB (MySqlConnector)
  - [x] UI de configuración SSL en diálogo de conexión
  - [x] Prueba de conexión SSL
- [x] 2.5 Autenticación avanzada
  - [x] Métodos de auth PostgreSQL
  - [x] Métodos de auth MySQL/MariaDB
  - [x] Soporte SQLite cifrado (opcional SQLCipher)
- [x] 2.6 SSH Tunneling
  - [x] Integrar SSH.NET
  - [x] Modelo `SshTunnelConfig`
  - [x] Flujo de apertura/cierre de túnel
  - [x] UI de configuración SSH en diálogo de conexión
  - [x] Autenticación por contraseña y clave privada
- [x] 2.7 Soporte para bases de datos en la nube
  - [x] Plantilla de conexión Amazon RDS
  - [x] Plantilla de conexión Google Cloud SQL
  - [x] Plantilla de conexión Azure Database
  - [x] Documentación en UI por proveedor
- [x] 2.8 Reconexión automática y manejo de errores
  - [x] `ConnectionHealthMonitor` con ping periódico
  - [x] Reconexión con backoff exponencial
  - [x] Notificaciones al usuario en barra de estado
  - [x] Traducción de excepciones a mensajes amigables
  - [x] Logging de eventos de conexión
