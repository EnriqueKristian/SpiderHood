using System.Collections.Concurrent;

namespace SpiderHood.Services
{
    // Lista de revocación de sesiones, en memoria del servidor.
    //
    // La cookie de autenticación (ver Program.cs) es válida por su cuenta hasta que
    // expira — cambiar la contraseña o pedir "cerrar sesión en todos los dispositivos"
    // NO invalida por sí solo ninguna cookie ya emitida (la tuya en otro navegador, o
    // una robada). Esto es lo que cierra ese hueco: guarda, por usuario, el instante a
    // partir del cual toda cookie emitida ANTES se considera inválida. El middleware de
    // cookies (OnValidatePrincipal en Program.cs) consulta esto en cada validación.
    //
    // Es en memoria, no en base de datos, a propósito: no requiere ningún cambio de
    // esquema. El costo es que un reinicio del proceso la vacía — para lo que cubre hoy
    // (matar sesiones tras cambio de contraseña o un pedido explícito del usuario) es un
    // costo aceptable frente a la complejidad de agregar y mantener una columna nueva.
    public interface ISessionRevocationService
    {
        void RevokeAllSessions(Guid userId);
        bool IsRevoked(Guid userId, DateTimeOffset sessionIssuedAt);
    }

    public class SessionRevocationService : ISessionRevocationService
    {
        private readonly ConcurrentDictionary<Guid, DateTimeOffset> _revokedBefore = new();

        public void RevokeAllSessions(Guid userId)
        {
            _revokedBefore[userId] = DateTimeOffset.UtcNow;
        }

        public bool IsRevoked(Guid userId, DateTimeOffset sessionIssuedAt)
        {
            return _revokedBefore.TryGetValue(userId, out var revokedAt) && sessionIssuedAt < revokedAt;
        }
    }
}
