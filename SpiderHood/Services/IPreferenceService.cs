using SpiderHood.Data;
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

    public class PreferenceService : IPreferenceService
    {
        private readonly BDLayout _db;
        private readonly ILogger<PreferenceService> _logger;
        private readonly Blazored.LocalStorage.ILocalStorageService _localStorage;

        public PreferenceService(
            BDLayout db,
            ILogger<PreferenceService> logger,
            Blazored.LocalStorage.ILocalStorageService localStorage)
        {
            _db = db;
            _logger = logger;
            _localStorage = localStorage;
        }

        public async Task<Preferences> GetPreferencesAsync(Guid userId)
        {
            try
            {
                // Primero intentar desde localStorage (más rápido)
                try
                {
                    var cached = await _localStorage.GetItemAsync<string>($"preferences_{userId}");
                    if (!string.IsNullOrEmpty(cached))
                    {
                        return new Preferences();// JsonSerializer.Deserialize<Preferences>(cached) ?? new Preferences();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al leer preferencias desde localStorage");
                }

                // Si no está en caché, obtener de base de datos
                var preferences = new Preferences { UserId = userId }; //await _db.GetUserPreferencesAsync(userId);
                if (preferences == null)
                {
                    preferences = new Preferences { UserId = userId };
                    //await _db.CreateUserPreferencesAsync(preferences);
                }

                // Guardar en caché
                try
                {
                    //await _localStorage.SetItemAsync($"preferences_{userId}", JsonSerializer.Serialize(preferences));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al guardar preferencias en localStorage");
                }

                return preferences;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener preferencias para usuario {UserId}", userId);
                return new Preferences { UserId = userId };
            }
        }

        public async Task<AuthResult> SavePreferencesAsync(Preferences preferences)
        {
            try
            {
                // Guardar en base de datos
                //await _db.SaveUserPreferencesAsync(preferences);

                // Actualizar caché local
                try
                {
                    //await _localStorage.SetItemAsync($"preferences_{preferences.UserId}",
                     //   JsonSerializer.Serialize(preferences));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al actualizar caché de preferencias");
                }

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
            try
            {
                var prefs = await GetPreferencesAsync(userId);
                prefs.Theme = theme;
                await SavePreferencesAsync(prefs);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar tema para usuario {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> UpdateLanguageAsync(Guid userId, string language)
        {
            try
            {
                var prefs = await GetPreferencesAsync(userId);
                prefs.Language = language;
                await SavePreferencesAsync(prefs);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar idioma para usuario {UserId}", userId);
                return false;
            }
        }
    }
}