using Blazored.LocalStorage;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    public interface IPreferenceService
    {
        Task<Preferences> GetPreferencesAsync(Guid userId);
        Task<AuthResult> SavePreferencesAsync(Preferences preferences);
        Task<bool> UpdateThemeAsync(Guid userId, string theme);
        Task<bool> UpdateLanguageAsync(Guid userId, string language);
    }

    public class Preferences
    {
        public Guid UserId { get; set; }
        public string Theme { get; set; } = "light";
        public string Language { get; set; } = "es";
        public bool EmailNotifications { get; set; } = true;
        public bool MonthlySummary { get; set; } = true;
        public bool PaymentReminders { get; set; } = true;
        public string ReportFrequency { get; set; } = "monthly";
        public string TimeZone { get; set; } = "America/Lima";
    }

    // Preferencias del usuario (tema, idioma, notificaciones) — viven enteramente en
    // localStorage, igual que el edificio por defecto (ver AuthService.
    // SetDefaultBuildingAsync). No hay tabla de preferencias en la base de datos: esto
    // es configuración del navegador/dispositivo, no datos de sesión ni de negocio.
    public class PreferenceService : IPreferenceService
    {
        private readonly ILogger<PreferenceService> _logger;
        private readonly ILocalStorageService _localStorage;

        public PreferenceService(ILogger<PreferenceService> logger, ILocalStorageService localStorage)
        {
            _logger = logger;
            _localStorage = localStorage;
        }

        private static string Key(Guid userId) => $"preferences_{userId}";

        public async Task<Preferences> GetPreferencesAsync(Guid userId)
        {
            try
            {
                var cached = await _localStorage.GetItemAsync<Preferences>(Key(userId));
                return cached ?? new Preferences { UserId = userId };
            }
            catch (Exception ex)
            {
                // Esperable durante el prerender estático (sin JS interop todavía) — la
                // página vuelve a pedirlas una vez conectado el circuito.
                _logger.LogWarning(ex, "No se pudieron leer las preferencias de localStorage para el usuario {UserId}", userId);
                return new Preferences { UserId = userId };
            }
        }

        public async Task<AuthResult> SavePreferencesAsync(Preferences preferences)
        {
            try
            {
                await _localStorage.SetItemAsync(Key(preferences.UserId), preferences);
                _logger.LogInformation("Preferencias guardadas para usuario {UserId}", preferences.UserId);
                return new AuthResult { Success = true, Message = "Preferencias guardadas exitosamente." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar preferencias para usuario {UserId}", preferences.UserId);
                return new AuthResult { Success = false, Message = "Error al guardar las preferencias." };
            }
        }

        public async Task<bool> UpdateThemeAsync(Guid userId, string theme)
        {
            var prefs = await GetPreferencesAsync(userId);
            prefs.Theme = theme;
            var result = await SavePreferencesAsync(prefs);
            return result.Success;
        }

        public async Task<bool> UpdateLanguageAsync(Guid userId, string language)
        {
            var prefs = await GetPreferencesAsync(userId);
            prefs.Language = language;
            var result = await SavePreferencesAsync(prefs);
            return result.Success;
        }
    }
}
