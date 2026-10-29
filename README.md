# SGCAE - Sistema de Gestión y Control de Acceso de Empleados

Backend del taller 2 de Programación Distribuida. El sistema permite administrar
empleados y credenciales, registrar entradas y salidas y consultar el historial
de accesos.

## Estado actual

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

## Requisitos de desarrollo

- SDK .NET 10.0.401 o un parche posterior de la misma banda 10.0.4xx,
  según `global.json`.
- SQL Server. En Windows se puede usar SQL Server Express LocalDB con la
  instancia `MSSQLLocalDB`; para otro servidor, configurar su conexión.
- Acceso a NuGet para restaurar los paquetes y la herramienta local `dotnet-ef`.

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
dotnet build SGCAE.slnx --no-restore
```

## Ejecutar las pruebas

```powershell
dotnet test SGCAE.slnx --configuration Release
```

El comando anterior ejecuta todas las pruebas y requiere SQL Server LocalDB
en Windows o la variable de entorno `SGCAE_TEST_SQLSERVER` con la conexión
a otro servidor SQL Server. La cuenta de pruebas debe poder crear y eliminar
bases de datos. Cada conjunto de integración crea su propia base
`SGCAE_Tests_<Guid>`, aplica las migraciones y elimina únicamente esa base
al finalizar. Las pruebas de persistencia revierten sus transacciones; las
pruebas HTTP limpian los datos de su base temporal antes de cada caso.
No se usa la base `SGCAE_Taller2` para las pruebas.

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
registro, para apoyar las consultas y paginación de las próximas etapas.

La base garantiza unicidad e integridad referencial. Las decisiones de acceso
y las transiciones siguen siendo responsabilidad del dominio y de los
casos de uso. La persistencia no sustituye las reglas del negocio.

## Gestión de empleados (RF-01)

Decisiones de la API para este bloque: la identificación se asigna al registrar
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

Este bloque incorpora registro y consulta con el estado del punto. Se crea
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
Este bloque usa la tabla y la migración existentes.

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
Este bloque utiliza el esquema existente.

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

Los ejemplos HTTP contienen una entrada, una salida y un intento con código
desconocido. Para observar una autorización, usar un empleado activo, una
credencial vigente y un punto habilitado: los ejemplos de bloques anteriores
también incluyen desactivación y revocación, que generan rechazos posteriores.

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
   deshabilitado. El dominio permite habilitarlo y deshabilitarlo.
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
