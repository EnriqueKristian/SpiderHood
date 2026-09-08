using System.Globalization;

namespace SpiderHood.Models
{
    // Extension methods para utilidades
    public static class CuotaExtensions
    {
        // BuildingConfiguration.Currency (PEN/USD/EUR, ver Classes/Building.cs) es la
        // moneda con la que transacciona CADA edificio -- .ToString("C")/"C2" (que se
        // usaba en toda la app) ignora esto por completo y formatea según la cultura del
        // SERVIDOR, no la del edificio, así que un edificio en USD podía mostrarse en
        // soles o viceversa según qué cultura tuviera configurado el server. Símbolo +
        // InvariantCulture en vez de CultureInfo.GetCultureInfo(código de moneda): un
        // código de moneda ISO no define una cultura/formato regional real (separadores
        // de miles, orden del símbolo), y son 3 monedas nada más -- no vale la pena
        // mantener ese mapeo.
        private static readonly Dictionary<string, string> _currencySymbols = new()
        {
            ["PEN"] = "S/",
            ["USD"] = "$",
            ["EUR"] = "€",
        };

        public static string ToCurrencySymbol(this string? currencyCode)
        {
            if (string.IsNullOrWhiteSpace(currencyCode))
                return "S/"; // Default histórico de BuildingConfiguration.Currency

            return _currencySymbols.TryGetValue(currencyCode, out var symbol) ? symbol : currencyCode;
        }

        // Reemplaza a valor.ToString("C"/"C2") en toda la UI -- ver comentario arriba.
        public static string FormatoMoneda(this decimal valor, string? currencyCode)
        {
            return $"{currencyCode.ToCurrencySymbol()} {valor.ToString("N2", CultureInfo.InvariantCulture)}";
        }

        // Sobrecarga sin moneda: sólo para código que no tiene forma de acceder al
        // edificio actual (reportes/exports fuera de un componente Razor). Prefer
        // siempre la sobrecarga con currencyCode cuando esté disponible.
        public static string FormatoMoneda(this decimal valor)
        {
            return valor.FormatoMoneda(null);
        }

        public static string FormatoPorcentaje(this decimal valor)
        {
            return valor.ToString("N2") + "%";
        }

        public static string FormatoFecha(this DateTime fecha)
        {
            return fecha.ToString("dd/MM/yyyy");
        }

        public static string FormatoFechaHora(this DateTime fecha)
        {
            return fecha.ToString("dd/MM/yyyy HH:mm");
        }

        public static string GetNombreMes(this int mes)
        {
            if (mes < 1 || mes > 12)
                return "Desconocido";

            return CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(mes);
        }

        public static List<SelectListItem> GetMesesSelectList()
        {
            return [..Enumerable.Range(1, 12)
                .Select(m => new SelectListItem
                {
                    Value = m.ToString(),
                    Text = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(m)
                })
                ];
        }

        public static List<SelectListItem> GetAniosSelectList(int aniosAtras = 10, int aniosAdelante = 2)
        {
            var anioActual = DateTime.Now.Year;
            var anios = Enumerable.Range(anioActual - aniosAtras, aniosAtras + aniosAdelante + 1);

            return [..anios
                .Select(a => new SelectListItem
                {
                    Value = a.ToString(),
                    Text = a.ToString()
                })
                .OrderByDescending(x => x.Value)
                ];
        }

        public static decimal CalcularMontoPorcentual(this decimal montoTotal, decimal porcentaje)
        {
            return Math.Round(montoTotal * (porcentaje / 100), 2);
        }

        public static decimal CalcularMontoFijo(this decimal montoTotal, int totalDepartamentos)
        {
            if (totalDepartamentos <= 0)
                return 0;

            var montoBase = Math.Round(montoTotal / totalDepartamentos, 2);

            // Ajuste por redondeo
            var totalDistribuido = montoBase * totalDepartamentos;
            var diferencia = montoTotal - totalDistribuido;

            return montoBase + (diferencia / totalDepartamentos);
        }
    }
}
