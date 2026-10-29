# SGCAE - Sistema de Gestión y Control de Acceso de Empleados

Backend del taller 2 de Programación Distribuida. El sistema permite administrar
empleados y credenciales, registrar entradas y salidas y consultar el historial
de accesos.

## Contenido

- [Inicio rápido](#inicio-rápido)
- [Requisitos y cobertura](#requisitos-y-cobertura)
- [Organización de la solución](#organización-de-la-solución)
- [Pruebas](#ejecutar-las-pruebas)
- [Configuración](#configuración) y [base de datos](#crear-o-actualizar-la-base-de-datos)
- [Empleados](#gestión-de-empleados-rf-01), [puntos](#puntos-de-acceso-rf-06) y [credenciales](#gestión-de-credenciales-rf-02)
- [Entradas y salidas](#registro-de-entradas-y-salidas-rf-03) e [historial](#consulta-del-historial-rf-04)
- [Modelo de dominio](#modelo-de-dominio)
- [Demostración reproducible](#demostración-reproducible)
- [Evidencias de validación](#evidencias-de-validación)
- [Solución de problemas](#solución-de-problemas)

## Requisitos y cobertura

La solución cuenta con la estructura de Clean Architecture, una API ejecutable
y un dominio con entidades, objetos de valor y reglas de acceso probadas.
La persistencia usa Entity Framework Core 10.0.14 y SQL Server, con mapeos,
restricciones, migración inicial y pruebas de integración. La gestión de
empleados ya dispone de comandos, consultas y endpoints REST (RF-01).
Los puntos de acceso permiten registro y consulta con su estado (RF-06).
La gestión de credenciales incluye asignación, consulta, bloqueo y revocación
(RF-02). El registro de entradas y salidas evalúa el acceso y almacena
autorizaciones y rechazos (RF-03). El historial permite filtros combinables
y paginación con orden estable (RF-04).
Las reglas y decisiones del modelo se describen en la sección Modelo de dominio de este README.

| Requisito del taller | Implementación | Pruebas principales en tests/SGCAE.Api.Tests |
| --- | --- | --- |
| RF-01: empleados | Registro, consulta, actualización y desactivación; identificación única. | EmployeeEndpointsTests.cs |
| RF-02: credenciales | Asignación, consulta, bloqueo y revocación; código único. | CredentialEndpointsTests.cs |
| RF-03: movimientos | Validación de entrada/salida y persistencia de autorizaciones y rechazos. | AccessRecordEndpointsTests.cs |
| RF-04: historial | Filtros combinables, paginación y orden estable. | AccessHistoryEndpointsTests.cs |
| RF-06: puntos de acceso | Registro y consulta con estado habilitado/deshabilitado. | AccessPointEndpointsTests.cs |

El enunciado enumera RF-01, RF-02, RF-03, RF-04 y RF-06; no define RF-05.
`SystemScenarioTests.cs` verifica el recorrido entre estos requisitos.
La entrega es una API REST: no incluye interfaz gráfica, autenticación de usuarios
ni control de ocupación. Las decisiones adicionales se explican en el modelo de dominio.

## Requisitos de desarrollo

- SDK .NET 10.0.401 o un parche posterior de la misma banda 10.0.4xx,
  según `global.json`.
- SQL Server. En Windows se puede usar SQL Server Express LocalDB con la
  instancia `MSSQLLocalDB`; para otro servidor, configurar su conexión.
- Acceso a NuGet para restaurar los paquetes y la herramienta local `dotnet-ef`.
- Git y PowerShell para los comandos de esta guía. LocalDB requiere Windows;
  en otro sistema debe usarse una instancia accesible de SQL Server.

## Inicio rápido

En Windows con los requisitos anteriores instalados, ejecutar en PowerShell:

```powershell
git clone https://github.com/JCobz0714/Seguimiento-2-Programacion-Distribuida.git
Set-Location Seguimiento-2-Programacion-Distribuida
dotnet --version
dotnet restore SGCAE.slnx
dotnet tool restore
dotnet build SGCAE.slnx --configuration Release --no-restore --disable-build-servers --maxcpucount:1
SqlLocalDB start MSSQLLocalDB
dotnet ef database update --configuration Release --no-build --project src/SGCAE.Infrastructure --startup-project src/SGCAE.Api -- --environment Development
dotnet run --project src/SGCAE.Api --configuration Release --no-build --launch-profile http
```

Si ya se dispone del repositorio, comenzar desde su raíz y omitir la clonación.
Si se usa otro SQL Server, configurar la conexión antes de aplicar la migración
y omitir `SqlLocalDB start`. La migración crea `SGCAE_Taller2` con el esquema,
sin datos iniciales. Para detener la API, pulsar Ctrl+C en su terminal.

Conservar esa terminal abierta. En otra, comprobar `/health` y `/health/db`
y ejecutar la [demostración](#demostración-reproducible). La raíz `/` no tiene
una página de inicio ni Swagger; usar las rutas documentadas o el archivo
[SGCAE.Api.http](src/SGCAE.Api/SGCAE.Api.http) en un cliente compatible.

## Organización de la solución

```text
SGCAE.slnx
src/
  SGCAE.Domain/          Entidades, objetos de valor y reglas del negocio.
  SGCAE.Application/     Casos de uso, comandos, consultas y contratos.
  SGCAE.Infrastructure/  Persistencia e implementaciones de los contratos.
  SGCAE.Api/             Endpoints HTTP y configuración de la aplicación.
tests/
  SGCAE.Domain.Tests/    Pruebas de las reglas del negocio.
  SGCAE.Infrastructure.Tests/ Pruebas de persistencia en SQL Server real.
  SGCAE.Api.Tests/       Pruebas HTTP de los casos de uso con SQL Server real.
  Shared/               Preparación compartida de bases temporales de pruebas.
```

Las referencias entre proyectos son:

```text
Application    -> Domain
Infrastructure -> Application
Api            -> Application, Infrastructure
```

Domain no depende de los demás proyectos. Api referencia Infrastructure para
configurar las implementaciones mediante inyección de dependencias; los casos
de uso residen en Application con separación entre comandos y consultas.

## Compilar

Desde la raíz del repositorio:

```powershell
dotnet restore SGCAE.slnx
dotnet tool restore
dotnet build SGCAE.slnx --configuration Release --no-restore --disable-build-servers --maxcpucount:1
```

## Ejecutar las pruebas

```powershell
dotnet test SGCAE.slnx --configuration Release --logger trx --results-directory artifacts/validation
```

El comando anterior ejecuta todas las pruebas y requiere SQL Server LocalDB
en Windows o la variable de entorno `SGCAE_TEST_SQLSERVER` con la conexión
a otro servidor SQL Server. La cuenta de pruebas debe poder crear y eliminar
bases de datos. Cada conjunto de integración crea su propia base
`SGCAE_Tests_<Guid>`, aplica las migraciones y elimina únicamente esa base
al finalizar. Las pruebas de persistencia revierten sus transacciones; las
pruebas HTTP limpian los datos de su base temporal antes de cada caso.
No se usa la base `SGCAE_Taller2` para las pruebas.
Los archivos TRX quedan en `artifacts/validation`, excluido de Git. Para otro
servidor, establecer `SGCAE_TEST_SQLSERVER` en la terminal de pruebas; esta
variable es independiente de `ConnectionStrings__Sgcae`, usada por la API:

```powershell
$env:SGCAE_TEST_SQLSERVER = 'Server=SERVIDOR;Integrated Security=True;Encrypt=True;TrustServerCertificate=False'
```

La instancia debe existir y la cuenta tener permisos de creación y eliminación
de bases temporales; el fixture selecciona su propio nombre de base.

Para ejecutar solo las pruebas de dominio, sin SQL Server:

```powershell
dotnet test tests/SGCAE.Domain.Tests --configuration Release
```

Las pruebas cubren transiciones de estados, actualizaciones atómicas, validaciones de valores, todos los motivos de rechazo para entrada y salida, integridad de las referencias, fechas UTC y conservación del resultado histórico.

Las pruebas de persistencia comprueban lectura y escritura de las cuatro
entidades, objetos de valor y estados, todos los motivos de rechazo,
unicidad de códigos e identificaciones, comparación sensible a mayúsculas,
claves foráneas, restricciones de estados y coherencia entre modelo y migración.

Las pruebas HTTP de empleados recorren controlador, casos de uso, dominio y
SQL Server: registro, consulta, actualización, desactivación repetida,
conservación del historial, validaciones, duplicados (también concurrentes),
paginación y lecturas sin seguimiento. Las de puntos de acceso verifican
registro habilitado/deshabilitado, estado predeterminado, consultas, filtro,
conteo filtrado, paginación con nombres repetidos y errores de entrada.
Las de credenciales verifican asignación a empleados activos, unicidad global
de códigos, operaciones concurrentes, transiciones definitivas, conservación
del historial, filtros, paginación y validaciones.
Las de entradas/salidas verifican autorización y rechazos para ambos movimientos,
referencias desconocidas, prioridades de rechazo, fecha UTC del servidor,
validaciones y rollback ante un fallo posterior al INSERT. EmployeeNotFound
se cubre en dominio: la clave foránea impide crear credenciales huérfanas
en una base válida mediante los endpoints.
Las del historial comprueban las 32 combinaciones de filtros, rango temporal
con desplazamientos horarios, orden con fechas empatadas, páginas sin
solapamientos sobre datos fijos, conteo filtrado, consultas vacías, validaciones
y conservación de la evidencia tras cambios en el empleado.
Para ejecutar las pruebas HTTP por separado:

```powershell
dotnet test tests/SGCAE.Api.Tests --configuration Release
```

### Escenarios completos

`SystemScenarioTests` usa una base SQL Server temporal migrada y crea los datos
del negocio mediante HTTP. Comprueba:

- Registro y actualización de empleados, creación de puntos, asignación de
  credenciales, entradas/salidas, bloqueo, revocación, desactivación y consulta
  del historial con todos los filtros y paginación.
- Conservación de autorizaciones y rechazos, incluidos código y punto
  desconocidos, después de cambiar los estados del empleado y la credencial.
- Conflictos por duplicados y operaciones inválidas sin datos parciales.
- Accesos simultáneos con una revocación: se conserva cada decisión registrada
  y los accesos posteriores a la revocación son rechazados.
- Lectura de datos confirmados desde una segunda instancia de la API.
- Respuestas 400, 404, 409 y 500 ante un Accept de HTML o texto.

La revisión integral detectó que el escritor de ProblemDetails no podía
atender ciertos valores de Accept y el manejador dejaba escapar la excepción
original. Ahora se intenta el escritor configurado y, si no es compatible,
se devuelve `application/problem+json` como respaldo, manteniendo el código
HTTP y ocultando los detalles internos de errores inesperados.

Para ejecutar solo estos escenarios:

```powershell
dotnet test tests/SGCAE.Api.Tests --configuration Release --filter FullyQualifiedName~SystemScenarioTests
```

Las pruebas de fallos de persistencia y rollback de los bloques anteriores
siguen formando parte de la suite completa.

## Ejecutar la API

```powershell
dotnet run --project src/SGCAE.Api --launch-profile http
```

La API escucha en `http://localhost:5080`. Para comprobar el arranque, abrir
`http://localhost:5080/health`, usar el archivo `src/SGCAE.Api/SGCAE.Api.http`
o ejecutar en otra terminal:

```powershell
Invoke-WebRequest http://localhost:5080/health
```

La respuesta esperada es HTTP 200 con el texto `Healthy`. Esta comprobación
confirma que la aplicación responde. Para comprobar además la conexión a SQL Server:

```powershell
Invoke-WebRequest http://localhost:5080/health/db
```

`/health/db` devuelve HTTP 200 (`Healthy`) si puede conectar a la base configurada
y HTTP 503 (`Unhealthy`) si no puede. No aplica migraciones ni comprueba por sí
solo que el esquema esté actualizado. Aplicar la migración antes de usar la base.

## Configuración

`appsettings.json` contiene únicamente configuración general.
`appsettings.Development.json` configura la base `SGCAE_Taller2` en
`(localdb)\MSSQLLocalDB` con autenticación integrada de Windows, sin contraseña.
Ese perfil permite confiar en el certificado local de desarrollo.

Para usar otro servidor, configurar `ConnectionStrings:Sgcae` mediante secretos
de usuario de .NET (solo Development) o la variable de entorno
`ConnectionStrings__Sgcae`. No publicar contraseñas en archivos del repositorio.
Fuera de Development se exige una conexión explícita; usar un certificado
válido y `TrustServerCertificate=False` en un servidor desplegado.

Ejemplo con autenticación integrada desde PowerShell (ajustar el servidor):

```powershell
$env:ConnectionStrings__Sgcae = 'Server=SERVIDOR;Database=SGCAE_Taller2;Integrated Security=True;Encrypt=True;TrustServerCertificate=False'
```

## Crear o actualizar la base de datos

Para la instancia de desarrollo LocalDB, ejecutar desde la raíz:

```powershell
SqlLocalDB start MSSQLLocalDB
dotnet tool restore
dotnet ef database update --project src/SGCAE.Infrastructure --startup-project src/SGCAE.Api -- --environment Development
```

La herramienta usa la conexión de la API y crea la base si no existe. La
migración `InitialCreate` está versionada junto con su snapshot en Infrastructure.
La aplicación no aplica migraciones automáticamente durante el arranque.
La cuenta que aplica la migración necesita permisos para crear la base, o una
base previamente creada y permisos para modificar su esquema. No hace falta
crear manualmente las tablas ni ejecutar `migrations add` al clonar.

Para comprobar la migración aplicada:

```powershell
dotnet ef migrations list --project src/SGCAE.Infrastructure --startup-project src/SGCAE.Api -- --environment Development
dotnet ef migrations has-pending-model-changes --project src/SGCAE.Infrastructure --startup-project src/SGCAE.Api -- --environment Development
```

Se espera `20261028212642_InitialCreate` sin indicación de pendiente y ninguna
diferencia entre modelo y snapshot. Ejecutar `database update` de nuevo debe
indicar que la base ya está actualizada. Desde un cliente SQL conectado a
`SGCAE_Taller2`, estas consultas de lectura permiten inspeccionar el esquema:

```sql
SELECT MigrationId, ProductVersion FROM dbo.__EFMigrationsHistory;
SELECT name FROM sys.tables WHERE schema_id = SCHEMA_ID('dbo') ORDER BY name;
SELECT OBJECT_NAME(object_id) AS TableName, name AS UniqueIndex
FROM sys.indexes
WHERE is_unique = 1 AND is_primary_key = 0
  AND OBJECT_NAME(object_id) IN ('Employees', 'Credentials');
```

Además de `__EFMigrationsHistory`, deben aparecer `Employees`, `Credentials`,
`AccessPoints` y `AccessRecords`; los índices únicos son
`IX_Employees_Identification` e `IX_Credentials_Code`.

Para generar un script SQL revisable, sin aplicarlo:

```powershell
dotnet ef migrations script --idempotent --project src/SGCAE.Infrastructure --startup-project src/SGCAE.Api -- --environment Development
```

## Modelo de persistencia

- `Employees`: identificación única y datos laborales.
- `Credentials`: código único y clave foránea obligatoria hacia el empleado.
- `AccessPoints`: nombre y disponibilidad del punto físico.
- `AccessRecords`: fecha `datetimeoffset`, movimiento, resultado y motivo de rechazo.

Los objetos de valor se convierten a columnas de texto conservando sus límites.
Los índices únicos de identificación y código utilizan `Latin1_General_100_BIN2`
para respetar la distinción de mayúsculas del dominio. Las claves Guid las
genera el dominio. Las entidades mantienen setters privados y constructores
privados para permitir su reconstrucción por EF Core sin exponer cambios libres
al resto de la aplicación.

Las claves foráneas usan `NO ACTION`: borrar una entidad referenciada falla
en lugar de eliminar o desvincular su historial. Los intentos desconocidos
conservan el código y el punto solicitados en columnas independientes; sus
referencias a entidades existentes pueden ser nulas. Las restricciones CHECK
validan estados, tipos de movimiento y coherencia entre resultado y motivo.
Hay índices por fecha, empleado, punto y resultado, junto al identificador del
registro, para apoyar las consultas y paginación del historial.

La base garantiza unicidad e integridad referencial. Las decisiones de acceso
y las transiciones siguen siendo responsabilidad del dominio y de los
casos de uso. La persistencia no sustituye las reglas del negocio.

## Gestión de empleados (RF-01)

Decisiones de la API: la identificación se asigna al registrar
y no se modifica. Los empleados nacen activos; desactivar es una operación
repetible que conserva sus datos. Actualizar un empleado inactivo no lo reactiva.
El listado incluye activos e inactivos y usa paginación, con 20 elementos por
defecto y un máximo de 100 por página, ordenados por identificación e Id.
Las solicitudes solo aceptan los campos definidos por cada operación.
Los datos inválidos producen HTTP 400, los empleados inexistentes HTTP 404 y
las identificaciones duplicadas HTTP 409. El índice único de SQL Server resuelve
también el conflicto entre registros simultáneos con la misma identificación.

### Comandos, consultas y persistencia

Application contiene `CreateEmployeeCommand`, `UpdateEmployeeCommand` y
`DeactivateEmployeeCommand`, cada uno con su manejador. Las consultas
`GetEmployeeQuery` y `ListEmployeesQuery` tienen sus propios manejadores y
devuelven DTO. El controlador recibe HTTP y delega en estos casos de uso.

Los comandos usan `IEmployeeRepository` para cargar entidades, ejecutar sus
reglas y guardar los cambios. Las consultas usan `IEmployeeQueries`;
Infrastructure implementa ambos contratos con EF Core. Las lecturas emplean
`AsNoTracking`, y el listado pagina en SQL. Esta separación implementa CQRS
sin exigir bases independientes ni una biblioteca de mediación.

### Endpoints

| Método | Ruta | Resultado |
| --- | --- | --- |
| POST | `/api/employees` | 201, empleado creado y cabecera Location. |
| GET | `/api/employees/{id}` | 200, empleado por Guid. |
| GET | `/api/employees?page=1&pageSize=20` | 200, items, page, pageSize y totalCount. |
| PUT | `/api/employees/{id}` | 200, empleado actualizado. |
| PATCH | `/api/employees/{id}/deactivate` | 204, sin cuerpo. |

Cuerpo de registro:

```json
{
  "identification": "00123",
  "firstName": "Ana",
  "lastName": "Pérez",
  "email": "ana@example.com",
  "jobTitle": "Operadora",
  "organizationalArea": "Bodega"
}
```

PUT exige los cinco campos de datos personales/laborales del ejemplo y omite
`identification`. No admite modificar `id` ni `status`. Los campos adicionales
se rechazan con 400 para evitar solicitudes aparentemente exitosas que ignoren
cambios. PATCH no necesita cuerpo. Las respuestas incluyen `id` y `status`
como `Active` o `Inactive`. Los errores usan `application/problem+json`;
los fallos inesperados devuelven 500 sin detalles internos de SQL Server.

El archivo `src/SGCAE.Api/SGCAE.Api.http` contiene el recorrido de ejemplo.
Ejecutar el POST y copiar el Guid devuelto en la variable `employeeId` antes
de consultar, actualizar o desactivar. Repetir el POST con la misma
identificación produce 409. Una página posterior al último resultado devuelve
una lista vacía; los números de página deben ser positivos.

## Puntos de acceso (RF-06)

Los puntos admiten registro y consulta con su estado. Se crea cada punto
habilitado por defecto y admite estado inicial deshabilitado. El nombre se
recorta, es obligatorio y admite hasta 100 caracteres. No se exige unicidad
del nombre: cada punto se identifica por su Guid.

El listado incluye ambos estados por defecto y permite filtrar por
`isEnabled=true` o `isEnabled=false`. Usa páginas de 20 elementos por defecto,
un máximo de 100 y orden estable por nombre e Id. Estas son decisiones de
la API para consultar los puntos y reconocer los habilitados.

| Método | Ruta | Resultado |
| --- | --- | --- |
| POST | `/api/access-points` | 201, punto creado y cabecera Location. |
| GET | `/api/access-points/{id}` | 200, punto por Guid con su estado. |
| GET | `/api/access-points?page=1&pageSize=20&isEnabled=true` | 200, items, page, pageSize y totalCount filtrado. |

Cuerpo de registro:

```json
{
  "name": "Entrada principal",
  "isEnabled": true
}
```

`isEnabled` es opcional; si se envía, debe ser un booleano. Las respuestas
contienen `id`, `name` e `isEnabled`. Un punto inexistente devuelve 404;
los campos desconocidos, datos inválidos o parámetros fuera de rango devuelven
400 con ProblemDetails. Una consulta sin resultados devuelve una lista vacía.
Los ejemplos están en `src/SGCAE.Api/SGCAE.Api.http`; copiar el Guid devuelto
por POST en la variable `accessPointId` antes de consultar ese punto.

Application separa `CreateAccessPointCommand` de `GetAccessPointQuery` y
`ListAccessPointsQuery`, con manejadores y contratos de persistencia propios.
Infrastructure guarda mediante EF Core y proyecta las lecturas a DTO sin
seguimiento. El filtro, el conteo y la paginación se ejecutan en SQL Server.
Se utiliza la tabla AccessPoints de la migración inicial.

## Gestión de credenciales (RF-02)

La asignación exige un empleado existente y activo. Un empleado puede tener
varias credenciales. Cada código es único, distingue mayúsculas, elimina
espacios exteriores y admite hasta 100 caracteres; no puede reutilizarse
después de revocar una credencial. El código y el propietario son inmutables.
El estado inicial es Active. Blocked y Revoked representan bloqueo y revocación.
Bloquear y revocar son repetibles; la revocación es definitiva.

Las consultas incluyen todos los estados y admiten filtros por empleado y
estado, con páginas de 20 elementos por defecto y máximo 100, ordenadas por
código e Id. Los errores de formato y reglas del dominio devuelven 400,
las referencias inexistentes 404 y los códigos duplicados 409.

| Método | Ruta | Resultado |
| --- | --- | --- |
| POST | `/api/credentials` | 201, credencial asignada y cabecera Location. |
| GET | `/api/credentials/{id}` | 200, credencial por Guid. |
| GET | `/api/credentials?employeeId={guid}&status=Active&page=1&pageSize=20` | 200, página filtrada. |
| PATCH | `/api/credentials/{id}/block` | 200, credencial bloqueada. |
| PATCH | `/api/credentials/{id}/revoke` | 200, credencial revocada. |

POST recibe `employeeId` (Guid de un empleado registrado) y `code` (texto).
Por ejemplo, reemplazar el identificador con el obtenido al crear un empleado:

```json
{
  "employeeId": "00000000-0000-0000-0000-000000000001",
  "code": "C-001"
}
```

La respuesta contiene `id`, `employeeId`, `code` y `status`.
Los campos adicionales en POST se rechazan. Los PATCH no necesitan cuerpo.
Los filtros son opcionales; `status` acepta Active, Blocked o Revoked.
El listado devuelve `items`, `page`, `pageSize` y `totalCount` después
de aplicar los filtros. Un filtro por empleado sin credenciales devuelve
una página vacía. Los ejemplos completos están en `SGCAE.Api.http`.

Application contiene los comandos AssignCredential, BlockCredential y
RevokeCredential, las consultas GetCredential y ListCredentials, sus
manejadores y contratos. Las reglas permanecen en Credential y CredentialCode.
Infrastructure ejecuta cada comando en una transacción. La lectura del
empleado durante la asignación y de la credencial durante los cambios de estado
utiliza bloqueos de actualización hasta confirmar la operación. Así, un
bloqueo simultáneo no puede sobrescribir una revocación. Los reintentos de
errores transitorios vuelven a leer el estado dentro de una nueva transacción.
El índice único resuelve las asignaciones simultáneas con el mismo código.
Las consultas usan AsNoTracking; el filtrado y la paginación se hacen en SQL.
Se utiliza la tabla Credentials de la migración inicial.

## Registro de entradas y salidas (RF-03)

La API recibe código de credencial, identificador del punto y movimiento Entry
o Exit. La fecha se obtiene del servidor en UTC. Cada solicitud válida genera
un registro, autorizado o rechazado, conservando los valores solicitados aunque
la credencial o el punto no existan. Los datos mal formados devuelven 400 sin
registrarse. Los registros guardados devuelven 201 en ambos resultados; el campo
result indica Authorized o Rejected y rejectionReason explica el rechazo.
No se devuelve una autorización si falla el guardado.

La lectura de estados, evaluación y persistencia se realizan en una transacción
serializable: los estados leídos se mantienen hasta guardar. Los reintentos
transitorios vuelven a consultar y evaluar. Solicitudes HTTP repetidas son
intentos independientes; no se implementa deduplicación entre solicitudes.

Endpoint: `POST /api/access-records`. Reemplazar el identificador del punto
con uno registrado y el código con el de una credencial:

```json
{
  "credentialCode": "C-001",
  "accessPointId": "00000000-0000-0000-0000-000000000001",
  "movement": "Entry"
}
```

Usar `Exit` para una salida. La respuesta incluye `id`,
`requestedCredentialCode`, `requestedAccessPointId`, `credentialId`,
`employeeId`, `accessPointId`, `movement`, `occurredAt`, `result` y
`rejectionReason`. Las referencias a entidades desconocidas son null.
Los campos adicionales enviados por el cliente, como fecha, resultado o empleado,
se rechazan: la aplicación los resuelve. No se controla ocupación y se permiten
movimientos consecutivos del mismo tipo.

`RegisterAccessCommand` y su manejador coordinan el caso de uso en Application.
`AccessPolicy` decide en Domain y `IAccessRecordRepository` permite consultar
las entidades y guardar el registro. Infrastructure implementa el contrato
con EF Core, transacciones y el esquema existente. El controlador responde
solo después de confirmar la transacción; un fallo de persistencia devuelve
500 con ProblemDetails sin exponer detalles internos.

Los ejemplos HTTP siguen el ciclo de vida: primero entradas y salidas autorizadas,
después bloqueo, revocación y desactivación con sus rechazos. Para autorizar,
se requiere un empleado activo, una credencial vigente y un punto habilitado.

## Consulta del historial (RF-04)

El historial permite combinar empleado, punto solicitado, resultado y rango
de fechas mediante AND. El filtro por punto utiliza el identificador solicitado,
por lo que también encuentra rechazos por puntos desconocidos. El filtro por
empleado solo encuentra registros con ese empleado identificado.

El rango temporal incluye `from` y excluye `to`; se compara por instante UTC.
Se recomienda enviar fechas ISO 8601 con Z o desplazamiento explícito.
El orden es fecha descendente y, en empates, Id descendente según SQL Server.
Las páginas empiezan en 1, con tamaño 20 por defecto y máximo 100.
El orden es determinista para los mismos datos; nuevos registros entre consultas
pueden desplazar las páginas. Cada respuesta mantiene consistentes su total y
sus elementos mediante una transacción de lectura serializable.

Endpoint: `GET /api/access-records`. Todos los parámetros son opcionales:

| Parámetro | Uso |
| --- | --- |
| `employeeId` | Guid del empleado identificado en el registro. |
| `accessPointId` | Guid del punto solicitado, incluso si no existe. |
| `result` | Authorized o Rejected. |
| `from` | Fecha/hora inicial incluida, por ejemplo 2026-10-29T00:00:00Z. |
| `to` | Fecha/hora final excluida, por ejemplo 2026-10-30T00:00:00Z. |
| `page` | Página desde 1; por defecto 1. |
| `pageSize` | Elementos por página, de 1 a 100; por defecto 20. |

Ejemplo de filtros combinados:

```http
GET /api/access-records?result=Rejected&from=2026-10-29T00:00:00Z&to=2026-10-30T00:00:00Z&page=1&pageSize=20
```

Para consultar un día de Colombia, usar desde las 00:00:00-05:00 hasta las
00:00:00-05:00 del día siguiente. En una URL, codificar el signo + de un
desplazamiento positivo como %2B.

La respuesta 200 contiene `items` (los registros con sus valores solicitados,
referencias, fecha, movimiento, resultado y motivo), `page`, `pageSize` y
`totalCount`. El total se calcula después de combinar los filtros y antes
de paginar. Sin coincidencias o con una página posterior a la última se devuelve
una lista vacía, conservando el total filtrado. Un Guid de filtro sin registros
no produce 404.

Páginas inválidas, Guid vacíos, resultados no definidos, fechas mal formadas
o un rango donde `from >= to` devuelven 400 con ProblemDetails.
`ListAccessRecordsQuery` y su manejador validan en Application;
`IAccessRecordQueries` separa la lectura del registro de movimientos.
Infrastructure aplica los filtros, el orden y la paginación en SQL Server con
AsNoTracking. Se usa el esquema existente. El límite de cada respuesta evita
cargar todo el historial en memoria.

## Modelo de dominio

### Requisitos y alcance

El dominio representa RF-01 (empleados), RF-02 (credenciales), RF-03 (movimientos)
y RF-06 (puntos de acceso). Los registros contienen los datos necesarios para
el historial de RF-04. Las reglas se prueban en memoria y su persistencia
se verifica en SQL Server. Los casos de uso y endpoints de empleados, puntos,
credenciales, registro de accesos y consulta del historial están implementados.

### Entidades y límites

- **Employee**: identidad propia, identificación laboral, nombres, apellidos,
  correo, cargo, área y estado activo/inactivo. Se desactiva sin borrar historial.
- **Credential**: identidad propia, código, referencia al empleado propietario y
  estado vigente, bloqueado o revocado. El propietario y el código no cambian.
- **AccessPoint**: identidad propia, nombre y disponibilidad.
- **AccessRecord**: evidencia inmutable de un intento, fecha UTC, entrada/salida,
  resultado, motivo de rechazo y referencias a las entidades conocidas.

Cada entidad constituye un agregado pequeño con identidad Guid y protege su
estado mediante operaciones de negocio. Las relaciones entre agregados usan
identificadores. AccessPolicy coordina la decisión que depende de varios
agregados, sin consultas a bases de datos ni dependencias de ASP.NET o EF Core.
La aplicación resuelve las entidades y proporciona la fecha del servidor.

### Objetos de valor

EmployeeIdentification, CredentialCode y EmailAddress son objetos inmutables,
comparados por valor. Se eliminan espacios exteriores y se rechazan valores
vacíos. La identificación conserva ceros iniciales. El correo acepta una sola
dirección sin nombre de presentación; valida sintaxis, no existencia del buzón.

Límites adoptados para este proyecto: identificación 30 caracteres, código 100,
correo 254; nombres, apellidos, cargo, área y nombre del punto 100 cada uno.
Los códigos e identificaciones distinguen mayúsculas y minúsculas. El correo
conserva su escritura y no se considera único.

La unicidad global de identificación y código es un requisito del enunciado,
pero no puede comprobarse dentro de una entidad aislada. Ya se garantiza con
índices únicos en SQL Server. El repositorio de empleados traduce el conflicto
de su índice de identificación a un error de aplicación que la API responde
con 409, incluso ante solicitudes concurrentes. Los demás errores de SQL
no se interpretan como duplicados.

### Decisiones para los aspectos no definidos por el enunciado

1. Un empleado puede tener varias credenciales con códigos diferentes.
2. Solo se asignan credenciales a empleados activos.
3. El bloqueo puede repetirse. Una credencial activa o bloqueada puede revocarse;
   revocar de nuevo es seguro. La revocación es definitiva y no permite bloquear
   después. No se incluye una operación de desbloqueo.
4. Actualizar los datos de un empleado no cambia su identificación ni reactiva
   su vínculo laboral. Una actualización inválida no modifica ningún campo.
5. Un punto se crea habilitado por defecto, pero admite estado inicial
   deshabilitado. El dominio permite habilitarlo y deshabilitarlo; la API
   implementada expone registro y consulta, no un cambio posterior del estado.
6. Entradas y salidas aplican la misma validación. No se implementa control de
   ocupación ni se rechazan movimientos consecutivos del mismo tipo.
7. No se implementan permisos por área: el enunciado no define una matriz
   empleado/punto. Cualquier punto habilitado acepta una credencial vigente
   perteneciente a un empleado activo.
8. Cada evaluación genera un registro independiente. La deduplicación de
   solicitudes de transporte o reintentos es una decisión futura de aplicación.

### Decisión de acceso y trazabilidad

Si coinciden varios impedimentos, se registra el primero en este orden:

1. Credencial desconocida.
2. Empleado no encontrado para la credencial.
3. Empleado inactivo.
4. Credencial bloqueada.
5. Credencial revocada.
6. Punto de acceso desconocido.
7. Punto de acceso deshabilitado.

Sin impedimentos, el resultado es autorizado y no tiene motivo de rechazo.
Siempre se preservan el código y el identificador del punto solicitados.
Las referencias a entidades desconocidas son nulas, lo que permite representar
intentos fallidos sin inventar empleados, credenciales ni puntos existentes.

Los datos mal formados (código vacío, Guid vacío, movimiento no definido o fecha
ausente) producen una excepción de dominio. No son rechazos de una solicitud
válida. También se rechazan como errores los objetos resueltos que no
corresponden al código, punto o propietario solicitado; esto evita autorizar
usando datos de otra persona. La aplicación distingue estos errores
de los rechazos del negocio.

AccessPolicy devuelve el registro tanto al autorizar como al rechazar.
La capa de aplicación guarda ambos resultados antes de responder. Las pruebas
HTTP verifican su persistencia y que un fallo después del INSERT revierta la
transacción, sin devolver una decisión de acceso como si se hubiera guardado.

## Demostración reproducible

Con la API y la base preparadas, pegar este bloque completo en otra terminal
PowerShell. Usa identificaciones y códigos nuevos en cada ejecución, toma los
Guid de las respuestas y verifica los códigos HTTP. Crea un empleado, dos puntos,
una credencial y ocho intentos en la base configurada; esos datos quedan guardados
para inspeccionarlos. No elimina ni modifica registros previos.

El recorrido conserva dos autorizaciones y seis rechazos. Al final consulta con
los cinco filtros combinados y divide los tres rechazos coincidentes en páginas
de dos y un elemento. Si una comprobación falla, el bloque se detiene con un error.

```powershell
# SGCAE-DEMO-BEGIN
$ErrorActionPreference = 'Stop'
$baseUrl = 'http://localhost:5080'
$suffix = [Guid]::NewGuid().ToString('N').Substring(0, 12)
$from = [DateTimeOffset]::UtcNow.ToString('o')

function Invoke-Sgcae($method, $path, $body = $null, $expected = 200) {
    $request = @{
        Uri = "$baseUrl$path"; Method = $method; UseBasicParsing = $true
        Headers = @{ Accept = 'application/json' }
    }
    if ($null -ne $body) {
        $request.ContentType = 'application/json; charset=utf-8'
        $request.Body = [Text.Encoding]::UTF8.GetBytes(($body | ConvertTo-Json -Depth 10))
    }
    $response = Invoke-WebRequest @request
    if ([int]$response.StatusCode -ne $expected) { throw "HTTP inesperado en $path" }
    if ($response.Content) { $response.Content | ConvertFrom-Json }
}

foreach ($path in @('/health', '/health/db')) {
    $health = Invoke-WebRequest -UseBasicParsing "$baseUrl$path"
    if ($health.StatusCode -ne 200 -or $health.Content.Trim() -ne 'Healthy') {
        throw "Health check fallido: $path"
    }
}
$employee = Invoke-Sgcae POST '/api/employees' @{
    identification = "DEMO-$suffix"; firstName = 'Ana'; lastName = 'Perez'
    email = 'ana@example.com'; jobTitle = 'Operadora'; organizationalArea = 'Bodega'
} 201
$employee = Invoke-Sgcae PUT "/api/employees/$($employee.id)" @{
    firstName = 'Ana'; lastName = 'Perez'; email = 'ana@example.com'
    jobTitle = 'Supervisora'; organizationalArea = 'Logistica'
}
$point = Invoke-Sgcae POST '/api/access-points' @{ name = "Principal $suffix"; isEnabled = $true } 201
$disabled = Invoke-Sgcae POST '/api/access-points' @{ name = "Cerrado $suffix"; isEnabled = $false } 201
$credential = Invoke-Sgcae POST '/api/credentials' @{ employeeId = $employee.id; code = "C-$suffix" } 201

function Register-DemoAccess($code, $pointId, $movement, $result, $reason = $null) {
    $record = Invoke-Sgcae POST '/api/access-records' @{
        credentialCode = $code; accessPointId = $pointId; movement = $movement
    } 201
    if ($record.result -ne $result -or $record.rejectionReason -ne $reason) {
        throw "Decision inesperada: $($record | ConvertTo-Json -Compress)"
    }
    $record
}

$records = @(
    Register-DemoAccess $credential.code $point.id Entry Authorized
    Register-DemoAccess $credential.code $point.id Exit Authorized
    Register-DemoAccess $credential.code $disabled.id Entry Rejected DisabledAccessPoint
    Register-DemoAccess $credential.code ([Guid]::NewGuid().ToString()) Entry Rejected UnknownAccessPoint
    Register-DemoAccess "UNKNOWN-$suffix" $point.id Entry Rejected UnknownCredential
)
$null = Invoke-Sgcae PATCH "/api/credentials/$($credential.id)/block"
$records += Register-DemoAccess $credential.code $point.id Entry Rejected BlockedCredential
$null = Invoke-Sgcae PATCH "/api/credentials/$($credential.id)/revoke"
$records += Register-DemoAccess $credential.code $point.id Entry Rejected RevokedCredential
$null = Invoke-Sgcae PATCH "/api/employees/$($employee.id)/deactivate" $null 204
$records += Register-DemoAccess $credential.code $point.id Entry Rejected InactiveEmployee

$to = [DateTimeOffset]::UtcNow.ToString('o')
$range = "from=$([Uri]::EscapeDataString($from))&to=$([Uri]::EscapeDataString($to))"
$filter = "employeeId=$($employee.id)&accessPointId=$($point.id)&result=Rejected&$range"
$page1 = Invoke-Sgcae GET "/api/access-records?${filter}&page=1&pageSize=2"
$page2 = Invoke-Sgcae GET "/api/access-records?${filter}&page=2&pageSize=2"
$page1Again = Invoke-Sgcae GET "/api/access-records?${filter}&page=1&pageSize=2"
if ($page1.totalCount -ne 3 -or $page2.totalCount -ne 3 -or
    @($page1.items).Count -ne 2 -or @($page2.items).Count -ne 1 -or
    ($page1.items.id -join ',') -ne ($page1Again.items.id -join ',')) {
    throw 'Historial o paginacion inesperados'
}
$history = Invoke-Sgcae GET "/api/access-records?accessPointId=$($point.id)&$range&pageSize=100"
if ($history.totalCount -ne 6) { throw 'Faltan registros del punto principal' }
$storedEntry = @($history.items | Where-Object { $_.id -eq $records[0].id })
if ($storedEntry.Count -ne 1 -or $storedEntry[0].result -ne 'Authorized') {
    throw 'No se conservo la autorizacion historica'
}

[ordered]@{
    verifiedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    attempts = $records.Count
    authorized = @($records | Where-Object result -eq Authorized).Count
    rejected = @($records | Where-Object result -eq Rejected).Count
    filteredTotal = $page1.totalCount
    page1Items = @($page1.items).Count
    page2Items = @($page2.items).Count
    authorizedExample = $records[0]
    rejectedExample = $records[5]
} | ConvertTo-Json -Depth 10
# SGCAE-DEMO-END
```

Los rechazos de negocio responden **201** porque el intento se guardó; para
saber si el acceso fue permitido se debe leer `result`. Por ejemplo, el intento
posterior al bloqueo debe incluir `result: Rejected` y
`rejectionReason: BlockedCredential`. El último devuelve `InactiveEmployee`,
que tiene prioridad sobre la revocación. Los Guid y fechas cambian en cada ejecución.

Para el recorrido manual, abrir [SGCAE.Api.http](src/SGCAE.Api/SGCAE.Api.http),
ejecutar las solicitudes en orden y reemplazar sus variables con los Guid
recibidos. Cambiar identificación y código si ya se usaron y ajustar el rango
de fechas antes de filtrar el historial.

## Evidencias de validación

Las evidencias siguientes corresponden a una ejecución local real del
29 de octubre de 2026, sobre la implementación `969cc50` y los ejemplos de
este README. Se usaron .NET SDK 10.0.401, EF Core 10.0.14 y SQL Server LocalDB.
Son resultados observados; al clonar se pueden reproducir con los comandos
anteriores y obtener nuevos archivos TRX, identificadores y fechas.

| Comprobación | Resultado observado |
| --- | --- |
| Compilación completa en Release | 0 errores y 0 advertencias. |
| SGCAE.Domain.Tests | 44 aprobadas. |
| SGCAE.Infrastructure.Tests, SQL Server real | 27 aprobadas. |
| SGCAE.Api.Tests, HTTP y SQL Server real | 200 aprobadas. |
| Total de pruebas | 271 aprobadas, 0 fallidas y 0 omitidas. |
| Migración aplicada en SGCAE_Taller2 | 20261028212642_InitialCreate; repetir database update no aplicó cambios. |
| Modelo frente al snapshot | has-pending-model-changes no encontró diferencias. |
| API en localhost:5080 | /health y /health/db respondieron 200 Healthy. |
| Demostración por HTTP | 8 intentos guardados: 2 autorizaciones y 6 rechazos. |
| Historial con cinco filtros | 3 coincidencias; páginas de 2 y 1 elementos; primera página repetida sin cambios. |
| Conservación histórica | La entrada autorizada siguió autorizada tras revocar la credencial y desactivar al empleado. |

La suite verifica índices únicos, relaciones, restricciones, aplicación de
la migración y correspondencia con el modelo, además de los escenarios de
negocio. Los TRX se generan localmente; los resultados resumidos se centralizan
aquí para mantener toda la documentación en este archivo.

Extracto de la salida real de la demostración (se omiten los dos objetos de
ejemplo completos para facilitar la lectura):

```json
{
  "verifiedAtUtc": "2026-10-29T20:44:17.7996661+00:00",
  "attempts": 8,
  "authorized": 2,
  "rejected": 6,
  "filteredTotal": 3,
  "page1Items": 2,
  "page2Items": 1
}
```

El registro autorizado de esa ejecución tiene Id
`e46c1086-d27b-4bc1-b1be-35e3bd21d917`; el rechazo por bloqueo tiene Id
`6f4d5e44-e51b-4936-a007-073cf2a63dd8`. Ambos corresponden al empleado
`7593e4b5-4526-44dd-afb9-4cf55cfd4e5d` y al punto
`a3502f77-0006-4abc-bbaf-02fe942785ab`. Estos son datos sintéticos de la
base local usada para verificar, no datos iniciales distribuidos con el repositorio.
Una ejecución nueva genera sus propios identificadores.

## Solución de problemas

| Síntoma | Comprobación y solución |
| --- | --- |
| No se encuentra un SDK compatible | Ejecutar `dotnet --list-sdks` y comprobar la banda 10.0.4xx indicada por global.json. |
| `dotnet ef` no está disponible | Ejecutar `dotnet tool restore` desde la raíz; se usa la herramienta local del repositorio. |
| LocalDB no existe o no arranca | Consultar `SqlLocalDB info`; instalar/iniciar MSSQLLocalDB o configurar otro SQL Server. |
| `/health` funciona y `/health/db` devuelve 503 | Revisar servidor, permisos, conexión y existencia de la base; aplicar la migración. |
| Error de tabla inexistente aunque `/health/db` responda 200 | La conexión funciona, pero falta el esquema: ejecutar `dotnet ef database update` con la misma conexión y entorno que la API. |
| Error 409 al repetir un registro | Identificación o código ya utilizados; cambiar los valores o consultar el registro existente. |
| Entrada rechazada con HTTP 201 | Leer result y rejectionReason; comprobar empleado activo, credencial Active y punto habilitado. |
| Puerto 5080 ocupado | Detener la instancia anterior o ejecutar con `--urls http://localhost:5081` y ajustar la URL de los ejemplos. |
| Las pruebas fallan creando una base | Revisar SGCAE_TEST_SQLSERVER y los permisos CREATE/DROP DATABASE; las pruebas no usan la conexión de la API. |
| La terminal muestra `(END)` después de un comando Git | Pulsar `q` para salir del visor y volver al prompt. |
