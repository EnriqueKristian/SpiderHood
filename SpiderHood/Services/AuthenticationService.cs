using Blazored.LocalStorage;
using SpiderHood.Models;
using System.Net.Http.Json;

namespace SpiderHood.Services
{
    public interface IAuthenticationService
    {
        Task<AuthResult> LoginAsync(string email, string password);
        Task<AuthResult> SocialLoginAsync(SocialLoginResult socialLogin);
        Task<RegistrationResult> RegisterAsync(RegistrationModel model);
        Task<RegistrationResult> RegisterWithInvitationAsync(UserRegistrationModel user, InvitationModel invitation);
        Task<bool> LogoutAsync();
        Task<bool> ValidateInvitationAsync(string invitationCode);
        Task<UserModel?> GetCurrentUserAsync();
        Task<List<BuildingModel>> GetUserBuildingsAsync();
    }

    public class AuthenticationService : IAuthenticationService
    {
        private readonly HttpClient _httpClient;
        private readonly ILocalStorageService _localStorage;

        public AuthenticationService(HttpClient httpClient, ILocalStorageService localStorage)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
        }

        public async Task<AuthResult> LoginAsync(string email, string password)
        {
            try
            {
                // En una implementación real, esto haría una llamada a la API
                await Task.Delay(500); // Simular llamada a API

                // Simulación de autenticación exitosa
                var user = new UserModel
                {
                    Id = 1,
                    Email = email,
                    FirstName = "Usuario",
                    LastName = "Demo",
                    Role = "Administrador",
                    Buildings = new List<BuildingModel>
                    {
                        new BuildingModel { Id = 1, Name = "Torres del Sol" }
                    }
                };

                var token = "simulated-jwt-token";

                // Guardar en localStorage
                await _localStorage.SetItemAsync("authToken", token);
                await _localStorage.SetItemAsync("user", user);

                return new AuthResult
                {
                    Success = true,
                    Message = "Login exitoso",
                    User = user,
                    Token = token
                };
            }
            catch (Exception ex)
            {
                return new AuthResult
                {
                    Success = false,
                    Message = $"Error al iniciar sesión: {ex.Message}"
                };
            }
        }

        public async Task<AuthResult> SocialLoginAsync(SocialLoginResult socialLogin)
        {
            try
            {
                // Verificar si el usuario existe
                var existingUser = await GetUserByEmailAsync(socialLogin.Email);

                if (existingUser != null)
                {
                    // Usuario existente - login
                    var token = "social-login-token";

                    await _localStorage.SetItemAsync("authToken", token);
                    await _localStorage.SetItemAsync("user", existingUser);

                    return new AuthResult
                    {
                        Success = true,
                        Message = "Login con Google exitoso",
                        User = existingUser,
                        Token = token
                    };
                }
                else
                {
                    // Nuevo usuario - redirigir a registro de edificio
                    var newUser = new UserModel
                    {
                        Email = socialLogin.Email,
                        FirstName = socialLogin.Name.Split(' ')[0],
                        LastName = socialLogin.Name.Contains(' ') ?
                                 socialLogin.Name.Substring(socialLogin.Name.LastIndexOf(' ') + 1) : "",
                        Role = "Administrador"
                    };

                    await _localStorage.SetItemAsync("socialUser", newUser);
                    await _localStorage.SetItemAsync("socialToken", socialLogin.Token);

                    return new AuthResult
                    {
                        Success = true,
                        Message = "Redirigiendo a registro de edificio",
                        User = newUser,
                        Token = socialLogin.Token
                    };
                }
            }
            catch (Exception ex)
            {
                return new AuthResult
                {
                    Success = false,
                    Message = $"Error en login social: {ex.Message}"
                };
            }
        }

        public async Task<RegistrationResult> RegisterAsync(RegistrationModel model)
        {
            try
            {
                // Crear usuario
                var user = new UserModel
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Email = model.Email,
                    PhoneNumber = model.PhoneNumber,
                    Role = "Administrador",
                    Status = "Active"
                };

                // Crear edificio
                var building = new BuildingModel
                {
                    Name = model.BuildingName,
                    Address = model.Address,
                    City = model.City,
                    TotalApartments = model.TotalApartments,
                    Floors = model.Floors,
                    BuildingType = model.BuildingType
                };

                user.Buildings.Add(building);

                // Guardar en localStorage (en una app real, sería en la API)
                await _localStorage.SetItemAsync("user", user);
                await _localStorage.SetItemAsync("building", building);

                return new RegistrationResult
                {
                    Success = true,
                    Message = "Registro completado exitosamente",
                    Email = model.Email,
                    Password = model.Password,
                    BuildingName = model.BuildingName
                };
            }
            catch (Exception ex)
            {
                return new RegistrationResult
                {
                    Success = false,
                    Message = $"Error en el registro: {ex.Message}"
                };
            }
        }

        public async Task<RegistrationResult> RegisterWithInvitationAsync(UserRegistrationModel userModel, InvitationModel invitation)
        {
            try
            {
                // Crear usuario con rol de invitación
                var user = new UserModel
                {
                    FirstName = userModel.FirstName,
                    LastName = userModel.LastName,
                    Email = userModel.Email,
                    PhoneNumber = userModel.PhoneNumber,
                    Role = invitation.Role,
                    BuildingId = invitation.Building.Id,
                    ApartmentNumber = invitation.ApartmentNumber,
                    Status = invitation.RequiresApproval ? "Pending" : "Active",
                    Buildings = new List<BuildingModel> { invitation.Building }
                };

                // Guardar en localStorage
                await _localStorage.SetItemAsync("user", user);

                // En una implementación real, aquí se enviaría la solicitud al administrador

                return new RegistrationResult
                {
                    Success = true,
                    Message = invitation.RequiresApproval ?
                             "Solicitud enviada para aprobación" :
                             "Registro completado exitosamente",
                    Email = userModel.Email,
                    Password = userModel.Password
                };
            }
            catch (Exception ex)
            {
                return new RegistrationResult
                {
                    Success = false,
                    Message = $"Error en el registro: {ex.Message}"
                };
            }
        }

        public async Task<bool> LogoutAsync()
        {
            try
            {
                await _localStorage.RemoveItemAsync("authToken");
                await _localStorage.RemoveItemAsync("user");
                await _localStorage.RemoveItemAsync("socialUser");
                await _localStorage.RemoveItemAsync("socialToken");
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> ValidateInvitationAsync(string invitationCode)
        {
            // Validar código de invitación
            await Task.Delay(300);
            return !string.IsNullOrEmpty(invitationCode);
        }

        public async Task<UserModel?> GetCurrentUserAsync()
        {
            return await _localStorage.GetItemAsync<UserModel>("user");
        }

        public async Task<List<BuildingModel>> GetUserBuildingsAsync()
        {
            var user = await GetCurrentUserAsync();
            return user?.Buildings ?? new List<BuildingModel>();
        }

        private async Task<UserModel?> GetUserByEmailAsync(string email)
        {
            // En una implementación real, esto consultaría la API
            var users = await _localStorage.GetItemAsync<List<UserModel>>("users") ?? new List<UserModel>();
            return users.FirstOrDefault(u => u.Email == email);
        }
    }
}