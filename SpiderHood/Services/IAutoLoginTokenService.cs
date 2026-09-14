using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace SpiderHood.Services
{
    // Puente entre un "autologin" decidido en un circuito InteractiveServer ya
    // conectado (registro de Administrador/Colaborador/invitación -- todos
    // @rendermode InteractiveServer) y el HttpContext.SignInAsync real, que sólo
    // funciona en una request/response HTTP genuina (ver el comentario de
    // Login.razor sobre por qué). El circuito interactivo genera un token de un
    // solo uso ligado al usuario recién creado y navega (forceLoad:true, para que
    // sea una request HTTP real) a GET /auto-login/{token} -- ahí, con un
    // HttpContext real disponible, se firma la cookie de verdad y se redirige.
    //
    // Singleton en memoria, mismo patrón que ISessionRevocationService: vive todo
    // el proceso, no por circuito. Un token filtrado no vale nada pasados los 60
    // segundos ni después de usarse una vez.
    public interface IAutoLoginTokenService
    {
        string IssueToken(Guid idUser);
        Guid? Consume(string token);
    }

    public class AutoLoginTokenService : IAutoLoginTokenService
    {
        private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);
        private readonly ConcurrentDictionary<string, (Guid IdUser, DateTimeOffset Expiry)> _tokens = new();

        public string IssueToken(Guid idUser)
        {
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            _tokens[token] = (idUser, DateTimeOffset.UtcNow.Add(Ttl));
            return token;
        }

        public Guid? Consume(string token)
        {
            if (!_tokens.TryRemove(token, out var entry))
                return null;

            return entry.Expiry >= DateTimeOffset.UtcNow ? entry.IdUser : null;
        }
    }
}
