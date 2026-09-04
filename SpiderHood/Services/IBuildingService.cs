using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;


namespace SpiderHood.Services
{
    // Interfaces/IBuildingService.cs
    public interface IBuildingService
    {
        Task CreateConfigurationAsync(Models.BuildingConfiguration newconfiguration);
        Task AddContactAsync(Models.Contact newcontact);
        Task UpdateContactAsync(Models.Contact contact);
        Task AddExonerationAsync(Models.Exoneration newexoneartion);
        Task DeleteExonerationAsync(Models.Exoneration newexoneartion);
        Task<List<Models.Building>> GetAllBuildingByOwnerAsync(Guid IdOwner);
        Task<List<Models.Building>> GetAllBuildingsPublicAsync();
        Task<OperationResult> CreateBuildingAsync(Models.Building building, Guid createdByUserId, string createdByRole);
        Task<OperationResult> UpdateBuildingAsync(Models.Building building);
        /*Task<List<Models.BuildingConfiguration>> GetBuildingConfigurationAsync(Guid IdBuilding);
        Task<List<Models.BankAccount>> GetBankAccountsByBuildingAsync(Guid IdBuilding);
        Task<List<Models.Contact>> GetAllContactsAsync(Guid IdBuildingConfiguration);
        Task<List<Models.Exoneration>> GetExonerationsByBuildingAsync(Guid IdBuilding);*/
        Task<BuildingConfiguration> GetConfigurationAsync(Guid IdBuilding);
        BuildingConfiguration CreateDefaultConfigurationAsync(Guid IdBuilding);
        Task UpdateConfigurationAsync(Models.BuildingConfiguration configuration);

        Task<List<Models.OwnerUnitView>> GetOwnersByBuildingAsync(Guid IdBuilding);

        Task<List<Models.RealEstateUnit>> GetUnitsByBuildingAsync(Guid IdBuilding);

        /*  UNITS SECTIONS  */
        Task<List<Models.UnitView>> GetGroupUnitsByTypeAsync(Guid IdBuilding, int type);

        Task AddOwnerUnitAsync(Models.OwnerUnit ownerunit);

        Task AddGroupUnitAsync(Models.GroupUnit newgroupunit);

        Task UpdateOwnerUnitAsync(Models.OwnerUnit ownerunit);

        Task AddOwnerGroupOwnerAsync(Models.OwnerGroupOwner newgroup);

        Task AddUnitAsync(Models.RealEstateUnit newunit);


        Task DeleteUnitAsync(Models.RealEstateUnit unit);

        Task UpdateUnitAsync(Models.RealEstateUnit unit);


        Task<List<Departamento>> ObtenerDepartamentosActivosAsync();
        Task<Departamento> ObtenerDepartamentoPorIdAsync(int id);
        Task<Departamento> CrearDepartamentoAsync(Departamento departamento);
        Task<bool> ActualizarDepartamentoAsync(Departamento departamento);
        Task<bool> EliminarDepartamentoAsync(int id);
        Task<Dictionary<int, decimal>> CalcularPorcentajesAreaAsync();
        Task<decimal> ObtenerAreaTotalAsync();
    }

    public class BuildingService : IBuildingService
    {
        private BDLayout ec { get; set; }
        private ParameterService ParameterService { get; set; } = default!;
        private readonly AuthService _authService;

