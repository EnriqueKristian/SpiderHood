namespace SpiderHood.Services
{
    // Estado de UI puro (sidebar colapsado a solo íconos o expandido), compartido entre
    // MainLayout (dueño del ancho real de .sidebar) y LeftMenu (dueño del botón toggle y
    // del contenido que cambia según el estado) sin acoplarlos por parámetros a través de
    // NavMenu -- mismo patrón pub/sub que CustomAuthenticationStateProvider.AuthenticationStateChanged.
    // Scoped: vive por circuito (por usuario conectado), igual que esos servicios.
    public class SidebarStateService
    {
        public bool Collapsed { get; private set; }

        public event Action? CollapsedChanged;

        public void SetCollapsed(bool collapsed)
        {
            if (Collapsed == collapsed) return;
            Collapsed = collapsed;
            CollapsedChanged?.Invoke();
        }
    }
}
