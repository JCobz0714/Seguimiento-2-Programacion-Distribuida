# SGCAE - Sistema de Gestión y Control de Acceso de Empleados

Backend del taller 2 de Programación Distribuida. El sistema permitirá administrar
empleados y credenciales, registrar entradas y salidas y consultar el historial
de accesos.

## Estado actual

La solución cuenta con la estructura de Clean Architecture, una API ejecutable
y un dominio con entidades, objetos de valor y reglas de acceso probadas.
Los casos de uso y la persistencia se incorporarán en las siguientes etapas.
Las reglas y decisiones del modelo se describen en la sección Modelo de dominio de este README.

## Requisitos de desarrollo

- SDK .NET 10.0.401 o un parche posterior de la misma banda 10.0.4xx,
  según `global.json`.
- SQL Server para la futura etapa de persistencia. La estructura inicial puede
  compilarse y ejecutarse sin base de datos.

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
```

Las referencias entre proyectos son:

```text
Application    -> Domain
Infrastructure -> Application
Api            -> Application, Infrastructure
```

Domain no depende de los demás proyectos. Api referencia Infrastructure para
configurar las implementaciones mediante inyección de dependencias; los casos
de uso se organizarán en Application con separación entre comandos y consultas.

## Compilar

Desde la raíz del repositorio:

```powershell
dotnet restore SGCAE.slnx
dotnet build SGCAE.slnx --no-restore
```

## Ejecutar las pruebas

```powershell
dotnet test SGCAE.slnx --configuration Release
```

Las pruebas de dominio se ejecutan en memoria y no requieren SQL Server.

Las pruebas cubren transiciones de estados, actualizaciones atómicas, validaciones de valores, todos los motivos de rechazo para entrada y salida, integridad de las referencias, fechas UTC y conservación del resultado histórico.

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
confirma que la aplicación responde; todavía no valida una conexión a SQL Server.

## Configuración

`appsettings.json` contiene únicamente configuración general. Las credenciales
y cadenas de conexión privadas deberán proporcionarse mediante variables de
entorno o secretos de usuario de .NET cuando se agregue la persistencia.

## Modelo de dominio

### Requisitos y alcance

El dominio representa RF-01 (empleados), RF-02 (credenciales), RF-03 (movimientos)
y RF-06 (puntos de acceso). Los registros contienen los datos necesarios para
el futuro historial de RF-04. Esta etapa modela y prueba reglas en memoria;
la persistencia, los casos de uso CQRS y los endpoints se incorporan después.

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
La aplicación resolverá las entidades y proporcionará la fecha del servidor.

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
pero no puede comprobarse dentro de una entidad aislada. Se garantizará con
consultas de aplicación e índices únicos en SQL Server en las siguientes
etapas, usando una comparación consistente con estos objetos de valor.

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
usando datos de otra persona. La aplicación deberá distinguir estos errores
de los rechazos del negocio.

AccessPolicy devuelve el registro tanto al autorizar como al rechazar.
La futura capa de aplicación deberá guardar ambos resultados antes de responder;
las pruebas de dominio no demuestran todavía persistencia ni concurrencia.