        public BuildingService(IDbContextFactory<SpiderHoodContext> contextFactory, AuthService authService)
        {
            ec = new BDLayout(contextFactory);
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        private async Task<string> GetPerformedByAsync()
        {
            var user = await _authService.GetCurrentUserAsync();
            return user?.Email ?? "system";
        }

        public async Task<List<Models.Building>> GetAllBuildingByOwnerAsync(Guid IdOwner)
        {
            return await ec.GetAllBuildingByOwnerAsync(IdOwner);
        }

        // Crea el Building + su BuildingConfiguration inicial + la
        // UserBuildingAssociation que vincula a quien lo crea, sin la cual ni
        // Administrador ni SysAdmin sin fila propia verían el edificio recién creado
        // en su lista (SysAdmin global sí lo ve solo, vía
        // GrantSysAdminAccessToAllBuildingsAsync, pero Administrador no). Antes de
        // este método, BuildingPage.razor.cs.SaveBuilding() sólo agregaba el objeto a
        // una lista en memoria -- no persistía nada.
        public async Task<OperationResult> CreateBuildingAsync(Models.Building building, Guid createdByUserId, string createdByRole)
        {
            try
            {
                // Un solo lookup del template, reusado por BuildingConfiguration (Paso 2)
                // y por los Parameter Mixto (Paso 3) -- si building.IsTemplate, este
                // edificio ES el template (o uno más, no hay tope de uno solo), así que
                // no tiene sentido clonarse defaults a sí mismo.
                var template = building.IsTemplate ? null : await ec.GetTemplateBuildingAsync();
                if (template != null)
                    await ApplyTemplateDefaultsAsync(building.Configuration, template);

                await ec.AddNewRecordAsync(building);
                await ec.StampAuditAsync(AuditableEntity.Building, building.IdBuilding, await GetPerformedByAsync(), isCreate: true);

                building.Configuration.IdBuildingConfiguration = Guid.NewGuid();
                building.Configuration.IdBuilding = building.IdBuilding;
                await ec.AddNewRecordAsync(building.Configuration);
                await ec.StampAuditAsync(AuditableEntity.BuildingConfiguration, building.Configuration.IdBuildingConfiguration, await GetPerformedByAsync(), isCreate: true);

                if (template != null)
                {
                    await CloneMixtoParametersAsync(template.IdBuilding, building.IdBuilding);
                    await CloneCategoriesAsync(template.IdBuilding, building.IdBuilding);
                }

                await ec.AcceptInvitationAsync(new UserBuildingAssociation
                {
                    IdUser = createdByUserId,
                    IdBuilding = building.IdBuilding,
                    Role = createdByRole,
                    IsApproved = true,
                    RequestedAt = DateTime.UtcNow
                });

                return OperationResult.Success(building);
            }
            catch (Exception ex)
            {
                return OperationResult.Failure($"No se pudo crear el edificio: {DescribeError(ex)}");
            }
        }

        // Edificio Template (Docs/Design-Defaults-Sistema-Mixto.md, Paso 2): pisa los
        // valores de "config" con los del Edificio Template, si existe alguno. Sólo
        // los campos pedidos como default -- BankAccounts, Contacts,
        // DefaultCategory/WaterReadingDefault y Exonerations quedan tal como venían
        // (vacíos/null para un edificio nuevo), porque no tiene sentido copiar una
        // cuenta bancaria o categoría del template a un edificio real (y Category
        // todavía no se clona -- eso es el Paso 4, así que no hay ninguna Category
        // propia a la que apuntar todavía). Si no hay ningún template marcado,
        // "config" queda como venía (el fallback hardcodeado de
        // CreateDefaultConfigurationAsync que ya arma ShowCreateModal).
        private async Task ApplyTemplateDefaultsAsync(BuildingConfiguration config, Models.Building template)
        {
            var templateConfig = await GetConfigurationAsync(template.IdBuilding);

            config.Currency = templateConfig.Currency;
            config.PaymentMethods = [.. templateConfig.PaymentMethods];
            config.PaymentPeriod = templateConfig.PaymentPeriod;
            config.DueDay = templateConfig.DueDay;
            config.FineAmount = templateConfig.FineAmount;
            config.MinWaterConsumtion = templateConfig.MinWaterConsumtion;
            config.DefaultFixedCharge = templateConfig.DefaultFixedCharge;
            config.LateInterestRate = templateConfig.LateInterestRate;
            config.InvoiceDay = templateConfig.InvoiceDay;
            config.DebtWarningDays = templateConfig.DebtWarningDays;
            config.DebtCriticalDays = templateConfig.DebtCriticalDays;
            config.ReceiptFooterText = templateConfig.ReceiptFooterText;
        }

        // Parameter Sistema/Mixto (Docs/Design-Defaults-Sistema-Mixto.md, Paso 3):
        // clona los hijos Mixto propios del Edificio Template al edificio nuevo --
        // los grupos Sistema no se clonan nunca (son una sola fila global, ver
        // Classes/Parameter.cs), y de los Mixto sólo se clonan los HIJOS que de
        // verdad pertenecen al template (IdBuilding == template.IdBuilding), no su
        // raíz (que ya es global). Quedan marcados IsSystemDefault=true -- vinieron
        // del template, no los agregó el admin de este edificio.
        private async Task CloneMixtoParametersAsync(Guid templateBuildingId, Guid newBuildingId)
        {
            var templateParameters = await ec.GetParametersByBuildingAsync(templateBuildingId);

            var hijosMixtoDelTemplate = templateParameters
                .Where(p => p.IdParent != 0 && p.IdBuilding == templateBuildingId);

            foreach (var hijo in hijosMixtoDelTemplate)
            {
                await ec.AddNewRecordAsync(new Models.Parameter
                {
                    Description = hijo.Description,
                    ShortDescription = hijo.ShortDescription,
                    Value = hijo.Value,
                    Sort = hijo.Sort,
                    IdParent = hijo.IdParent,
                    Estado = hijo.Estado,
                    IdBuilding = newBuildingId,
                    IsSystemDefault = true
                });
            }
        }

        // Category (Docs/Design-Defaults-Sistema-Mixto.md, Paso 4): clona el set
        // completo de Categorías del Edificio Template al edificio nuevo. A
        // diferencia de Parameter, acá TODO se clona por edificio -- no hay nivel
        // Sistema para Category (decisión explícita: nada de fusión/promoción de
        // duplicados a futuro tampoco, ver §6).
        //
        // Category usa Guid como PK (IdCategory) en vez de un IDENTITY int como
        // Parameter, así que clonar no es tan directo como copiar filas: cada clon
        // necesita su propio Guid nuevo, y los hijos (Nivel 1) tienen que apuntar
        // (ParentId) al Guid NUEVO de su padre clonado, no al del template -- por
        // eso se clona en dos pasadas (raíces primero, armando el mapeo
        // template->nuevo, después los hijos usando ese mapeo).
        private async Task CloneCategoriesAsync(Guid templateBuildingId, Guid newBuildingId)
        {
            var templateCategories = await ec.GetCategoriesAsync(templateBuildingId);

            var idMap = new Dictionary<Guid, Guid>();

            foreach (var raiz in templateCategories.Where(c => c.Nivel == 0))
            {
                var nuevoId = Guid.NewGuid();
                idMap[raiz.IdCategory] = nuevoId;

                await ec.AddNewRecordAsync(new Models.Category
                {
                    IdCategory = nuevoId,
                    Description = raiz.Description,
                    ShortDescript = raiz.ShortDescript,
                    Icon = raiz.Icon,
                    Color = raiz.Color,
                    Distribution = raiz.Distribution,
                    ParentId = Guid.Empty,
                    IdBuilding = newBuildingId,
                    Nivel = 0,
                    // Sin esto quedaba en el default 0 de int para toda categoría raíz
                    // clonada -- mismo bug que CategoryPage.AddItem (ver
                    // Database/Scripts/2026-09-04_43_*): view_categories propaga el Sort
                    // de la raíz a sus hijos, así que con todas las raíces clonadas en
                    // Sort = 0, GET_BudgetDetailDefault las colapsaba en una sola sección
                    // en cada edificio nuevo. Se preserva el orden del Template.
                    Sort = raiz.Sort,
                    ShowDetailInReceipt = raiz.ShowDetailInReceipt
                });
            }

            foreach (var hijo in templateCategories.Where(c => c.Nivel != 0))
            {
                // Defensivo: si el template tuviera un hijo cuyo padre no está en el
                // mapeo (dato huérfano preexistente), se lo salta en vez de romper
                // toda la creación del edificio por una fila mal cargada del template.
                if (!idMap.TryGetValue(hijo.ParentId, out var nuevoParentId))
                    continue;

                await ec.AddNewRecordAsync(new Models.Category
                {
                    IdCategory = Guid.NewGuid(),
                    Description = hijo.Description,
                    ShortDescript = hijo.ShortDescript,
                    Icon = hijo.Icon,
                    Color = hijo.Color,
                    Distribution = hijo.Distribution,
                    ParentId = nuevoParentId,
                    IdBuilding = newBuildingId,
                    Nivel = hijo.Nivel,
                    ShowDetailInReceipt = hijo.ShowDetailInReceipt
                });
            }
        }

        public async Task<OperationResult> UpdateBuildingAsync(Models.Building building)
        {
            try
            {
                await ec.UpdateRecordAsync(building);
                await ec.StampAuditAsync(AuditableEntity.Building, building.IdBuilding, await GetPerformedByAsync(), isCreate: false);
                return OperationResult.Success(building);
            }
            catch (Exception ex)
            {
                return OperationResult.Failure($"No se pudo actualizar el edificio: {DescribeError(ex)}");
            }
        }

        // BDLayout envuelve la excepción real (la de SQL Server) en una
        // RepositoryException genérica cuyo .Message no dice nada útil -- ver el mismo
        // patrón en ParameterService.DescribeError.
        private static string DescribeError(Exception ex)
        {
            var innermost = ex;
            while (innermost.InnerException != null)
                innermost = innermost.InnerException;
            return innermost.Message;
        }

        public async Task<List<Models.Building>> GetAllBuildingsPublicAsync()
        {
            return await ec.GetAllBuildingsPublicAsync();
        }

        private async Task<List<Models.BuildingConfiguration>> GetBuildingConfigurationAsync(Guid IdBuilding)
        {
            return await ec.GetBuildingConfigurationAsync(IdBuilding);
        }

        private async Task<List<Models.BankAccount>> GetBankAccountsByBuildingAsync(Guid IdBuilding)
        {
            return await ec.GetBankAccountsByBuildingAsync(IdBuilding);
        }

        private async Task<List<Models.Contact>> GetAllContactsAsync(Guid IdBuildingConfiguration)
        {
            return await ec.GetAllContactsAsync(IdBuildingConfiguration);
        }

        private async Task<List<Models.Exoneration>> GetExonerationsByBuildingAsync(Guid IdBuilding)
        {
            return await ec.GetExonerationsByBuildingAsync(IdBuilding);
        }

        public async Task<List<Models.UnitView>> GetGroupUnitsByTypeAsync(Guid IdBuilding, int type)
        {
            return await ec.GetGroupUnitsByTypeAsync(IdBuilding, type);
        }

        public async Task<BuildingConfiguration> GetConfigurationAsync(Guid IdBuilding)
        {
            if (IdBuilding == Guid.Empty)
            {
                return new BuildingConfiguration();
            }
            var configs = await GetBuildingConfigurationAsync(IdBuilding);

            if (configs != null && configs.Count > 0)
            {
                BuildingConfiguration config = configs[0];

                List<BankAccount> bankAccounts = await GetBankAccountsByBuildingAsync(IdBuilding);

                foreach (var bank in bankAccounts)
                {
                    config.BankAccounts.Add(bank);
                }

                List<Contact> contacts = await GetAllContactsAsync(config.IdBuildingConfiguration);

                config.AdminContact = contacts.FirstOrDefault(c => c.TypeContact == 1) ?? new Contact();
                config.RealEstateCompany = contacts.FirstOrDefault(c => c.TypeContact == 2) ?? new Contact();
                config.MaintenanceCompany = contacts.FirstOrDefault(c => c.TypeContact == 3) ?? new Contact();

                List<Exoneration> exonerations = await GetExonerationsByBuildingAsync(IdBuilding);

                foreach (var item in exonerations)
                {
                    config.Exonerations.Add(item);
                }

                return config;
            }
            else
            {
                return CreateDefaultConfigurationAsync(IdBuilding);
            }
        }

        public BuildingConfiguration CreateDefaultConfigurationAsync(Guid IdBuilding)
        {
            return new BuildingConfiguration
            {
                Currency = "PEN",
                BankAccounts = new List<BankAccount>
            {
                new BankAccount { BankName = "Banco A", AccountNumber = "123456789" }
            },
                PaymentMethods = new List<string> { "Transferencia Bancaria", "Pago en Efectivo" },
                PaymentPeriod = 1,
                DueDay = 5,
                FineAmount = 10.00m,
                LateInterestRate = 2.00m,
                InvoiceDay = 1,
                AdminContact = new Contact
                {
                    Name = "Pepito Perez",
                    Phone = "987654321",
                    Email = "algo@algo.com",
                    Address = "Av. Siempre Viva 123"
                },
                RealEstateCompany = new Contact
                {
                    Name = "Mattins",
                    Phone = "123456789",
                    Email = "algo@algo.com",
                    Address = "Av. Los Olivos 456"
                },
                MaintenanceCompany = new Contact
                {
                    Name = "Mantenciones S.A.",
                    OfficePhone = "961651893",
                    MobilePhone = "961893518",
                    Email = "algo@algo.com",
                    Address = "Dirección de la Empresa de Mantenimiento 789"
                }
            };
        }

        public async Task CreateConfigurationAsync(Models.BuildingConfiguration newconfiguration)
        {
            try
            {
                await ec.AddNewRecordAsync(newconfiguration);
                await ec.StampAuditAsync(AuditableEntity.BuildingConfiguration, newconfiguration.IdBuildingConfiguration, await GetPerformedByAsync(), isCreate: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al crear el contacto: {ex.Message}");
            }
        }

        public async Task UpdateConfigurationAsync(Models.BuildingConfiguration configuration)
        {
            try
            {
                await ec.UpdateRecordAsync(configuration);
                await ec.StampAuditAsync(AuditableEntity.BuildingConfiguration, configuration.IdBuildingConfiguration, await GetPerformedByAsync(), isCreate: false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al crear el contacto: {ex.Message}");
            }
        }

        public async Task AddContactAsync(Models.Contact newcontact)
        {
            try
            {
                await ec.AddNewRecordAsync(newcontact);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al crear el contacto: {ex.Message}");
            }
        }

        public async Task UpdateContactAsync(Models.Contact contact)
        {
            try
            {
                await ec.UpdateRecordAsync(contact);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al actualizar el contacto: {ex.Message}");
            }

        }

        public async Task AddExonerationAsync(Models.Exoneration newexoneartion)
        {
            try
            {
                await ec.AddNewRecordAsync(newexoneartion);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al crear el contacto: {ex.Message}");
            }
        }

        public async Task DeleteExonerationAsync(Models.Exoneration exoneration)
        {
            try
            {
                await ec.DeleteRecordAsync(exoneration);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al crear el contacto: {ex.Message}");
            }
        }


        public async Task UpdateUnitAsync(Models.RealEstateUnit unit)
        {
            try
            {
                await ec.UpdateRecordAsync(unit);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al crear el contacto: {ex.Message}");
            }
        }

        public async Task AddUnitAsync(Models.RealEstateUnit newunit)
        {
            try
            {
                await ec.AddNewRecordAsync(newunit);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al crear el contacto: {ex.Message}");
            }
        }

        public async Task<List<Models.OwnerUnitView>> GetOwnersByBuildingAsync(Guid IdBuilding)
        {
            return await ec.GetOwnersByBuildingAsync(IdBuilding);
        }

        public async Task<List<Models.RealEstateUnit>> GetUnitsByBuildingAsync(Guid IdBuilding)
        {

            try
            {
                return await ec.GetUnitsByBuildingAsync(IdBuilding);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener las unidades por edificio : {ex.Message}");
                throw new Exception(ex.Message);
            }
        }

        public async Task AddOwnerGroupOwnerAsync(Models.OwnerGroupOwner newgroup)
        {
            try
            {
                await ec.AddNewRecordAsync(newgroup);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al crear el contacto: {ex.Message}");
            }
        }


        public async Task AddGroupUnitAsync(Models.GroupUnit newgroupunit)
        {
            try
            {
                await ec.AddNewRecordAsync(newgroupunit);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al crear el contacto: {ex.Message}");
            }
        }

        public async Task AddOwnerUnitAsync(Models.OwnerUnit ownerunit)
        {
            try
            {
                await ec.AddNewRecordAsync(ownerunit);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al crear el contacto: {ex.Message}");
            }
        }


        public async Task UpdateOwnerUnitAsync(Models.OwnerUnit ownerunit)
        {
            try
            {
                await ec.UpdateRecordAsync(ownerunit);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al crear el contacto: {ex.Message}");
            }
        }

        public async Task DeleteUnitAsync(Models.RealEstateUnit unit)
        {
            try
            {
                await ec.DeleteRecordAsync(unit);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al crear el contacto: {ex.Message}");
            }
        }


        public async Task<List<Departamento>> ObtenerDepartamentosActivosAsync()
        {
            return new List<Departamento>(); // await ParameterService.ec.GetDptos();
            /*return await _context.Departamentos
                .Where(d => d.Activo)
                .OrderBy(d => d.Nombre)
                .ToListAsync();*/
        }

        public async Task<Departamento> ObtenerDepartamentoPorIdAsync(int id)
        {
            // The original code was returning a List<Departamento> from getDptos(), but the method expects a single Departamento.
            // To fix CS0029, fetch the list and return the Departamento with the matching id.
            var departamentos = new List<Departamento>(); // await ParameterService.ec.GetDptos();
            return departamentos.FirstOrDefault(d => d.Id == id)!;
        }

        public async Task<Departamento> CrearDepartamentoAsync(Departamento departamento)
        {
            //_context.Departamentos.Add(departamento);
            return departamento;
        }

        public async Task<bool> ActualizarDepartamentoAsync(Departamento departamento)
        {
            /*var deptoExistente = await _context.Departamentos.FindAsync(departamento.Id);
            if (deptoExistente == null)
                return false;

            _context.Entry(deptoExistente).CurrentValues.SetValues(departamento);*/
            return true;
        }

        public async Task<bool> EliminarDepartamentoAsync(int id)
        {
            /*var departamento = await _context.Departamentos.FindAsync(id);
            if (departamento == null)
                return false;

            // Soft delete
            departamento.Activo = false;*/
            return true;
        }

        public async Task<Dictionary<int, decimal>> CalcularPorcentajesAreaAsync()
        {
            var departamentos = await ObtenerDepartamentosActivosAsync();
            var totalArea = departamentos.Sum(d => d.AreaM2);

            var porcentajes = new Dictionary<int, decimal>();

            foreach (var depto in departamentos)
            {
                var porcentaje = totalArea > 0 ? (depto.AreaM2 / totalArea) * 100 : 0;
                porcentajes.Add(depto.Id, Math.Round(porcentaje, 2));
            }

            return porcentajes;
        }

        public async Task<decimal> ObtenerAreaTotalAsync()
        {
            var departamentos = await ObtenerDepartamentosActivosAsync();
            return departamentos.Sum(d => d.AreaM2);
        }
    }
}