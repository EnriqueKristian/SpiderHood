using SpiderHood.Models;

namespace SpiderHood.Services
{
    public class UsuarioService
    {
        private List<Usuario> usuarios = new();

        public async Task<List<Usuario>> ObtenerUsuariosAsync()
        {
            // Simulación de carga asíncrona
            await Task.Delay(100);

            if (!usuarios.Any())
            {
                usuarios = new List<Usuario>
                {
                    new Usuario { Id = 1, Nombre = "Juan Pérez", Email = "juan.perez@email.com",
                                 Telefono = "+52 55 1234 5678", Rol = "Administrador",
                                 Departamento = "101", Estado = "Activo", FechaIngreso = new DateTime(2023, 1, 14) },
                    // ... resto de usuarios
                };
            }

            return usuarios;
        }

        public async Task<Usuario?> ObtenerUsuarioPorIdAsync(int id)
        {
            var usuarios = await ObtenerUsuariosAsync();
            return usuarios.FirstOrDefault(u => u.Id == id);
        }

        public async Task AgregarUsuarioAsync(Usuario usuario)
        {
            usuarios.Add(usuario);
            await Task.CompletedTask;
        }

        public async Task ActualizarUsuarioAsync(Usuario usuarioActualizado)
        {
            var usuario = usuarios.FirstOrDefault(u => u.Id == usuarioActualizado.Id);
            if (usuario != null)
            {
                usuario.Nombre = usuarioActualizado.Nombre;
                usuario.Email = usuarioActualizado.Email;
                usuario.Telefono = usuarioActualizado.Telefono;
                usuario.Rol = usuarioActualizado.Rol;
                usuario.Departamento = usuarioActualizado.Departamento;
                usuario.Estado = usuarioActualizado.Estado;
            }
            await Task.CompletedTask;
        }

        public async Task EliminarUsuarioAsync(int id)
        {
            var usuario = usuarios.FirstOrDefault(u => u.Id == id);
            if (usuario != null)
            {
                usuarios.Remove(usuario);
            }
            await Task.CompletedTask;
        }

        public async Task<List<string>> ObtenerRolesAsync()
        {
            return await Task.FromResult(new List<string>
            {
                "Administrador",
                "Junta Directiva",
                "Residente",
                "Invitado"
            });
        }
    }
}
