using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;

namespace SpiderHood.Models
{
    public class RegistrationModel
    {
        // Información personal
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string ConfirmPassword { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public bool AcceptTerms { get; set; }

        // Información del edificio (para administrador)
        public string BuildingName { get; set; } = "";
        public string Address { get; set; } = "";
        public string City { get; set; } = "";
        public int TotalApartments { get; set; } = 1;
        public int Floors { get; set; } = 1;
        public int ConstructionYear { get; set; } = DateTime.Now.Year;
        public string BuildingType { get; set; } = "";
        public string BuildingDescription { get; set; } = "";
    }

    public class UserRegistrationModel
    {
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string ConfirmPassword { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public bool AcceptTerms { get; set; }
    }

    public class InvitationModel
    {
        public Guid IdInvitation { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public Guid IdBuilding { get; set; }
        public string BuildingName { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string InvitedBy { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int ApartmentNumber { get; set; }
        public bool RequiresApproval { get; set; }
        public string AdminMessage { get; set; } = string.Empty;
        public DateTime ExpirationDate { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Accepted, Rejected, Expired
    }

    public class AuthResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public UserModel? User { get; set; }
        public string Token { get; set; } = "";
        public bool RequiresApproval { get; set; }
    }

    public class RegistrationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string BuildingName { get; set; } = "";
    }

    public class SocialLoginResult
    {
        public bool Success { get; set; }
        public string Provider { get; set; } = ""; // Google, Facebook
        public string Email { get; set; } = "";
        public string Name { get; set; } = "";
        public string Token { get; set; } = "";
    }

    public class BankAccount
    {
        public Guid IdBankAccount { get; set; }
        public string AccountName { get; set; } = "";
        public string AccountNumber { get; set; } = "";
        public string CCI { get; set; } = "";
        public string BankName { get; set; } = "";
        public int AccountType { get; set; }  // Ahorros, Corriente, etc.
        [Precision(18, 2)]
        public decimal CurrentBalance { get; set; }
        [Precision(18, 2)]
        public decimal ReconciledBalance { get; set; }
        public DateTime LastReconciliation { get; set; }
        public int Status { get; set; }
        public Guid IdBuilding { get; set; }

        public BankAccount Clone() => (BankAccount)this.MemberwiseClone();
    }

    public class ViewExpense
    {
        public Guid IdExpense { get; set; }
        public string Description { get; set; } = string.Empty;
        [Precision(18, 2)]
        public decimal Amount { get; set; }
        public string Category { get; set; } = string.Empty;
        public Guid IdCategory { get; set; }
        public string Supplier { get; set; } = string.Empty;
        public PaymentMethod PaymentMethod { get; set; }
        public StatusExpense Status { get; set; }  // Pending, Approved, Rejected
        public bool Reconciled { get; set; } = false;
        public Guid? ReconciledTransactionId { get; set; }
        public TypeDistribution? Distribution { get; set; }
        public Guid? IdBuilding { get; set; }
        public string Notes { get; set; } = string.Empty;
        //public DateTime CreatedDate { get; set; } = DateTime.Now;
        public bool AutoReconcile { get; set; } = true;
        public DateTime ExpenseDate { get; set; }
        public bool IncludeInQuota { get; set; }
        //public DateTime? PaymentDate { get; set; }
    }

    public class Conciliacion
    {
        public int Id { get; set; }
        public Guid CuentaBancariaId { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public int TransaccionesProcesadas { get; set; }
        public int TransaccionesConciliadas { get; set; }
        [Precision(18, 2)]
        public decimal Diferencia { get; set; }
        public bool Completada { get; set; }
        public DateTime Fecha { get; set; } = DateTime.Now;
        public string Usuario { get; set; } = "";
        public string Notas { get; set; } = "";
    }

    public class Installment
    {
        public Guid IdInstallment { get; set; }
        public Guid IdBudgetHeader { get; set; }
        public int Number { get; set; }
        public string UnitName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public DateTime CreationDate { get; set; }
        [Precision(18, 2)] public decimal Amount { get; set; }
        [Precision(18, 2)] public decimal Percent { get; set; }
        [Precision(18, 2)] public decimal TotalArea { get; set; }
        public DateTime Period { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public ConcilationType Status { get; set; }
        [Precision(18, 2)] public decimal AmountPaid { get; set; }
        [Precision(18, 2)] public decimal Debt { get; set; }
        public Guid IdGroupUnit { get; set; }
        public DateTime DueDate { get; set; }
        // Ordinaria (default, cuota mensual normal) vs. Extraordinaria/Multa/Mora,
        // generadas por ExtraChargeService bajo su propio BudgetHeader. Requiere la
        // columna Installment.Type — ver
        // Database/Migrations/2026-08-28_CuotasExtraordinarias_MultasMora.sql.
        public InstallmentType Type { get; set; } = InstallmentType.Ordinaria;
        // Descripción libre para cuotas que no vienen de BudgetDetail (p.ej. "Fondo de
        // obras - pintado fachada", "Mora (2 meses de atraso) - Cuota Jun-2026"). Las
        // Ordinarias la dejan vacía porque su desglose sale de BudgetHeader.Details.
        public string Concept { get; set; } = string.Empty;
        // Para Multa/Mora: IdInstallment de la cuota Ordinaria vencida que originó el
        // cargo. Permite calcular mora incremental (cuánto ya se cobró de más contra
        // esa cuota) sin duplicar ni necesitar UPDATE. Guid.Empty para Ordinaria/Extraordinaria.
        public Guid SourceInstallmentId { get; set; } = Guid.Empty;
        [NotMapped]
        public bool IsPaid { get; set; } = false;
        [NotMapped]
        public bool Reconciled { get; set; } = false;
        [NotMapped]
        public Guid ReconciledTransactionId { get; set; }
        [NotMapped]
        public bool AutoReconcile { get; set; } = false;
        [NotMapped]
        public DateTime LastPartialPaymentDate { get; set; }
        [NotMapped]
        public List<TransactionBankDetail> PosiblesMatches { get; set; } = [];
        [NotMapped]
        public List<InstallmentPaid> Paids { get; set; } = [];
        [NotMapped]
        public List<TransactionBankDetail> PreviousPaid { get; set; } = [];
    }

    // Models/DetalleCuota.cs
    public class InstallmentPaid
    {
        public Guid IdPaid { get; set; }
        public Guid IdInstallment { get; set; }
        public DateTime PaymentDate { get; set; }
        public Guid IdTransaction { get; set; }

        [Precision(18, 2)]
        public decimal Amount { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public ConcilationType Status { get; set; }
        public bool IsAutoReconcile { get; set; } = false;
        public bool IsPartialPayment { get; set; } = false;
    }

    public class CuotaMensual
    {
        public int Id { get; set; }
        public int Mes { get; set; }
        public int Anio { get; set; }
        public DateTime FechaGeneracion { get; set; }
        [Precision(18, 2)]
        public decimal TotalDistribuido { get; set; }
        public string UsuarioGeneracion { get; set; } = string.Empty;
        public List<DetalleCuota> Detalles { get; set; } = [];
        public bool Procesada { get; set; }
    }

    // Models/DetalleCuota.cs
    public class DetalleCuota
    {
        public int Id { get; set; }
        public int CuotaId { get; set; }
        public int DepartamentoId { get; set; }
        public Guid GastoId { get; set; }
        [Precision(18, 2)]
        public decimal Monto { get; set; }
        public DateTime FechaVencimiento { get; set; }
        public bool Pagado { get; set; }
    }

    // Models/ResultadoGeneracion.cs
    public class ResultadoGeneracion
    {
        public bool Exito { get; set; }
        public int CuotaId { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        [Precision(18, 2)]
        public decimal TotalGenerado { get; set; }
        public int TotalDepartamentos { get; set; }
        public int TotalGastosIncluidos { get; set; }
        public DateTime FechaGeneracion { get; set; }
        public Dictionary<string, decimal> ResumenPorCategoria { get; set; } = [];
        public List<string> Errores { get; set; } = [];
        public List<string> Advertencias { get; set; } = [];

        public static ResultadoGeneracion Exitoso(int cuotaId, string mensaje = "Cuota generada exitosamente")
        {
            return new ResultadoGeneracion
            {
                Exito = true,
                CuotaId = cuotaId,
                Mensaje = mensaje,
                FechaGeneracion = DateTime.Now
            };
        }

        public static ResultadoGeneracion Error(string mensaje, List<string>? errores = null)
        {
            return new ResultadoGeneracion
            {
                Exito = false,
                Mensaje = mensaje,
                FechaGeneracion = DateTime.Now,
                Errores = errores ?? []
            };
        }

        public void AgregarError(string error)
        {
            Errores.Add(error);
            Exito = false;
        }

        public void AgregarAdvertencia(string advertencia)
        {
            Advertencias.Add(advertencia);
        }
    }

    // Resultado de generar una cuota extraordinaria: cuántas unidades quedaron con
    // cargo y el total repartido, para mostrar una confirmación en la UI.
    public class CuotaExtraordinariaResultado
    {
        public bool Exito { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public Guid IdBudgetHeader { get; set; }
        public int UnidadesConCargo { get; set; }
        public decimal TotalRepartido { get; set; }
    }

    // Resultado de correr el proceso de Multas y Mora: qué se generó en esta corrida
    // (no es un acumulado histórico, solo lo que se creó ahora).
    public class AplicacionCargosResultado
    {
        public bool Exito { get; set; } = true;
        public string Mensaje { get; set; } = string.Empty;
        public int CuotasRevisadas { get; set; }
        public int UnidadesConMulta { get; set; }
        public int UnidadesConMora { get; set; }
        public decimal TotalMultas { get; set; }
        public decimal TotalMora { get; set; }
        public List<string> Detalle { get; set; } = [];
    }

    // Models/ViewModels para la UI
    public class GastoPendienteViewModel
    {
        public Guid Id { get; set; }
        public string CategoriaNombre { get; set; } = string.Empty;
        public int CategoriaId { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        [Precision(18, 2)]
        public decimal Monto { get; set; }
        public DateTime FechaGasto { get; set; }
        public DateTime FechaVencimiento { get; set; }
        public TipoDistribucion TipoDistribucion { get; set; }
        public bool Seleccionado { get; set; }
        public bool Pagado { get; set; }
        public bool ConsideradoEnCuota { get; set; }
        public string Observaciones { get; set; } = string.Empty;

        // Propiedades calculadas
        public string DisplayFecha => FechaGasto.ToString("dd/MM/yyyy");
        public string DisplayMonto => Monto.ToString("C");
        public string DisplayTipoDistribucion => TipoDistribucion.ToString();
        public string EstadoColor => Pagado ? "success" : ConsideradoEnCuota ? "warning" : "danger";
        public string EstadoTexto => Pagado ? "Pagado" : ConsideradoEnCuota ? "En Cuota" : "Pendiente";
    }

    public class CuotaViewModel
    {
        public int Id { get; set; }
        public string Periodo { get; set; } = string.Empty;
        public string MesNombre { get; set; } = string.Empty;
        public int Mes { get; set; }
        public int Anio { get; set; }
        public DateTime FechaGeneracion { get; set; }
        public string DisplayFechaGeneracion => FechaGeneracion.ToString("dd/MM/yyyy HH:mm");
        [Precision(18, 2)]
        public decimal TotalDistribuido { get; set; }
        public string DisplayTotal => TotalDistribuido.ToString("C");
        public string UsuarioGeneracion { get; set; } = string.Empty;
        public bool Procesada { get; set; }
        public string Estado => Procesada ? "Procesada" : "Pendiente";
        public string EstadoColor => Procesada ? "success" : "warning";
        public int TotalDepartamentos { get; set; }
        public int TotalGastos { get; set; }

        // Para filtros
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
    }

    public class DetalleCuotaDepartamentoViewModel
    {
        public int DepartamentoId { get; set; }
        public string DepartamentoNombre { get; set; } = string.Empty;
        [Precision(18, 2)]
        public decimal AreaM2 { get; set; }
        [Precision(18, 2)]
        public decimal PorcentajeArea { get; set; }
        [Precision(18, 2)]
        public decimal TotalAPagar { get; set; }
        public List<DetalleCuotaCategoriaViewModel> DetallePorCategoria { get; set; } = [];
        public bool Pagado { get; set; }
        public DateTime? FechaPago { get; set; }
    }

    public class DetalleCuotaCategoriaViewModel
    {
        public int CategoriaId { get; set; }
        public string CategoriaNombre { get; set; } = string.Empty;
        public TipoDistribucion TipoDistribucion { get; set; }
        [Precision(18, 2)]
        public decimal Monto { get; set; }
        [Precision(18, 2)]
        public decimal PorcentajeDelTotal { get; set; }
        public string DescripcionGasto { get; set; } = string.Empty;
        public int GastoId { get; set; }
    }

    public class ResumenGeneracionViewModel
    {
        public DateTime FechaGeneracion { get; set; }
        public string Periodo { get; set; } = string.Empty;
        public int TotalDepartamentos { get; set; }
        public int TotalGastosIncluidos { get; set; }
        [Precision(18, 2)]
        public decimal TotalMonto { get; set; }
        [Precision(18, 2)]
        public decimal PromedioPorDepartamento { get; set; }
        public List<GastoIncluidoViewModel> GastosIncluidos { get; set; } = [];
        public List<DistribucionDepartamentoViewModel> DistribucionDepartamentos { get; set; } = [];
        public Dictionary<string, decimal> DistribucionPorCategoria { get; set; } = [];
    }

    public class GastoIncluidoViewModel
    {
        public int Id { get; set; }
        public string Categoria { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        [Precision(18, 2)]
        public decimal Monto { get; set; }
        public string TipoDistribucion { get; set; } = string.Empty;
        public DateTime FechaGasto { get; set; }
    }

    public class DistribucionDepartamentoViewModel
    {
        public string Departamento { get; set; } = string.Empty;
        [Precision(18, 2)]
        public decimal Monto { get; set; }
        [Precision(18, 2)]
        public decimal Porcentaje { get; set; }
        [Precision(18, 2)]
        public decimal AreaM2 { get; set; }
        [Precision(18, 2)]
        public decimal PorcentajeArea { get; set; }
    }

    // Models/SelectListItem para combos
    public class SelectListItem
    {
        public string Value { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public bool Selected { get; set; }
        public object? Data { get; set; } = null;
    }

    public class ToastMessage
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public ToastType Type { get; set; }
        public int Duration { get; set; } = 3000;
    }

    public enum ToastType
    {
        Success,
        Error,
        Warning,
        Info
    }

    /// <summary>
    /// Clase auxiliar para resultados de operaciones
    /// </summary>
    public class OperationResult
    {
        public bool IsSuccess { get; }
        public string? ErrorMessage { get; }
        public object? Data { get; }

        private OperationResult(bool isSuccess, string? errorMessage = null, object? data = null)
        {
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
            Data = data;
        }

        public static OperationResult Success(object? data = null)
            => new OperationResult(true, data: data);

        public static OperationResult Failure(string errorMessage)
            => new OperationResult(false, errorMessage);
    }

    // Models/FiltroCuotas para búsquedas
    public class FiltroCuotas
    {
        public int? Anio { get; set; }
        public int? Mes { get; set; }
        public bool? Procesada { get; set; }
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
        public string UsuarioGeneracion { get; set; } = string.Empty;
        [Precision(18, 2)]
        public decimal? MontoMinimo { get; set; }
        [Precision(18, 2)]
        public decimal? MontoMaximo { get; set; }
        public string OrdenarPor { get; set; } = "FechaGeneracion";
        public bool OrdenDescendente { get; set; } = true;
        public int Pagina { get; set; } = 1;
        public int TamanoPagina { get; set; } = 20;

        public bool TieneFiltros =>
            Anio.HasValue ||
            Mes.HasValue ||
            Procesada.HasValue ||
            FechaDesde.HasValue ||
            FechaHasta.HasValue ||
            !string.IsNullOrEmpty(UsuarioGeneracion) ||
            MontoMinimo.HasValue ||
            MontoMaximo.HasValue;
    }

    // Models/ConfiguracionSistema para parámetros
    public class ConfiguracionSistema
    {
        public int Id { get; set; }
        public string Clave { get; set; } = string.Empty;
        public string Valor { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty; // "string", "int", "decimal", "bool", "date"
        public string Grupo { get; set; } = string.Empty;
        public int Orden { get; set; }
        public bool Editable { get; set; } = true;
    }

    // Models/Notificacion para mensajes al usuario
    public class Notificacion
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Tipo { get; set; } = string.Empty; // "success", "error", "warning", "info"
        public string Titulo { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
        public DateTime Fecha { get; set; } = DateTime.Now;
        public bool Leida { get; set; }
        public int Timeout { get; set; } = 5000; // milisegundos
        public bool AutoCerrar { get; set; } = true;

        public string Icono => Tipo switch
        {
            "success" => "fa-check-circle",
            "error" => "fa-exclamation-circle",
            "warning" => "fa-exclamation-triangle",
            "info" => "fa-info-circle",
            _ => "fa-info-circle"
        };

        public string Color => Tipo switch
        {
            "success" => "bg-success text-white",
            "error" => "bg-danger text-white",
            "warning" => "bg-warning text-dark",
            "info" => "bg-info text-white",
            _ => "bg-info text-white"
        };
    }

    // Models/Auditoria para tracking
    public class Auditoria
    {
        public int Id { get; set; }
        public string Entidad { get; set; } = string.Empty;
        public int EntidadId { get; set; }
        public string Accion { get; set; } = string.Empty; // "CREATE", "UPDATE", "DELETE", "GENERATE"
        public string Usuario { get; set; } = string.Empty;
        public DateTime Fecha { get; set; } = DateTime.Now;
        public string DatosOriginales { get; set; } = string.Empty;
        public string DatosNuevos { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public string Observaciones { get; set; } = string.Empty;
    }

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

    // Models/ViewModel para Detalle Cuota
    public class DetalleCuotaViewModel
    {
        public int Id { get; set; }
        public int CuotaId { get; set; }
        public int DepartamentoId { get; set; }
        public string DepartamentoNombre { get; set; } = string.Empty;
        [Precision(18, 2)]
        public decimal AreaM2 { get; set; }
        [Precision(18, 2)]
        public decimal PorcentajeArea { get; set; }
        public int GastoId { get; set; }
        public string CategoriaNombre { get; set; } = string.Empty;
        public string DescripcionGasto { get; set; } = string.Empty;
        public TipoDistribucion TipoDistribucion { get; set; }
        [Precision(18, 2)]
        public decimal Monto { get; set; }
        public DateTime FechaVencimiento { get; set; }
        public bool Pagado { get; set; }
    }

    // Models/ViewModel para Gasto
    public class GastoViewModel
    {
        public int Id { get; set; }
        public string CategoriaNombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        [Precision(18, 2)]
        public decimal Monto { get; set; }
        public DateTime FechaGasto { get; set; }
        public DateTime FechaVencimiento { get; set; }
        public TipoDistribucion TipoDistribucion { get; set; }
        public bool Pagado { get; set; }
    }

    // Models/Resumen de Cuota
    public class ResumenCuota
    {
        public int CuotaId { get; set; }
        public string Periodo { get; set; } = string.Empty;
        public int TotalDepartamentos { get; set; }
        public int TotalGastos { get; set; }
        [Precision(18, 2)]
        public decimal TotalMonto { get; set; }
        [Precision(18, 2)]
        public decimal PromedioPorDepartamento { get; set; }
        public Dictionary<string, decimal>? DistribucionPorCategoria { get; set; }
        public Dictionary<string, decimal>? DistribucionPorDepartamento { get; set; }
        public List<GastoResumen>? GastosPrincipales { get; set; }
    }

    public class GastoResumen
    {
        public string Categoria { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        [Precision(18, 2)]
        public decimal Monto { get; set; }
        [Precision(18, 2)]
        public decimal PorcentajeDelTotal { get; set; }
    }

    public class BudgetHeader
    {
        public Guid IdBudgetHeader { get; set; }
        public string BudgetName { get; set; } = string.Empty;
        // Un new BudgetHeader() recién creado (flujo de "nuevo presupuesto") queda con
        // BudgetDate = default(DateTime) = 0001-01-01 si no se inicializa aquí. SQL Server
        // sólo acepta datetime desde 1753-01-01, así que cualquier consulta que use esa
        // fecha antes de que el usuario la elija en el modal "Nuevo Cálculo" (p.ej. cargar
        // gastos pendientes de conciliación) truena con SqlTypeException y tumba el circuito
        // entero de Blazor Server.
        public DateTime BudgetDate { get; set; } = DateTime.Now;
        [Precision(18, 2)]
        public decimal Amount { get; set; }
        [Precision(18, 2)]
        public decimal AnnualAmount { get; set; }
        public string BudgetType { get; set; } = string.Empty;
        public Guid IdBuilding { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public Guid? IdPeriod { get; set; }
        public DateTime CreatedOn { get; set; }
        [NotMapped]
        public int Month { get { return BudgetDate.Month; } }
        [NotMapped]
        public int Year { get { return BudgetDate.Year; } }
        public BudgetStatus Status { get; set; }
        public string Mes => CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(Month);

        [NotMapped]
        public List<BudgetDetail> Details { get; set; } = [];
    }

    public class BudgetDetail
    {
        public Guid IdBudgetDetail { get; set; }
        public Guid IdCategory { get; set; }
        public int IdSection { get; set; }
        [Precision(18, 2)]
        public decimal ItemNumber { get; set; }
        public string Description { get; set; } = "";
        [Precision(18, 2)]
        public decimal MonthlyAmount { get; set; }
        [Precision(18, 2)]
        public decimal AnnualAmount { get; set; }
        public int Frequency { get; set; }
        public int Type { get; set; }
        public bool IsHeader { get; set; } = false;
        public Guid IdBudgetHeader { get; set; }
        public bool IsNewItem { get; set; } = false;
        public Guid IdParent { get; set; }
    }

    public class ViewBudgetDetail
    {
        public Guid IdBudgetDetail { get; set; }
        public Guid IdCategory { get; set; }
        public int IdSection { get; set; }
        [Precision(18, 2)]
        public decimal ItemNumber { get; set; }
        public string Description { get; set; } = string.Empty;
        public string ShortDescrition { get; set; } = string.Empty;
        [Precision(18, 2)]
        public decimal MonthlyAmount { get; set; }
        [Precision(18, 2)]
        public decimal AnnualAmount { get; set; }
        public int Frequency { get; set; } = 1;
        public int Type { get; set; } = 1;
        public bool IsHeader { get; set; } = false;

        public Guid IdParent { get; set; }
    }

    public class SectionInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public Guid IdCategory { get; set; }
    }

    public class NewCalculation
    {
        public int Month { get; set; }
        public int Year { get; set; }
        public string BuildingName { get; set; } = "";
        public int TotalApartments { get; set; } = 30;
        public string Template { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public class NewSection
    {
        public string Name { get; set; } = "";
        public string Position { get; set; } = "last";
        public Guid IdCategory { get; set; }
    }

    public class SectionTotal
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public decimal Monthly { get; set; }
        public decimal Annual { get; set; }
    }

    public class RegisterWithInvitationModel
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
        public bool AcceptTerms { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
    }

    public class MenuPermissions
    {
        public Guid IdMenu { get; set; } = Guid.Empty;
        public Guid IdRole { get; set; } = Guid.Empty;
    }

    public class MenuItem
    {
        public Guid IdMenu { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ItemKey { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public string? Url { get; set; }
        public int Order { get; set; }
        public Guid? IdParent { get; set; }
        public List<string> RequiredPermissions { get; set; } = new();
        public List<MenuItem> Children { get; set; } = new();
        public bool IsVisible { get; set; } = true;
        public string? Target { get; set; } // Para menús colapsables
    }

    public class RolePermissions
    {
        public Guid IdRole { get; set; } = Guid.Empty;
        public Guid IdPermission { get; set; } = Guid.Empty;
        public string RoleName { get; set; } = string.Empty;
        public string PermissionKey { get; set; } = string.Empty;
    }

    public class Role
    {
        public Guid IdRole { get; set; } = Guid.Empty;
        public string RoleName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsSystem { get; set; } // Para roles que no se pueden eliminar
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [NotMapped]
        public List<string> Permissions { get; set; } = new();
    }

    public class PermissionGroup
    {
        public string Module { get; set; } = string.Empty;
        public string ModuleDisplayName { get; set; } = string.Empty;
        public List<PermissionDefinition> Permissions { get; set; } = new();
        public string Icon { get; set; } = "fas fa-cog";
    }

    public class PermissionDefinition
    {
        public Guid PermissionId { get; set; } = Guid.Empty;
        public string PermissionKey { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        [NotMapped]
        public bool IsSelected { get; set; }
    }

    public class RoleAssignment
    {
        public Guid IdUser { get; set; } = Guid.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string CurrentRole { get; set; } = string.Empty;
        [NotMapped]
        public List<Role> AvailableRoles { get; set; } = new();
    }

    // Una fila por (usuario, edificio, rol) en UserBuildingAssociation — la tabla real
    // que AuthService.LoginAsync lee para armar el menú y los permisos de sesión. Un
    // usuario puede tener varias filas (un rol por edificio, o varios roles sobre el
    // mismo edificio), a diferencia de RoleAssignment (que asume un solo rol global por
    // usuario, sobre la tabla UserRole — desconectada de la sesión real).
    public class UserBuildingRoleAssignment
    {
        public Guid IdUser { get; set; } = Guid.Empty;
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public Guid IdBuilding { get; set; } = Guid.Empty;
        public string BuildingName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    public class MenuItemDefinition
    {
        public Guid IdMenu { get; set; } = Guid.NewGuid();
        public string ItemKey { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public string? Url { get; set; }
        public int DisplayOrder { get; set; }
        public Guid? IdParent { get; set; }
        public string ParentKey { get; set; } = string.Empty;
        public string? Target { get; set; } // Para collapse ID
        [NotMapped]
        public List<Guid> RequiredPermissions { get; set; } = new();

        public bool IsVisible { get; set; } = true;

        public string? BadgeText { get; set; }

        public string? BadgeColor { get; set; } = "danger";
        [NotMapped]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [NotMapped]
        public DateTime? UpdatedAt { get; set; }

        // Propiedades de navegación
        [NotMapped]
        public List<MenuItemWithRoles> Children { get; set; } = new();
        [NotMapped]
        public MenuItemDefinition? Parent { get; set; }
    }

    public class PermissionSelection
    {
        public Guid IdPermission { get; set; } = Guid.Empty;
        public string PermissionKey { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public string DisplayGroupName { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
        public string ParentPermissionKey { get; set; } = string.Empty;
    }

    public class RolePermissionCheck
    {
        public Guid IdRole { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public bool CanView { get; set; }
        public bool IsExpanded { get; set; }
    }

    public class RoleDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsExpanded { get; set; } = true;
        public int UserCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class MenuItemWithRoles : MenuItemDefinition
    {
        public List<RolePermissionCheck> RolePermissions { get; set; } = new();
        public string? ParentTitle { get; set; }
        public bool IsExpanded { get; set; } = true;
        public int ChildrenCount => Children?.Count ?? 0;
        public bool HasChildren => ChildrenCount > 0;
    }

    public class UsuarioFormModel
    {
        public Guid IdUser { get; set; }
        public bool IsEdit { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "El apellido es obligatorio")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "El email es obligatorio")]
        [EmailAddress(ErrorMessage = "Formato de email inválido")]
        public string Email { get; set; } = string.Empty;

        public string PhoneNumber { get; set; } = string.Empty;

        public Guid? IdRole { get; set; }

        public bool IsActive { get; set; } = true;

        // Solo se usa al crear un usuario nuevo; en edición queda vacío.
        public string Password { get; set; } = string.Empty;
    }

}