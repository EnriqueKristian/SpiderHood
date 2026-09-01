using System.Globalization;

namespace SpiderHood.Models
{
    // Extension methods para utilidades
    public static class CuotaExtensions
    {
        public static string FormatoMoneda(this decimal valor)
        {
            return valor.ToString("C");
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
