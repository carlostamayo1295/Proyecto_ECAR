# Operación del backend — Fase 1

## Configuración local

Los valores sensibles se guardan en User Secrets del proyecto API, nunca en `appsettings.json` ni en Git:

```powershell
dotnet user-secrets set "ConnectionStrings:ECARConnection" "<cadena SQL Server>" --project ECAR.API
dotnet user-secrets set "JWT:Secret" "<secreto de 32 o más caracteres>" --project ECAR.API
dotnet user-secrets set "AdminPassword" "<contraseña inicial>" --project ECAR.API
```

El archivo `secrets.json` entregado por el profesor es material local. No debe copiarse al repositorio ni enviarse por chat/correo.

## Crear o actualizar la base

El arranque de la API ejecuta `Database.MigrateAsync()`. En una base nueva crea el esquema, registra las migraciones y carga roles, administrador, categorías y ubicaciones iniciales sin duplicarlos.

```powershell
dotnet run --project ECAR.API
```

Si la base local antigua fue creada con `EnsureCreated`, puede tener tablas pero no `__EFMigrationsHistory`. No se debe “arreglar” insertando filas de migración a mano. Para un ambiente de desarrollo sin datos valiosos, cree una base nueva o elimine y regenere esa base después de confirmar un respaldo. En producción se debe preparar una migración de adopción revisada por el DBA.

## Probar en Scalar

Con el perfil HTTPS de desarrollo:

- API: `https://localhost:7296`
- OpenAPI: `https://localhost:7296/openapi/v1.json`
- Scalar: `https://localhost:7296/scalar/v1`

Primero ejecute `POST /api/auth/login`. Copie únicamente el token retornado en el botón de autenticación de Scalar. No exponga la contraseña durante una presentación.

## Modos de autenticación

El modo predeterminado es local:

```json
"ECARAuthentication": { "Mode": "Local" }
```

Valores posibles:

- `Local`: solo BCrypt local.
- `ActiveDirectory`: solo LDAP/AD.
- `Hybrid`: intenta contraseña local y, si falla, LDAP/AD.

Para activar AD se requiere configurar `ActiveDirectory:Enabled`, `Server`, `Port`, `Domain`, `UseSsl` y `TimeoutSeconds` mediante configuración segura del servidor. LDAP seguro usa el certificado confiable del servidor; el código no deshabilita su validación.

## Comandos de verificación

```powershell
dotnet build ECAR.AuditoriaEquipos.slnx --no-restore
dotnet test ECAR.API.Tests/ECAR.API.Tests.csproj -c Release
dotnet ef migrations has-pending-model-changes --project ECAR.Infrastructure --startup-project ECAR.API
dotnet list ECAR.AuditoriaEquipos.slnx package --vulnerable --include-transitive
```

## Publicación para IIS

```powershell
dotnet publish ECAR.API/ECAR.API.csproj -c Release -o ./publish/ecar-api
```

El servidor IIS necesita el Hosting Bundle de .NET 10, un sitio con HTTPS, permisos de la identidad del Application Pool y variables/configuración seguras para SQL, JWT, contraseña inicial y AD. La publicación genera `web.config`; no se deben publicar secretos dentro de la carpeta.
