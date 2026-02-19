using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;

namespace SpiderHood.Models
{
    public class Usuario
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
        public string Email { get; set; } = "";
        public string Telefono { get; set; } = "";
        public string Rol { get; set; } = "";
        public string Departamento { get; set; } = "";
        public string Estado { get; set; } = "Activo";
        public DateTime FechaIngreso { get; set; } = DateTime.Now;
        public string PasswordHash { get; set; } = "";

        public Usuario Clone()
        {
            return (Usuario)this.MemberwiseClone();
        }
    }

    public class Rol
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public int UsuariosAsignados { get; set; }
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
    }

    public class Permiso
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public bool Activo { get; set; }
        public string Categoria { get; set; } = "";
    }

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
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public string Email { get; set; } = "";
        public BuildingModel Building { get; set; } = new();
        public string InvitedBy { get; set; } = "";
        public string Role { get; set; } = "";
        public int ApartmentNumber { get; set; }
        public bool RequiresApproval { get; set; }
        public string AdminMessage { get; set; } = "";
        public DateTime ExpirationDate { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Accepted, Rejected, Expired
    }

    public class BuildingModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Address { get; set; } = "";
        public string City { get; set; } = "";
        public int TotalApartments { get; set; }
        public int Floors { get; set; }
        public string BuildingType { get; set; } = "";
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }

    public class UserModel
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Email { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public string Role { get; set; } = "";
        public int BuildingId { get; set; }
        public int ApartmentNumber { get; set; }
        public string Status { get; set; } = "Pending"; // Active, Pending, Inactive
        public List<BuildingModel> Buildings { get; set; } = [];
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }

    public class AuthResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public UserModel? User { get; set; }
        public string Token { get; set; } = "";
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
        public string BankName { get; set; } = "";
        public int AccountType { get; set; }  // Ahorros, Corriente, etc.
        [Precision(18,2)]
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
        public Guid IdCategory{ get; set; }
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

    // Models/ResultadoPaginado para paginación
    public class ResultadoPaginado<T>
    {
        public List<T> Items { get; set; } = [];
        public int PaginaActual { get; set; }
        public int TotalPaginas { get; set; }
        public int TotalItems { get; set; }
        public int TamanoPagina { get; set; }
        public bool TienePaginaAnterior => PaginaActual > 1;
        public bool TienePaginaSiguiente => PaginaActual < TotalPaginas;

        public static ResultadoPaginado<T> Crear(List<T> items, int pagina, int tamanoPagina, int totalItems)
        {
            return new ResultadoPaginado<T>
            {
                Items = items,
                PaginaActual = pagina,
                TamanoPagina = tamanoPagina,
                TotalItems = totalItems,
                TotalPaginas = (int)Math.Ceiling(totalItems / (double)tamanoPagina)
            };
        }
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
        public DateTime BudgetDate { get; set; }
        [Precision(18, 2)]
        public decimal Amount { get; set; }
        [Precision(18, 2)]
        public decimal AnnualAmount { get; set; }
        public string BudgetType { get; set; } = string.Empty;
        public Guid IdBuilding { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public Guid IdPeriod { get; set; }
        public DateTime CreatedOn { get; set; }
        [NotMapped]
        public int Month { get { return BudgetDate.Month; } }
        [NotMapped]
        public int Year { get { return BudgetDate.Year; } }
        public BudgetStatus Status  { get; set; }
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

    public class Presupuesto
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = "";
        public string Mes { get; set; } = ""; // "Enero 2025"
        public int Month { get; set; }
        public int Year { get; set; }
        public string BudgetName { get; set; } = "";
        public DateTime CreatedOn { get; set; } = DateTime.Now;
        public int Status { get; set; } = 1;//"Activo"; // Activo, Cerrado, Pendiente
        [Precision(18, 2)]
        public decimal Amount { get; set; }
        public string CreatedBy { get; set; } = "";

        // Navigation properties
        [NotMapped]
        public List<PresupuestoDetalle> Detalles { get; set; } = [];
        [NotMapped]
        public List<PresupuestoCategoria> Categorias { get; set; } = [];
    }

    public class PresupuestoDetalle
    {
        public int Id { get; set; }
        public int PresupuestoId { get; set; }
        public int CategoriaId { get; set; }
        public string Descripcion { get; set; } = "";
        [Precision(18, 2)]
        public decimal Monto { get; set; }
        public string Notas { get; set; } = "";

        // Navigation properties
        [NotMapped]
        public Presupuesto? Presupuesto { get; set; }
        [NotMapped]
        public Categoria? Categoria { get; set; }
    }

    public class Categoria
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public string Tipo { get; set; } = "Gasto"; // Gasto, Ingreso
        public bool Activo { get; set; } = true;
        public int Orden { get; set; } = 0;
        public string Color { get; set; } = "#3498db";

        // Navigation properties
        [NotMapped]
        public List<PresupuestoDetalle> Detalles { get; set; } = [];
    }

    public class PresupuestoCategoria
    {
        public Guid Id { get; set; }
        public Guid PresupuestoId { get; set; }
        public Guid CategoriaId { get; set; }
        [Precision(18, 2)]
        public decimal MontoAsignado { get; set; }
        [Precision(18, 2)]
        public decimal MontoEjecutado { get; set; }

        // Navigation properties
        [NotMapped]
        public Presupuesto? Presupuesto { get; set; }
        [NotMapped]
        public Categoria? Categoria { get; set; }
 
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

}





