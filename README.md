# SGCAE - Sistema de Gestión y Control de Acceso de Empleados

Backend del taller 2 de Programación Distribuida. El sistema permitirá administrar
empleados y credenciales, registrar entradas y salidas y consultar el historial
de accesos.

## Estado actual

La solución cuenta con la estructura inicial de Clean Architecture y una API
ejecutable. Los casos de uso y la persistencia se incorporarán en las siguientes
etapas del desarrollo.

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
