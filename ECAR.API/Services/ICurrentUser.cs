namespace ECAR.API.Services;

public interface ICurrentUser
{
    long IdUsuario { get; }
    string Nombre { get; }
    IReadOnlyCollection<string> Roles { get; }
    bool IsInRole(string role);
}
