using System;
using System.Threading.Tasks;

namespace Services
{
    // Adds a lightweight extension method so existing call in the Razor component compiles.
    // Replace this stub with a proper implementation that uses your backend or AuthService internals.
    public static class AuthServiceExtensions
    {
        // Adds a lightweight shim so calls to SetCurrentBuilding compile.
        // Replace this with a real implementation on AuthService when possible.
        public static Task SetCurrentBuilding(this AuthService authService, UserBuilding building)
        {
            if (authService == null) throw new ArgumentNullException(nameof(authService));
            if (building == null) throw new ArgumentNullException(nameof(building));

            // NOTE:
            // - This is intentionally a no-op to resolve the CS1061 compile error.
            // - Recommended fix: add an instance method `Task SetCurrentBuilding(UserBuilding building)`
            //   to your `AuthService` implementation and implement the actual behaviour there
            //   (e.g., update a CurrentBuilding property, persist selection, notify state, etc.)
            return Task.CompletedTask;
        }
    }
}