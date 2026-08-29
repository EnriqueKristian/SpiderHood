
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace SpiderHood.Components;

// `RenderMode.InteractiveServer` (usado en @rendermode InteractiveServer) es un
// singleton estático — la MISMA instancia sin importar cuántos componentes la
// referencien, lo que le permite a Blazor coalescer varios componentes con
// @rendermode redundante (layout + página, p.ej. HeaderMainLayout/LeftMenu/cada
// página) en un solo circuito interactivo.
//
// "new InteractiveServerRenderMode(prerender: false)" evaluado inline en cada
// archivo .razor crea una instancia NUEVA cada vez, rompiendo esa coalescencia:
// cada componente termina como su propia raíz interactiva independiente, disparando
// un circuito (y una instancia de CustomAuthenticationStateProvider) por cada uno en
// vez de uno solo por página. Este singleton evita ese problema.
public static class AppRenderMode
{
    public static readonly IComponentRenderMode InteractiveServerNoPrerender =
        new InteractiveServerRenderMode(prerender: false);
}