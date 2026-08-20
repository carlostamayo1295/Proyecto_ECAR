# Configuración de Variables de Entorno

## Información Sensible Movida a UserSecrets

Las credenciales sensibles ya no están en el código fuente. Ahora se usan **UserSecrets de .NET** para desarrollo.

## Información Protegida

### 1. Connection String de Base de Datos
- **Antes**: En `appsettings.json` (expuesto en el repo)
- **Ahora**: En UserSecrets (local, no versionado)
- **Contenido**: Server, Database, User Id, Password

### 2. JWT Secret
- **Antes**: En `appsettings.json` (expuesto en el repo)  
- **Ahora**: En UserSecrets (local, no versionado)
- **Contenido**: Clave para firmar tokens JWT

## Configuración para Nuevos Desarrolladores

### Opción 1: Usar UserSecrets (Recomendado para Desarrollo)

#### Paso 1: Clonar el repositorio
```bash
git clone git@github.com:lgoenaga/proyecto_auditoria_equipos.git
cd proyecto_auditoria_equipos
```

#### Paso 2: Configurar UserSecrets para la API
```bash
cd ECAR.API
dotnet user-secrets init
```

#### Paso 3: Establecer Connection String
```bash
dotnet user-secrets set "ConnectionStrings:ECARConnection" "Server=localhost,1433;Database=ECARDB;User Id=sa;Password=TU_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=true"
```

#### Paso 4: Establecer JWT Secret
```bash
dotnet user-secrets set "JWT:Secret" "TU_JWT_SECRET_AQUI"
```

#### Paso 5: Verificar configuración
```bash
dotnet user-secrets list
```

### Opción 2: Variables de Entorno del Sistema (Opcional)

#### Linux/Mac
```bash
export ConnectionStrings__ECARConnection="Server=localhost,1433;Database=ECARDB;User Id=sa;Password=TU_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=true"
export JWT__Secret="TU_JWT_SECRET_AQUI"
```

#### Windows (PowerShell)
```powershell
$env:ConnectionStrings__ECARConnection="Server=localhost,1433;Database=ECARDB;User Id=sa;Password=TU_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=true"
$env:JWT__Secret="TU_JWT_SECRET_AQUI"
```

## Configuración para Producción

### Usar Variables de Entorno del Sistema/Contenedor

#### Docker
```bash
docker run -e ConnectionStrings__ECARConnection="..." -e JWT__Secret="..." ...
```

#### Kubernetes/Cloud
Configurar en el deployment configuration:
```yaml
env:
  - name: ConnectionStrings__ECARConnection
    value: "Server=prod-server;Database=ECARDB;..."
  - name: JWT__Secret
    value: "PRODUCTION_JWT_SECRET"
```

#### Azure App Service
Configurar en Application Settings del portal.

## Valores Recomendados

### Development
- **JWT Secret**: Puede ser cualquier string largo para desarrollo
- **Database**: Usar SQL Server local con contenedor Docker

### Production
- **JWT Secret**: Usar string aleatorio largo (mínimo 32 caracteres)
- **Database**: Usar servidor de base de datos de producción
- **Considerar**: Azure Key Vault, AWS Secrets Manager para secretos

## Verificación

### Verificar que UserSecrets Funciona
```bash
cd ECAR.API
dotnet run --launch-profile https
```

Si la API inicia y muestra el connection string correcto en consola, la configuración funciona.

### Verificar que Variables de Entorno Funcionan
```bash
# Linux/Mac
unset ConnectionStrings__DefaultConnection
# Windows PowerShell
$env:ConnectionStrings__DefaultConnection = $null
```

## Prioridad de Configuración

.NET Configuration API busca en este orden:
1. Variables de entorno del sistema
2. UserSecrets (solo en Development)
3. appsettings.Development.json
4. appsettings.json

## Seguridad

- **Nunca** commitear credenciales reales en el código
- **Nunca** commitear UserSecrets (están en `~/.microsoft/usersecrets/`)
- **Usar** secretos fuertes en producción
- **Rotar** secretos periódicamente
- **Considerar** usar servicios de gestión de secretos en producción

## Archivos de Configuración

### appsettings.json (Versionado)
- Contiene estructura de configuración
- Valores sensibles vacíos o placeholders
- Se usa como plantilla

### appsettings.Development.json (No versionado)
- Puede tener valores de desarrollo
- Excluido por .gitignore
- UserSecrets tiene prioridad

### UserSecrets (No versionado)
- Ubicación: `~/.microsoft/usersecrets/{ProjectId}/secrets.json`
- Solo accesible localmente
- No se versiona en git