using System.Security.Claims;
using ECAR.API.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace ECAR.API.Tests;

public class BackendPhaseThreeTests
{
    [Fact]
    public void CurrentUser_LeeIdentidadYRolesDesdeElToken()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "42"),
            new Claim(ClaimTypes.Name, "Carlos Tamayo"),
            new Claim(ClaimTypes.Role, "Administrador"),
            new Claim(ClaimTypes.Role, "Técnico")
        ], "Test"));
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        ICurrentUser currentUser = new CurrentUser(accessor);

        Assert.Equal(42, currentUser.IdUsuario);
        Assert.Equal("Carlos Tamayo", currentUser.Nombre);
        Assert.Equal(["Administrador", "Técnico"], currentUser.Roles);
        Assert.True(currentUser.IsInRole("Administrador"));
    }

    [Fact]
    public void CurrentUser_RechazaTokenSinIdentificadorValido()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "usuario-invalido")
        ], "Test"));
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        ICurrentUser currentUser = new CurrentUser(accessor);

        var exception = Assert.Throws<InvalidOperationException>(() => currentUser.IdUsuario);

        Assert.Contains("identificador de usuario válido", exception.Message);
    }
}