/*

namespace SpiderHood.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(SpiderHoodContext context)
        {
            // Verificar si ya hay datos
            if (context.Categorias.Any() || context.Presupuestos.Any())
            {
                return; // DB ya tiene datos
            }

            // Crear categorías por defecto si no existen
            if (!context.Categorias.Any())
            {
                var categorias = new List<Categoria>
                {
                    new Categoria { Codigo = "SERV", Nombre = "Servicios Básicos",
                        Descripcion = "Agua, luz, gas, internet", Tipo = "Gasto",
                        Color = "#3498db", Orden = 1, Activo = true },
                    new Categoria { Codigo = "MANT", Nombre = "Mantenimiento",
                        Descripcion = "Reparaciones y mantenimiento general", Tipo = "Gasto",
                        Color = "#e74c3c", Orden = 2, Activo = true },
                    new Categoria { Codigo = "ADMIN", Nombre = "Administración",
                        Descripcion = "Honorarios administrativos", Tipo = "Gasto",
                        Color = "#f39c12", Orden = 3, Activo = true },
                    new Categoria { Codigo = "DIV", Nombre = "Diversos",
                        Descripcion = "Gastos varios y extraordinarios", Tipo = "Gasto",
                        Color = "#9b59b6", Orden = 4, Activo = true },
                    new Categoria { Codigo = "RESV", Nombre = "Reservas",
                        Descripcion = "Fondo de reserva y contingencia", Tipo = "Gasto",
                        Color = "#1abc9c", Orden = 5, Activo = true }
                };

                await context.Categorias.AddRangeAsync(categorias);
                await context.SaveChangesAsync();
            }

            // Crear un presupuesto de ejemplo
            if (!context.Presupuestos.Any())
            {
                var categorias = await context.Categorias.ToListAsync();

                var presupuesto = new Presupuesto
                {
                    Codigo = "PRES-2024-001",
                    Mes = "Enero 2024",
                    BudgetName = "Presupuesto de ejemplo para el mes de enero",
                    Status = 1,//"Activo",
                    CreatedBy = "Sistema",
                    CreatedOn = DateTime.Now
                };

                await context.Presupuestos.AddAsync(presupuesto);
                await context.SaveChangesAsync();

                // Crear detalles de ejemplo
                var detalles = new List<PresupuestoDetalle>
                {
                    new PresupuestoDetalle { PresupuestoId = presupuesto.Id,
                        CategoriaId = categorias[0].Id, Descripcion = "Agua y luz",
                        Monto = 2500.00m },
                    new PresupuestoDetalle { PresupuestoId = presupuesto.Id,
                        CategoriaId = categorias[1].Id, Descripcion = "Mantenimiento áreas comunes",
                        Monto = 1800.00m },
                    new PresupuestoDetalle { PresupuestoId = presupuesto.Id,
                        CategoriaId = categorias[2].Id, Descripcion = "Honorarios administrador",
                        Monto = 1200.00m }
                };

                await context.PresupuestoDetalles.AddRangeAsync(detalles);
                await context.SaveChangesAsync();

                // Calcular y actualizar total
                presupuesto.Amount = detalles.Sum(d => d.Monto);
                context.Presupuestos.Update(presupuesto);
                await context.SaveChangesAsync();

                // Crear relaciones presupuesto-categoría
                var presupuestoCategorias = categorias.Select(c => new PresupuestoCategoria
                {
                    PresupuestoId = presupuesto.Id,
                    CategoriaId = c.Id,
                    MontoAsignado = detalles.Where(d => d.CategoriaId == c.Id).Sum(d => d.Monto),
                    MontoEjecutado = 0
                }).ToList();

                await context.PresupuestoCategorias.AddRangeAsync(presupuestoCategorias);
                await context.SaveChangesAsync();
            }
        }
    }
}
*/