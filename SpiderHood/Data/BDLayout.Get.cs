using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SpiderHood.Models;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;

namespace SpiderHood.Data
{
    public partial class BDLayout
    {
        #region Get Operations

        public async Task<List<Models.MenuPermissions>> GetAllMenuPermissionsAsync(CancellationToken cancellationToken = default)
        {
            List<MenuPermissions> list = [];
            try
            {
                using var connection = new SqlConnection(_connectionString);
                using var command = new SqlCommand(StoredProcedures.GET_AllMenuPemission, connection);

                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = 30;

                // Usar SqlDataAdapter en lugar de DataReader
                using var adapter = new SqlDataAdapter(command);
                var dataTable = new DataTable();

                // Fill no es async pero no se cuelga con múltiples filas
                adapter.Fill(dataTable);

                foreach (DataRow row in dataTable.Rows)
                {
                    list.Add(
                        new MenuPermissions
                        {
                            IdMenu = row["IdMenu"] is Guid g1 ? g1 : Guid.Parse(row["IdMenu"].ToString()!),
                            IdRole = row["IdRole"] is Guid g2 ? g2 : Guid.Parse(row["IdRole"].ToString()!)
                        });
                }

                return list;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return [];
            }
            /*return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.MenuPermissions>(
                    StoredProcedures.GET_AllMenuPemission);
            }, "GetAllBuildingsConfig", cancellationToken);*/
        }

        public async Task<List<Models.BuildingConfiguration>> GetAllBuildingsConfigAsync(Guid IdUser, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.BuildingConfiguration>(
                    StoredProcedures.GET_AllBuildingsConfig,
                    IdUser);
            }, "GetAllBuildingsConfig", cancellationToken);
        }

        public async Task<List<Models.UserBuildingAssociation>> GetUserBuildingAssociationAsync(Guid IdUser, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.UserBuildingAssociation>(
                    StoredProcedures.GET_UserBuildingAssociation,
                    IdUser);
            }, "GetUserBuildingAssociation", cancellationToken);
        }

        public async Task<List<Models.UserBuildingRoleAssignment>> GetAllUserBuildingRolesAsync(CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.UserBuildingRoleAssignment>(
                    StoredProcedures.GET_AllUserBuildingRoles);
            }, "GetAllUserBuildingRoles", cancellationToken);
        }
        public async Task<List<Models.UserModel>> GetUsersByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.UserModel>(
                    StoredProcedures.GET_UsersByEmail,
                    email);
            }, "GetUsersByEmail", cancellationToken);
        }

        // Login social (Google/Microsoft/Facebook/Apple) -- null si nadie vinculó
        // todavía este (proveedor, id de usuario en ese proveedor).
        public async Task<Models.UserModel?> GetUserByExternalLoginAsync(string provider, string providerId, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQuerySingleAsync<Models.UserModel>(
                    StoredProcedures.GET_UserByExternalLogin,
                    provider,
                    providerId);
            }, "GetUserByExternalLogin", cancellationToken);
        }

        public async Task<List<BudgetSumCategory>> GetBudgetSumAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<BudgetSumCategory>(
                    StoredProcedures.GET_BudgetDetails_Sum,
                    idBuilding);
            }, "GetBudgetSum", cancellationToken);
        }

        public async Task<List<BudgetHeader>> GetBudgetsAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<BudgetHeader>(
                    StoredProcedures.GET_Budgets,
                    idBuilding);
            }, "GetBudgets", cancellationToken);
        }

        public async Task<List<ViewExpense>> GetPendingConciliationExpensesAsync(Guid idBuilding, DateTime from, DateTime to, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<ViewExpense>(
                    StoredProcedures.GET_PendingConciliationExpenses,
                    idBuilding, from, to);
            }, "GetPendingConciliationExpenses", cancellationToken);
        }

        public async Task<List<OwnerUnitView>> GetOwnersByBuildingAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<OwnerUnitView>(
                    StoredProcedures.GET_OwnerByBuilding,
                    idBuilding);
            }, "GetOwnersByBuilding", cancellationToken);
        }

        public async Task<List<ViewBudgetDetail>> GetBudgetDetailDefaultAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<ViewBudgetDetail>(
                    StoredProcedures.GET_BudgetDetailDefault,
                    idBuilding);
            }, "GetBudgetDetailDefault", cancellationToken);
        }

        // Items reales que tuvieron las categorías hijas de @idParentCategory la última vez
        // que aparecieron en un presupuesto real de este edificio -- ver
        // Database/Scripts/2026-09-15_107_GET_LastBudgetItemsByParentCategory.sql. A
        // diferencia de GetBudgetDetailDefaultAsync (la Plantilla, siempre en S/0.00), acá
        // vienen los montos reales que se usaron antes.
        public async Task<List<ViewBudgetDetail>> GetLastBudgetItemsByParentCategoryAsync(Guid idBuilding, Guid idParentCategory, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<ViewBudgetDetail>(
                    StoredProcedures.GET_LastBudgetItemsByParentCategory,
                    idBuilding, idParentCategory);
            }, "GetLastBudgetItemsByParentCategory", cancellationToken);
        }

        public async Task<List<TransactionBankDetail>> GetBankTransactionsNoConciliedAsync(Guid idBuilding, DateTime star, DateTime end, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<TransactionBankDetail>(
                    StoredProcedures.GET_BankTransactionsNoConcilied,
                    idBuilding, star, end);
            }, "GetBankTransactionsNoConcilied", cancellationToken);
        }

        // Trae UNA transacción bancaria completa por Id -- usado por el botón "Crear
        // Gasto" inline de /expense (fila "virtual" de un egreso sin conciliar, ver
        // GET_ExpensesByBuilding), que sólo tiene el IdStatementDetail a mano y necesita
        // el TransactionBankDetail completo para abrir CreateExpenseFromTransactionModal
        // (el mismo formulario que usa Conciliación).
        public async Task<TransactionBankDetail?> GetTransactionByIdAsync(Guid idStatementDetail, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQuerySingleAsync<TransactionBankDetail>(
                    StoredProcedures.GET_TransactionBankDetailById,
                    idStatementDetail);
            }, "GetTransactionById", cancellationToken);
        }

        // Solo para migración de datos históricos (IMigrationImportService,
        // ImportarCuotasYPagosAsync) -- busca el IdStatementDetail del movimiento
        // bancario original por la referencia externa que trajo la plantilla de Estado
        // de Cuenta (columna 'Referencia Original'), no por SequenceNumber (ese lo
        // asigna SpiderHood al cargar y se desfasa si se descarta alguna fila del
        // archivo original). Ningún flujo de uso diario llama este método.
        //
        // Devuelve solo el Guid (no un TransactionBankDetail completo) a propósito --
        // ExecuteQueryListAsync<T> mapea contra dbContext.Set<T>(), que exige que el
        // SELECT devuelva TODAS las columnas que EF mapeó para esa entidad (incluidas
        // las que llegan por JOIN a AccountStatementHeader, como IdBankAccount, que no
        // vive en esta tabla). Pedir solo el Guid vía SqlQueryRaw evita depender de esa
        // lista completa de columnas.
        public async Task<Guid?> GetTransactionByOriginalReferenceAsync(Guid idBankAccount, string originalReference, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var dbContext = await RentContextAsync(cancellationToken);
                try
                {
                    var sql = $"EXEC {StoredProcedures.GET_TransactionBankDetail_ByOriginalReference} @p0, @p1";
                    var resultados = await dbContext.Database
                        .SqlQueryRaw<Guid>(sql,
                            new SqlParameter("@p0", idBankAccount),
                            new SqlParameter("@p1", originalReference))
                        .ToListAsync(cancellationToken);
                    return resultados.Count > 0 ? resultados[0] : (Guid?)null;
                }
                finally
                {
                    ReturnContext(dbContext);
                }
            }, "GetTransactionByOriginalReference", cancellationToken);
        }

        public async Task<List<Period>> GetPeriodsByBuildingAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Period>(
                    StoredProcedures.GET_PeriodsByBuilding,
                    idBuilding);
            }, "GetPeriodsByBuilding", cancellationToken);
        }

        public async Task<List<Contact>> GetAllContactsAsync(Guid idBuildingConfiguration, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Contact>(
                    StoredProcedures.GET_AllContacts,
                    idBuildingConfiguration);
            }, "GetAllContacts", cancellationToken);
        }

        public async Task<List<BuildingConfiguration>> GetBuildingConfigurationAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<BuildingConfiguration>(
                    StoredProcedures.GET_BuildingConfiguration,
                    idBuilding);
            }, "GetBuildingConfiguration", cancellationToken);
        }

        public async Task<List<Building>> GetAllBuildingByOwnerAsync(Guid idOwner, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Building>(
                    StoredProcedures.GET_AllBuildings,
                    idOwner);
            }, "GetAllBuildingByOwner", cancellationToken);
        }

        // A diferencia de GET_AllBuildings (que pese al nombre filtra por @IdOwner), este
        // lista todos los edificios activos sin importar dueño -- lo necesita el registro
        // público (/register) y "solicitar acceso a otro edificio" (/building-request),
        // donde todavía no hay ningún vínculo usuario-edificio del cual partir.
        public async Task<List<Building>> GetAllBuildingsPublicAsync(CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Building>(
                    StoredProcedures.GET_AllBuildingsPublic);
            }, "GetAllBuildingsPublic", cancellationToken);
        }

        // Edificio Template (Docs/Design-Defaults-Sistema-Mixto.md, Paso 2) -- null si
        // todavía no se marcó ninguno (instalación nueva, o antes de que el SysAdmin
        // configure uno).
        public async Task<Building?> GetTemplateBuildingAsync(CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var results = await ExecuteQueryListAsync<Building>(
                    StoredProcedures.GET_TemplateBuilding);
                return results.FirstOrDefault();
            }, "GetTemplateBuilding", cancellationToken);
        }

        public async Task<List<RealEstateUnit>> GetUnitsByBuildingAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            var units = new List<RealEstateUnit>();

            try
            {
                using var connection = new SqlConnection(_connectionString);
                using var command = new SqlCommand(StoredProcedures.GET_UnitsByBuilding, connection);

                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = 30;
                command.Parameters.AddWithValue("@idBuilding", idBuilding);

                // Usar SqlDataAdapter en lugar de DataReader
                using var adapter = new SqlDataAdapter(command);
                var dataTable = new DataTable();

                // Fill no es async pero no se cuelga con múltiples filas
                adapter.Fill(dataTable);

                foreach (DataRow row in dataTable.Rows)
                {
                    units.Add(new RealEstateUnit
                    {
                        IdUnit = Guid.Parse(row["IdUnit"].ToString()!),
                        UnitNumber = row["UnitNumber"].ToString()!,
                        Area = Convert.ToDecimal(row["Area"]),
                        TypeGroupUnit = (GroupUnitType)Convert.ToInt32(row["TypeGroupUnit"]),
                        IdGroupOwner = Guid.Parse(row["IdGroupOwner"].ToString()!),
                        GroupName = row["GroupName"].ToString()!,
                        AreaTotal = Convert.ToDecimal(row["AreaTotal"]),
                        TypeOwner = (OwnerType)Convert.ToUInt32(row["TypeOwner"]),
                        Names = row["Names"].ToString()!,
                        Surname = row["Surname"].ToString()!,        // ✔ FIX
                        IdBuilding = Guid.Parse(row["IdBuilding"].ToString()!),
                        TypeUnit = Convert.ToInt32(row["TypeUnit"].ToString()!),
                        IdOwner = Guid.Parse(row["IdOwner"].ToString()!),
                        Number = Convert.ToInt32(row["Number"]),
                        IsAvailable = Convert.ToBoolean(row["IsAvailable"]),   // ✔ FIX
                        Building = row["Building"].ToString()!
                    });
                }

                // Campos propios de la unidad (Docs/Design-Defaults-Sistema-Mixto.md no
                // aplica acá -- ver comentario de cabecera en
                // 2026-09-04_49_Unit_ExtraFields.sql): se traen con un proc aparte y se
                // mergean acá por IdUnit, en vez de tocar el SELECT/JOIN de
                // GET_UnitsByBuilding (desconocido, no confirmado por sp_helptext).
                MergeUnitExtraFields(units, idBuilding);

                return units;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                throw;
            }
        }

        private void MergeUnitExtraFields(List<RealEstateUnit> units, Guid idBuilding)
        {
            if (units.Count == 0) return;

            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand(StoredProcedures.GET_UnitExtraFieldsByBuilding, connection)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = 30,
            };
            command.Parameters.AddWithValue("@IdBuilding", idBuilding);

            using var adapter = new SqlDataAdapter(command);
            var dataTable = new DataTable();
            adapter.Fill(dataTable);

            // Lookup (no ToDictionary) porque GET_UnitsByBuilding puede traer más de un
            // RealEstateUnit con el mismo IdUnit -- una fila por cada Owner/GroupOwner
            // asociado a esa unidad -- y hay que mergear los campos nuevos en TODAS las
            // instancias que compartan ese IdUnit, no sólo en la última.
            var byId = units.ToLookup(u => u.IdUnit);
            foreach (DataRow row in dataTable.Rows)
            {
                var idUnit = Guid.Parse(row["IdUnit"].ToString()!);
                var floor = row["Floor"] is DBNull ? (int?)null : Convert.ToInt32(row["Floor"]);
                var tower = row["Tower"] is DBNull ? null : row["Tower"].ToString();
                var locationCode = row["LocationCode"] is DBNull ? null : row["LocationCode"].ToString();
                var bedrooms = row["Bedrooms"] is DBNull ? (int?)null : Convert.ToInt32(row["Bedrooms"]);
                var bathrooms = row["Bathrooms"] is DBNull ? (int?)null : Convert.ToInt32(row["Bathrooms"]);
                var builtArea = row["BuiltArea"] is DBNull ? (decimal?)null : Convert.ToDecimal(row["BuiltArea"]);
                var isCovered = row["IsCovered"] is DBNull ? (bool?)null : Convert.ToBoolean(row["IsCovered"]);
                var isForDisabled = row["IsForDisabled"] is DBNull ? (bool?)null : Convert.ToBoolean(row["IsForDisabled"]);
                var vehicleType = row["VehicleType"] is DBNull ? null : row["VehicleType"].ToString();
                var height = row["Height"] is DBNull ? (decimal?)null : Convert.ToDecimal(row["Height"]);
                var hasVentilation = row["HasVentilation"] is DBNull ? (bool?)null : Convert.ToBoolean(row["HasVentilation"]);
                var hasElectricity = row["HasElectricity"] is DBNull ? (bool?)null : Convert.ToBoolean(row["HasElectricity"]);
                var notes = row["Notes"] is DBNull ? null : row["Notes"].ToString();
                var status = row["Status"] is DBNull ? null : row["Status"].ToString();
                var orientation = row["Orientation"] is DBNull ? null : row["Orientation"].ToString();
                var hasBalcony = row["HasBalcony"] is DBNull ? (bool?)null : Convert.ToBoolean(row["HasBalcony"]);
                var hasParking = row["HasParking"] is DBNull ? (bool?)null : Convert.ToBoolean(row["HasParking"]);
                var hasStorage = row["HasStorage"] is DBNull ? (bool?)null : Convert.ToBoolean(row["HasStorage"]);
                var hasAirConditioning = row["HasAirConditioning"] is DBNull ? (bool?)null : Convert.ToBoolean(row["HasAirConditioning"]);
                var isFurnished = row["IsFurnished"] is DBNull ? (bool?)null : Convert.ToBoolean(row["IsFurnished"]);
                var plateNumber = row["PlateNumber"] is DBNull ? null : row["PlateNumber"].ToString();
                var hasElectricCharging = row["HasElectricCharging"] is DBNull ? (bool?)null : Convert.ToBoolean(row["HasElectricCharging"]);
                var hasWater = row["HasWater"] is DBNull ? (bool?)null : Convert.ToBoolean(row["HasWater"]);
                var hasSecurity = row["HasSecurity"] is DBNull ? (bool?)null : Convert.ToBoolean(row["HasSecurity"]);
                var estimatedValue = row["EstimatedValue"] is DBNull ? (decimal?)null : Convert.ToDecimal(row["EstimatedValue"]);
                var lastRenovationDate = row["LastRenovationDate"] is DBNull ? (DateTime?)null : Convert.ToDateTime(row["LastRenovationDate"]);
                var restrictions = row["Restrictions"] is DBNull ? null : row["Restrictions"].ToString();
                var hasElevatorAccess = row["HasElevatorAccess"] is DBNull ? (bool?)null : Convert.ToBoolean(row["HasElevatorAccess"]);

                foreach (var unit in byId[idUnit])
                {
                    unit.Floor = floor;
                    unit.Tower = tower;
                    unit.LocationCode = locationCode;
                    unit.Bedrooms = bedrooms;
                    unit.Bathrooms = bathrooms;
                    unit.BuiltArea = builtArea;
                    unit.IsCovered = isCovered;
                    unit.IsForDisabled = isForDisabled;
                    unit.VehicleType = vehicleType;
                    unit.Height = height;
                    unit.HasVentilation = hasVentilation;
                    unit.HasElectricity = hasElectricity;
                    unit.Notes = notes;
                    unit.Status = status;
                    unit.Orientation = orientation;
                    unit.HasBalcony = hasBalcony;
                    unit.HasParking = hasParking;
                    unit.HasStorage = hasStorage;
                    unit.HasAirConditioning = hasAirConditioning;
                    unit.IsFurnished = isFurnished;
                    unit.PlateNumber = plateNumber;
                    unit.HasElectricCharging = hasElectricCharging;
                    unit.HasWater = hasWater;
                    unit.HasSecurity = hasSecurity;
                    unit.EstimatedValue = estimatedValue;
                    unit.LastRenovationDate = lastRenovationDate;
                    unit.Restrictions = restrictions;
                    unit.HasElevatorAccess = hasElevatorAccess;
                }
            }
        }

        public async Task<List<MenuItem>> GetFullMenuAsync(Guid IdRole, CancellationToken cancellationToken = default)
        {
            var menus = new Dictionary<Guid, MenuItem>();

            try
            {
                using var connection = new SqlConnection(_connectionString);
                using var command = new SqlCommand("GET_FullMenu", connection)

                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = 30,
                };

                command.Parameters.Add(new SqlParameter("@IdRole", IdRole));

                await connection.OpenAsync(cancellationToken);

                using var reader = await command.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    Guid Idmenu = Guid.Parse(reader["IdMenu"].ToString()!);

                    if (!menus.ContainsKey(Idmenu))
                    {
                        menus[Idmenu] = new MenuItem
                        {
                            IdMenu = Idmenu,
                            IdParent = reader["IdParent"] != DBNull.Value ? Guid.Parse(reader["IdParent"].ToString()!) : Guid.Empty,
                            ItemKey = reader.GetString(reader.GetOrdinal("ItemKey")),
                            Title = reader.GetString(reader.GetOrdinal("Title")),
                            Icon = reader["Icon"]?.ToString(),
                            Url = reader["Url"]?.ToString(),
                            Target = reader["Target"]?.ToString(),
                            Order = reader.GetInt32(reader.GetOrdinal("DisplayOrder")),
                            RequiredPermissions = new List<string>(),
                            Children = new List<MenuItem>()
                        };
                    }

                    // Agregar permiso si existe
                    /*if (reader["PermissionKey"] != DBNull.Value)
                    {
                        menus[Idmenu].RequiredPermissions!
                            .Add(reader.GetString(reader.GetOrdinal("PermissionKey")));
                    }*/
                }

                // Convertir en lista jerárquica
                var menuList = menus.Values.ToList();

                // Asignar hijos
                var lookup = menuList.ToDictionary(m => m.IdMenu);

                foreach (var item in menuList)
                {
                    if (item.IdParent.HasValue && lookup.ContainsKey(item.IdParent.Value))
                    {
                        lookup[item.IdParent.Value].Children.Add(item);
                    }
                }

                // Solo elementos raíz
                var finalMenu = menuList
                    .Where(m => m.IdParent == Guid.Empty)
                    .OrderBy(m => m.Order)
                    .ToList();

                return finalMenu;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                throw;
            }
        }

        public async Task<List<RolePermissions>> GetPermissionsForRoleAsync(string role, CancellationToken cancellationToken = default)
        {
            try
            {
                return await ExecuteWithErrorHandlingAsync(async () =>
                {
                    return await ExecuteQueryListAsync<RolePermissions>(
                        StoredProcedures.GET_PermissionsByRole,
                        role);
                }, "GetPermissionsForRole", cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return [];
            }
        }

        public async Task<List<PermissionDefinition>> GetAllPermissionsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await ExecuteWithErrorHandlingAsync(async () =>
                {
                    return await ExecuteQueryListAsync<PermissionDefinition>(
                        StoredProcedures.GET_ALLPermissions);
                }, "GetAllPermissions", cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return [];
            }
        }

        public async Task<List<Role>> GetRoleByIdAsync(Guid IdRole, CancellationToken cancellationToken = default)
        {
            try
            {
                return await ExecuteWithErrorHandlingAsync(async () =>
                {
                    return await ExecuteQueryListAsync<Role>(
                        StoredProcedures.GET_RoleById, IdRole);
                }, "GetRoleById", cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return [];
            }
        }

        public async Task<List<RoleAssignment>> GetAllUsersWithRolesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await ExecuteWithErrorHandlingAsync(async () =>
                {
                    return await ExecuteQueryListAsync<RoleAssignment>(
                        StoredProcedures.GET_AllUsersWithRoles);
                }, "GetAllUsersWithRoles", cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        }

        public async Task<Role?> GetRoleByUserIdAsync(Guid idUser, CancellationToken cancellationToken = default)
        {
            try
            {
                return await ExecuteWithErrorHandlingAsync(async () =>
                {
                    var roles = await ExecuteQueryListAsync<Role>(
                        StoredProcedures.GET_RoleByUserId, idUser);
                    return roles.FirstOrDefault();
                }, "GetRoleByUserId", cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        }

        public async Task<List<Role>> GetAllRolesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await ExecuteWithErrorHandlingAsync(async () =>
                {
                    return await ExecuteQueryListAsync<Role>(
                        StoredProcedures.GET_AllRoles);
                }, "GetAllRoles", cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return [];
            }
        }

        // La suscripción vigente (más reciente) del Administrador -- null si la
        // cuenta todavía no tiene ninguna fila en Subscription (cuentas de antes
        // de este feature). Ver ISubscriptionService.EnsureCanCreateBuildingAsync,
        // que trata ese caso como "sin límite".
        public async Task<Models.Subscription?> GetSubscriptionByUserAsync(Guid idUser, CancellationToken cancellationToken = default)
        {
            try
            {
                return await ExecuteWithErrorHandlingAsync(async () =>
                {
                    var subscriptions = await ExecuteQueryListAsync<Models.Subscription>(
                        StoredProcedures.GET_SubscriptionByUser, idUser);
                    return subscriptions.FirstOrDefault();
                }, "GetSubscriptionByUser", cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        }

        public async Task<List<Models.SubscriptionPlan>> GetAllSubscriptionPlansAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await ExecuteWithErrorHandlingAsync(async () =>
                {
                    return await ExecuteQueryListAsync<Models.SubscriptionPlan>(
                        StoredProcedures.GET_AllSubscriptionPlans);
                }, "GetAllSubscriptionPlans", cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return [];
            }
        }

        // La Account a la que pertenece este usuario (Owner o Colaborador) -- null
        // si todavía no tiene ninguna (cuentas de antes de este feature, ver
        // Docs/Design-Account-Facturacion.md). Todo el resto de ISubscriptionService
        // cae de vuelta al comportamiento viejo por IdUser directo cuando esto da null.
        public async Task<Models.Account?> GetAccountByUserAsync(Guid idUser, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var accounts = await ExecuteQueryListAsync<Models.Account>(
                    StoredProcedures.GET_AccountByUser, idUser);
                return accounts.FirstOrDefault();
            }, "GetAccountByUser", cancellationToken);
        }

        // A diferencia de GetAccountByUserAsync (que resuelve la Account de una
        // PERSONA), esto resuelve por Building.IdAccount directo -- lo necesita
        // InstallmentExportService para el logo del recibo sin depender de qué
        // usuario está exportando.
        public async Task<Models.Account?> GetAccountByIdAsync(Guid idAccount, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var accounts = await ExecuteQueryListAsync<Models.Account>(
                    StoredProcedures.GET_AccountById, idAccount);
                return accounts.FirstOrDefault();
            }, "GetAccountById", cancellationToken);
        }

        public async Task<List<Models.AccountUserView>> GetAccountUsersByAccountAsync(Guid idAccount, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.AccountUserView>(
                    StoredProcedures.GET_AccountUsersByAccount, idAccount);
            }, "GetAccountUsersByAccount", cancellationToken);
        }

        public async Task<Models.AccountInvitation?> GetAccountInvitationByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var invitations = await ExecuteQueryListAsync<Models.AccountInvitation>(
                    StoredProcedures.GET_AccountInvitationByCode, code);
                return invitations.FirstOrDefault();
            }, "GetAccountInvitationByCode", cancellationToken);
        }

        public async Task<List<Models.AccountInvitation>> GetPendingInvitationsByAccountAsync(Guid idAccount, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.AccountInvitation>(
                    StoredProcedures.GET_PendingInvitationsByAccount, idAccount);
            }, "GetPendingInvitationsByAccount", cancellationToken);
        }

        public async Task<List<Models.Building>> GetBuildingsByAccountAsync(Guid idAccount, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.Building>(
                    StoredProcedures.GET_BuildingsByAccount, idAccount);
            }, "GetBuildingsByAccount", cancellationToken);
        }

        public async Task<List<MenuItemWithRoles>> GetMenuItemsAsync(CancellationToken cancellationToken = default)
        {
            List<MenuItemWithRoles> list = [];
            try
            {
                using var connection = new SqlConnection(_connectionString);
                using var command = new SqlCommand(StoredProcedures.GET_MenuItem, connection);

                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = 30;

                // Usar SqlDataAdapter en lugar de DataReader
                using var adapter = new SqlDataAdapter(command);
                var dataTable = new DataTable();

                // Fill no es async pero no se cuelga con múltiples filas
                adapter.Fill(dataTable);

                foreach (DataRow row in dataTable.Rows)
                {
                    list.Add(
                        new MenuItemWithRoles
                        {

                            IdMenu = row["IdMenu"] is Guid g1 ? g1 : Guid.Parse(row["IdMenu"].ToString()!),
                            IdParent = row["IdParent"] == DBNull.Value ? Guid.Empty : (row["IdParent"] is Guid g2 ? g2 : Guid.Parse(row["ParentId"].ToString()!)),
                            ItemKey = row["ItemKey"]?.ToString() ?? "",
                            Title = row["Title"]?.ToString() ?? "",
                            Icon = row["Icon"]?.ToString() ?? "",
                            Url = row["Url"]?.ToString() ?? "",
                            Target = row["Target"]?.ToString() ?? "",
                            ParentKey = row["ParentKey"]?.ToString() ?? "",
                            DisplayOrder = row["DisplayOrder"] is int d ? d : Convert.ToInt32(row["DisplayOrder"])
                        });
                }

                return list;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return [];
            }
        }

        public async Task<List<RealEstateUnit>> GetUnitsByBuildingAsync1(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            try
            {
                return await ExecuteWithErrorHandlingAsync(async () =>
                {
                    return await ExecuteQueryListAsync<RealEstateUnit>(
                        StoredProcedures.GET_UnitsByBuilding,
                        idBuilding);
                }, "GetUnitsByBuilding", cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return [];
            }
        }

        public async Task<List<ServiceReadingDetail>> GetServiceReadingDetailbyPeriodAsync(DateTime period, Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                // GET_ServiceReadingDetailList ahora toma @IdBuilding además de @Period
                // (Database/Scripts/2026-09-09_74_GET_ServiceReadingDetailList_FiltraPorEdificio.sql)
                // -- antes sólo filtraba por Period, así que dos edificios con un
                // ServiceReading del mismo Period exacto se mezclaban (confirmado con
                // datos reales: una unidad "101" de OTRO edificio aparecía junto a la
                // "101" del edificio consultado).
                var resultado = await ExecuteQueryListAsync<ServiceReadingDetail>(
                    StoredProcedures.GET_ServiceReadingDetailList,
                    period,
                    idBuilding);

                // El fan-out que duplicaba cada fila (JOIN GroupUnit sin colapsar primero
                // -- un Grupo de Unidades con más de una unidad física, ej. depto +
                // cochera, tenía más de una fila en GroupUnit para el mismo IdGroupUnit)
                // ya se corrigió de raíz en el SP
                // (Database/Scripts/2026-09-09_73_Fix_GET_ServiceReadingDetailList_Duplicates.sql).
                // Se deja este dedup como red de seguridad -- no hace nada si el SP ya no
                // duplica, y si algo raro pasa de nuevo, prefiere el PreviousReading más
                // alto entre los duplicados en vez de mostrar dos filas por lectura.
                return resultado
                    .GroupBy(d => d.IdServiceReadingDetail)
                    .Select(g => g.Count() == 1 ? g.First() : g.OrderByDescending(d => d.PreviousReading).First())
                    .ToList();
            }, "GetServiceReadingDetailbyPeriod", cancellationToken);
        }

        public async Task<List<ServiceReadingDetail>> GetFirstWaterReadingDetailListAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<ServiceReadingDetail>(
                    StoredProcedures.GET_FirstWaterReadingDetailList,
                    idBuilding);
            }, "GetFirstWaterReadingDetailList", cancellationToken);
        }

        public async Task<List<Parameter>> GetParametersByBuildingAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Parameter>(
                    StoredProcedures.GET_AllParameters,
                    idBuilding);
            }, "GetParametersByBuilding", cancellationToken);
        }

        // Paso 5 (promoción/fusión, ver Docs/Design-Defaults-Sistema-Mixto.md §5.3):
        // hijos Mixto activos, todavía no globales (IdBuilding IS NOT NULL) y
        // agregados A MANO por el admin de cada edificio (IsSystemDefault = 0 en el
        // hijo -- excluye los clonados del template, que no son duplicados reales,
        // sólo la misma fila copiada a propósito una vez por edificio), de TODOS los
        // edificios a la vez -- a diferencia de GetParametersByBuildingAsync, que
        // siempre filtra por un edificio puntual. Es la única forma de que un
        // SysAdmin detecte a ojo duplicados entre edificios (la detección queda
        // manual a propósito, sin nada automático).
        public async Task<List<Models.ParameterPromotionCandidate>> GetMixtoParameterCandidatesAsync(CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.ParameterPromotionCandidate>(
                    StoredProcedures.GET_MixtoParameterCandidates);
            }, "GetMixtoParameterCandidates", cancellationToken);
        }

        // Devuelve List<ViewExpense>, no List<Expense> -- GET_ExpensesByBuilding
        // devuelve columnas que coinciden con ViewExpense.cs (confirmado con
        // INFORMATION_SCHEMA.COLUMNS de dbo.Expense), no con Classes/Expense.cs.
        public async Task<List<ViewExpense>> GetExpensesByBuildingAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<ViewExpense>(
                    StoredProcedures.GET_ExpensesByBuilding,
                    idBuilding);
            }, "GetExpensesByBuilding", cancellationToken);
        }

        public async Task<List<TransactionBankHeader>> GetMovementByFileNameAsync(string fileName, Guid idBankAccount, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<TransactionBankHeader>(
                    StoredProcedures.GET_MovementByName,
                    fileName, idBankAccount);
            }, "GetMovementByFileName", cancellationToken);
        }

        // idBankAccount == null trae todas las cuentas del edificio ("Todos"); con valor,
        // filtra a esa cuenta bancaria únicamente.
        public async Task<List<TransactionBankHeader>> GetMovementHeadersAsync(Guid idBuilding, Guid? idBankAccount, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<TransactionBankHeader>(
                    StoredProcedures.GET_MovementHeaders,
                    idBuilding, idBankAccount);
            }, "GetMovementHeaders", cancellationToken);
        }

        public async Task<List<AccountStatementDetailView>> GetAccountStatementDetailByHeaderAsync(Guid idStatementHeader, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<AccountStatementDetailView>(
                    StoredProcedures.GET_AccountStatementDetailByHeader,
                    idStatementHeader);
            }, "GetAccountStatementDetailByHeader", cancellationToken);
        }

        public async Task<List<MovDetKey>> GetAllMovementDetailAsync(Guid idBankAccout, DateTime star, DateTime end, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<MovDetKey>(
                    StoredProcedures.GET_AllMovementDetail,
                    idBankAccout, star, end);
            }, "GetAllMovementDetail", cancellationToken);
        }

        public async Task<List<Installment>> GetInstallmentsByBudgetAsync(Guid idBudgetHeader, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Installment>(
                    StoredProcedures.GET_InstallmentsByBudget,
                    idBudgetHeader);
            }, "GetInstallmentsByBudget", cancellationToken);
        }

        public async Task<List<Installment>> GetPendingInstallmentsAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Installment>(
                    StoredProcedures.GET_PendingInstallments,
                    idBuilding);
            }, "GetPendingInstallments", cancellationToken);
        }

        // A diferencia de GetPendingInstallmentsAsync (GET_PendingInstallments filtra
        // Status <> 1), esta trae TODAS las cuotas del edificio sin importar su estado
        // -- la usa /cuotas (InstallmentList.razor), que necesita poder listar y
        // filtrar también por "Pagadas".
        public async Task<List<Installment>> GetInstallmentsByBuildingAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Installment>(
                    StoredProcedures.GET_InstallmentsByBuilding,
                    idBuilding);
            }, "GetInstallmentsByBuilding", cancellationToken);
        }

        public async Task<List<Exoneration>> GetExonerationByBudgetHeaderAsync(Guid idBudgetHeader, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Exoneration>(
                    StoredProcedures.GET_ExonerationByBudgetHeader,
                    idBudgetHeader);
            }, "GetExonerationByBudgetHeader", cancellationToken);
        }

        public async Task<List<BudgetHeader>> GetBudgetsByBuildingAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<BudgetHeader>(
                    StoredProcedures.GET_Budgets,
                    idBuilding);
            }, "GetBudgetsByBuilding", cancellationToken);
        }

        public async Task<List<Models.Category>> GetCategoriesAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.Category>(
                    StoredProcedures.GET_Categories,
                    idBuilding);
            }, "GetCategories", cancellationToken);
        }

        public async Task<List<Models.UnitView>> GetGroupUnitsByTypeAsync(Guid idBuilding, int _type, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.UnitView>(
                    StoredProcedures.GET_UnitsByType,
                    idBuilding, _type) ?? [];
            }, "GetGroupUnitsByType", cancellationToken);
        }

        public async Task<List<Exoneration>> GetExonerationsByBuildingAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Exoneration>(
                    StoredProcedures.GET_Exoneration_All,
                    idBuilding) ?? new List<Exoneration>();
            }, "GetExonerationsByBuilding", cancellationToken);
        }

        public async Task<List<BankAccount>> GetBankAccountsByBuildingAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<BankAccount>(
                    StoredProcedures.GET_BankAccountsByBuilding,
                    idBuilding) ?? new List<BankAccount>();
            }, "GetBankAccountsByBuilding", cancellationToken);
        }

        public async Task<List<ServiceReading>> GetServiceReadingbyPeriodAsync(DateTime period, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<ServiceReading>(
                    StoredProcedures.GET_ServiceReading,
                    period) ?? new List<ServiceReading>();
            }, "GetServiceReadingbyPeriod", cancellationToken);
        }

        public async Task<List<ServiceReading>> GetServiceReadingListAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<ServiceReading>(
                    StoredProcedures.GET_ServiceReadingList,
                    idBuilding) ?? new List<ServiceReading>();
            }, "GetServiceReadingList", cancellationToken);
        }

        public async Task<List<BudgetDetail>> GetBudgetDetailAsync(Guid idBudgetHeader, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<BudgetDetail>(
                    StoredProcedures.GET_List_BudgetDetail,
                    idBudgetHeader) ?? new List<BudgetDetail>();
            }, "GetBudgetDetail", cancellationToken);
        }

        public async Task<List<InstallmentPaid>> GetInstallmentsPaidAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<InstallmentPaid>(
                    StoredProcedures.GET_InstallmentPaid,
                    idBuilding) ?? new List<InstallmentPaid>();
            }, "GetInstallmentsPaid", cancellationToken);
        }

        public async Task<InvitationModel> GetInvitationByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var result = await ExecuteQuerySingleAsync<InvitationModel>(
                    StoredProcedures.GET_InvitationByCode,
                    code);
                return result ?? throw new EntityNotFoundException($"Invitation with code {code} not found");
            }, "GetInvitationByCode", cancellationToken);
        }

        public async Task<UserModel> GetUserByIdAsync(Guid idUser, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var result = await ExecuteQuerySingleAsync<UserModel>(
                    StoredProcedures.GET_UserById,
                    idUser);

                return result ?? throw new EntityNotFoundException($"User with ID {idUser} not found");
            }, "GetUserById", cancellationToken);
        }

        public async Task<Building> GetBuildingByIdAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var result = await ExecuteQuerySingleAsync<Building>(
                    StoredProcedures.GET_BuildingById,
                    idBuilding);

                return result ?? throw new EntityNotFoundException($"Building with ID {idBuilding} not found");
            }, "GetBuildingById", cancellationToken);
        }

        public async Task<Category> GetCategoryByIdAsync(Guid idCategory, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var result = await ExecuteQuerySingleAsync<Category>(
                    StoredProcedures.GET_CategoryById,
                    idCategory);

                return result ?? throw new EntityNotFoundException($"Category with ID {idCategory} not found");
            }, "GetCategoryById", cancellationToken);
        }

        public async Task<BudgetHeader> GetBudgetByIdAsync(Guid idBudgetHeader, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var result = await ExecuteQuerySingleAsync<BudgetHeader>(
                    StoredProcedures.GET_BudgetById,
                    idBudgetHeader);

                return result ?? throw new EntityNotFoundException($"BudgetHeader with ID {idBudgetHeader} not found");
            }, "GetBudgetById", cancellationToken);
        }

        public async Task<List<Models.Workflow>> GetWorkflowsAsync(CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.Workflow>(StoredProcedures.GET_Workflows);
            }, "GetWorkflows", cancellationToken);
        }

        public async Task<List<Models.WorkflowStep>> GetWorkflowStepsByWorkflowAsync(Guid idWorkflow, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.WorkflowStep>(StoredProcedures.GET_WorkflowStepsByWorkflow, idWorkflow);
            }, "GetWorkflowStepsByWorkflow", cancellationToken);
        }

        // Fila única (Id=1) sembrada por el script de Database/Scripts -- si por algún
        // motivo no existe (BD no actualizada todavía), se devuelve un default con logging
        // apagado en vez de reventar, para no tumbar el resto de la app.
        public async Task<Models.SystemLogSettings> GetSystemLogSettingsAsync(CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var result = await ExecuteQuerySingleAsync<Models.SystemLogSettings>(StoredProcedures.GET_SystemLogSettings);
                return result ?? new Models.SystemLogSettings { IsEnabled = false };
            }, "GetSystemLogSettings", cancellationToken);
        }

        public async Task<List<Models.SystemLogEntry>> GetRecentSystemLogsAsync(int top = 500, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.SystemLogEntry>(StoredProcedures.GET_SystemLogs_Recent, top);
            }, "GetRecentSystemLogs", cancellationToken);
        }

        public async Task<List<Models.Incident>> GetIncidentsByBuildingAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.Incident>(StoredProcedures.GET_IncidentsByBuilding, idBuilding);
            }, "GetIncidentsByBuilding", cancellationToken);
        }

        public async Task<List<Models.Incident>> GetIncidentsByReporterAsync(Guid reportedBy, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.Incident>(StoredProcedures.GET_IncidentsByReporter, reportedBy);
            }, "GetIncidentsByReporter", cancellationToken);
        }

        public async Task<Models.Incident> GetIncidentByIdAsync(Guid idIncident, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var result = await ExecuteQuerySingleAsync<Models.Incident>(StoredProcedures.GET_IncidentById, idIncident);
                return result ?? throw new EntityNotFoundException($"Incident with ID {idIncident} not found");
            }, "GetIncidentById", cancellationToken);
        }

        public async Task<List<Models.WorkflowAuditEntry>> GetWorkflowAuditLogAsync(string module, Guid entityId, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.WorkflowAuditEntry>(StoredProcedures.GET_WorkflowAuditLog, module, entityId);
            }, "GetWorkflowAuditLog", cancellationToken);
        }

        // Docs/Pendientes-Negocio-Conciliacion.md #3 -- GET_LastReconciliationSession ya
        // trae sólo la más reciente (TOP 1 ORDER BY Fecha DESC) por cuenta bancaria.
        public async Task<Models.ReconciliationSession?> GetLastReconciliationSessionAsync(Guid idBankAccount, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var resultado = await ExecuteQueryListAsync<Models.ReconciliationSession>(StoredProcedures.GET_LastReconciliationSession, idBankAccount);
                return resultado.FirstOrDefault();
            }, "GetLastReconciliationSession", cancellationToken);
        }

        // Docs/Pendientes-Negocio-Conciliacion.md #5
        public async Task<List<Models.ExpenseTemplate>> GetExpenseTemplatesByBuildingAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.ExpenseTemplate>(StoredProcedures.GET_ExpenseTemplatesByBuilding, idBuilding);
            }, "GetExpenseTemplatesByBuilding", cancellationToken);
        }

        // Docs/Pendientes-Negocio-Consolidado.md #18b -- null si esta cuota todavía
        // no tiene un recibo generado/guardado.
        public async Task<Models.ReceiptFile?> GetReceiptFileByInstallmentAsync(Guid idInstallment, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var resultado = await ExecuteQueryListAsync<Models.ReceiptFile>(StoredProcedures.GET_ReceiptFileByInstallment, idInstallment);
                return resultado.FirstOrDefault();
            }, "GetReceiptFileByInstallment", cancellationToken);
        }

        public async Task<List<Models.IncidentComment>> GetIncidentCommentsAsync(Guid idIncident, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.IncidentComment>(StoredProcedures.GET_IncidentCommentsByIncident, idIncident);
            }, "GetIncidentComments", cancellationToken);
        }

        public async Task<List<Models.IncidentAttachment>> GetIncidentAttachmentsAsync(Guid idIncident, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.IncidentAttachment>(StoredProcedures.GET_IncidentAttachmentsByIncident, idIncident);
            }, "GetIncidentAttachments", cancellationToken);
        }

        public async Task<List<Models.Announcement>> GetAnnouncementsByBuildingAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.Announcement>(StoredProcedures.GET_AnnouncementsByBuilding, idBuilding);
            }, "GetAnnouncementsByBuilding", cancellationToken);
        }

        public async Task<List<Models.AnnouncementRecipient>> GetAnnouncementRecipientsAsync(Guid idAnnouncement, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.AnnouncementRecipient>(StoredProcedures.GET_AnnouncementRecipientsByAnnouncement, idAnnouncement);
            }, "GetAnnouncementRecipients", cancellationToken);
        }

        public async Task<List<Models.Announcement>> GetAnnouncementsParaUsuarioAsync(Guid idBuilding, string rolUsuario, Guid? idGroupUnit, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.Announcement>(
                    StoredProcedures.GET_AnnouncementsParaUsuario, idBuilding, rolUsuario, (object?)idGroupUnit ?? DBNull.Value);
            }, "GetAnnouncementsParaUsuario", cancellationToken);
        }

        public async Task<List<Models.CommonArea>> GetCommonAreasByBuildingAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.CommonArea>(StoredProcedures.GET_CommonAreasByBuilding, idBuilding);
            }, "GetCommonAreasByBuilding", cancellationToken);
        }

        public async Task<List<Models.Reservation>> GetReservationsByBuildingAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.Reservation>(StoredProcedures.GET_ReservationsByBuilding, idBuilding);
            }, "GetReservationsByBuilding", cancellationToken);
        }

        public async Task<Models.Reservation> GetReservationByIdAsync(Guid idReservation, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var result = await ExecuteQuerySingleAsync<Models.Reservation>(StoredProcedures.GET_ReservationById, idReservation);
                return result ?? throw new EntityNotFoundException($"Reservation with ID {idReservation} not found");
            }, "GetReservationById", cancellationToken);
        }

        public async Task<List<Models.Reservation>> GetReservationsPendientesByBuildingAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.Reservation>(StoredProcedures.GET_ReservationsPendientesByBuilding, idBuilding);
            }, "GetReservationsPendientesByBuilding", cancellationToken);
        }

        public async Task<List<Models.Reservation>> GetReservationsByGroupUnitAsync(Guid idGroupUnit, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.Reservation>(StoredProcedures.GET_ReservationsByGroupUnit, idGroupUnit);
            }, "GetReservationsByGroupUnit", cancellationToken);
        }

        public async Task<List<Models.Reservation>> GetReservationsConflictoAsync(Guid idCommonArea, DateTime fechaInicio, DateTime fechaFin, Guid? excluirIdReservation = null, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.Reservation>(StoredProcedures.GET_ReservationsConflicto, idCommonArea, fechaInicio, fechaFin, (object?)excluirIdReservation ?? DBNull.Value);
            }, "GetReservationsConflicto", cancellationToken);
        }

        public async Task<List<Models.Reservation>> GetReservationsProximasByCommonAreaAsync(Guid idCommonArea, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.Reservation>(StoredProcedures.GET_ReservationsProximasByCommonArea, idCommonArea);
            }, "GetReservationsProximasByCommonArea", cancellationToken);
        }

        public async Task<List<Models.ReservationChecklistItem>> GetReservationChecklistItemsByReservationAsync(Guid idReservation, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.ReservationChecklistItem>(StoredProcedures.GET_ReservationChecklistItemsByReservation, idReservation);
            }, "GetReservationChecklistItemsByReservation", cancellationToken);
        }

        public async Task<List<Models.ReservationAttachment>> GetReservationAttachmentsByReservationAsync(Guid idReservation, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.ReservationAttachment>(StoredProcedures.GET_ReservationAttachmentsByReservation, idReservation);
            }, "GetReservationAttachmentsByReservation", cancellationToken);
        }

        public async Task<List<Models.CommunityIncome>> GetCommunityIncomesByBuildingAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.CommunityIncome>(StoredProcedures.GET_CommunityIncomesByBuilding, idBuilding);
            }, "GetCommunityIncomesByBuilding", cancellationToken);
        }

        public async Task<List<Models.CalendarItem>> GetCalendarItemsByBuildingAsync(Guid idBuilding, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.CalendarItem>(StoredProcedures.GET_CalendarItemsByBuilding, idBuilding, (object?)from, (object?)to);
            }, "GetCalendarItemsByBuilding", cancellationToken);
        }

        public async Task<Models.CalendarItem> GetCalendarItemByIdAsync(Guid idCalendarItem, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var result = await ExecuteQuerySingleAsync<Models.CalendarItem>(StoredProcedures.GET_CalendarItemById, idCalendarItem);
                return result ?? throw new EntityNotFoundException($"CalendarItem with ID {idCalendarItem} not found");
            }, "GetCalendarItemById", cancellationToken);
        }

        // Employee y Payroll -- Fase 1
        public async Task<List<Models.Employee>> GetEmployeeByAccountAsync(Guid idAccount, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.Employee>(StoredProcedures.GET_EmployeeByAccount, idAccount);
            }, "GetEmployeeByAccount", cancellationToken);
        }

        public async Task<Models.Employee> GetEmployeeByIdAsync(Guid idEmployee, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var result = await ExecuteQuerySingleAsync<Models.Employee>(StoredProcedures.GET_EmployeeById, idEmployee);
                return result ?? throw new EntityNotFoundException($"Employee with ID {idEmployee} not found");
            }, "GetEmployeeById", cancellationToken);
        }

        public async Task<List<Models.Shift>> GetShiftsByAccountAsync(Guid idAccount, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.Shift>(StoredProcedures.GET_ShiftsByAccount, idAccount);
            }, "GetShiftsByAccount", cancellationToken);
        }

        public async Task<List<Models.EmployeeShiftAssignment>> GetAsignacionesShiftByEmployeeAsync(Guid idEmployee, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.EmployeeShiftAssignment>(StoredProcedures.GET_AsignacionesShiftByEmployee, idEmployee);
            }, "GetAsignacionesShiftByEmployee", cancellationToken);
        }

        public async Task<List<Models.EmployeeBuildingAssignment>> GetEmployeeBuildingAssignmentsByEmployeeAsync(Guid idEmployee, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.EmployeeBuildingAssignment>(StoredProcedures.GET_EmployeeBuildingAssignmentsByEmployee, idEmployee);
            }, "GetEmployeeBuildingAssignmentsByEmployee", cancellationToken);
        }

        public async Task<List<Models.EmployeeBuildingAssignment>> GetEmployeeBuildingAssignmentsByBuildingAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.EmployeeBuildingAssignment>(StoredProcedures.GET_EmployeeBuildingAssignmentsByBuilding, idBuilding);
            }, "GetEmployeeBuildingAssignmentsByBuilding", cancellationToken);
        }

        public async Task<List<Models.TimeEntry>> GetTimeEntryByEmployeeAsync(Guid idEmployee, DateTime fechaDesde, DateTime fechaHasta, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.TimeEntry>(StoredProcedures.GET_TimeEntryByEmployee, idEmployee, fechaDesde, fechaHasta);
            }, "GetTimeEntryByEmployee", cancellationToken);
        }

        public async Task<List<Models.HolidayConfiguration>> GetFeriadosByAccountAndYearAsync(Guid? idAccount, int anio, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.HolidayConfiguration>(StoredProcedures.GET_FeriadosByAccountAndYear, (object?)idAccount ?? DBNull.Value, anio);
            }, "GetFeriadosByAccountAndYear", cancellationToken);
        }

        // Employee y Payroll -- Fase 2
        public async Task<Models.LaborRegimeConfiguration?> GetLaborRegimeConfigurationVigenteAsync(Guid idAccount, DateTime? fecha = null, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQuerySingleAsync<Models.LaborRegimeConfiguration>(StoredProcedures.GET_LaborRegimeConfigurationVigente, idAccount, (object?)fecha ?? DBNull.Value);
            }, "GetLaborRegimeConfigurationVigente", cancellationToken);
        }

        public async Task<List<Models.LaborRegimeConfiguration>> GetLaborRegimeConfigurationHistorialAsync(Guid idAccount, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.LaborRegimeConfiguration>(StoredProcedures.GET_LaborRegimeConfigurationHistorial, idAccount);
            }, "GetLaborRegimeConfigurationHistorial", cancellationToken);
        }

        public async Task<Models.LegalParameters?> GetLegalParametersByAccountAndYearAsync(Guid idAccount, int anio, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQuerySingleAsync<Models.LegalParameters>(StoredProcedures.GET_LegalParametersByAccountAndYear, idAccount, anio);
            }, "GetLegalParametersByAccountAndYear", cancellationToken);
        }

        public async Task<List<Models.Vacation>> GetVacationByEmployeeAsync(Guid idEmployee, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.Vacation>(StoredProcedures.GET_VacationByEmployee, idEmployee);
            }, "GetVacationByEmployee", cancellationToken);
        }

        public async Task<List<Models.Vacation>> GetVacationPendientesByAccountAsync(Guid idAccount, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.Vacation>(StoredProcedures.GET_VacationPendientesByAccount, idAccount);
            }, "GetVacationPendientesByAccount", cancellationToken);
        }

        // Escalar (un int) -- no un Models.* completo, así que se pide con
        // SqlQueryRaw en vez de ExecuteQueryListAsync<T> (mismo motivo que
        // GetTransactionByOriginalReferenceAsync más arriba).
        public async Task<int> GetVacationGozadasByEmployeeAnioAsync(Guid idEmployee, int anio, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var dbContext = await RentContextAsync(cancellationToken);
                try
                {
                    var sql = $"EXEC {StoredProcedures.GET_VacationGozadasByEmployeeAnio} @p0, @p1";
                    var resultados = await dbContext.Database
                        .SqlQueryRaw<int>(sql,
                            new SqlParameter("@p0", idEmployee),
                            new SqlParameter("@p1", anio))
                        .ToListAsync(cancellationToken);
                    return resultados.Count > 0 ? resultados[0] : 0;
                }
                finally
                {
                    ReturnContext(dbContext);
                }
            }, "GetVacationGozadasByEmployeeAnio", cancellationToken);
        }

        public async Task<List<Models.Payslip>> GetPayslipsByEmployeeAsync(Guid idEmployee, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.Payslip>(StoredProcedures.GET_PayslipsByEmployee, idEmployee);
            }, "GetPayslipsByEmployee", cancellationToken);
        }

        public async Task<Models.Payslip?> GetPayslipByIdAsync(Guid idPayslip, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQuerySingleAsync<Models.Payslip>(StoredProcedures.GET_PayslipById, idPayslip);
            }, "GetPayslipById", cancellationToken);
        }

        public async Task<List<Models.Payslip>> GetPayslipsByAccountAndPeriodoAsync(Guid idAccount, int anio, int mes, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.Payslip>(StoredProcedures.GET_PayslipsByAccountAndPeriodo, idAccount, anio, mes);
            }, "GetPayslipsByAccountAndPeriodo", cancellationToken);
        }

        public async Task<List<Models.PayslipDetail>> GetPayslipDetailByPayslipAsync(Guid idPayslip, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.PayslipDetail>(StoredProcedures.GET_PayslipDetailByPayslip, idPayslip);
            }, "GetPayslipDetailByPayslip", cancellationToken);
        }

        // Employee y Payroll -- Fase 3 (Permisos y licencias)
        public async Task<List<Models.LeaveRequest>> GetLeaveRequestByEmployeeAsync(Guid idEmployee, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.LeaveRequest>(StoredProcedures.GET_LeaveRequestByEmployee, idEmployee);
            }, "GetLeaveRequestByEmployee", cancellationToken);
        }

        public async Task<List<Models.LeaveRequest>> GetLeaveRequestPendientesByAccountAsync(Guid idAccount, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.LeaveRequest>(StoredProcedures.GET_LeaveRequestPendientesByAccount, idAccount);
            }, "GetLeaveRequestPendientesByAccount", cancellationToken);
        }

        // Escalar (un int) -- mismo motivo que GetVacationGozadasByEmployeeAnioAsync.
        public async Task<int> GetLeaveRequestSinGoceDiasByEmployeeMesAsync(Guid idEmployee, DateTime fechaDesde, DateTime fechaHasta, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var dbContext = await RentContextAsync(cancellationToken);
                try
                {
                    var sql = $"EXEC {StoredProcedures.GET_LeaveRequestSinGoceDiasByEmployeeMes} @p0, @p1, @p2";
                    var resultados = await dbContext.Database
                        .SqlQueryRaw<int>(sql,
                            new SqlParameter("@p0", idEmployee),
                            new SqlParameter("@p1", fechaDesde),
                            new SqlParameter("@p2", fechaHasta))
                        .ToListAsync(cancellationToken);
                    return resultados.Count > 0 ? resultados[0] : 0;
                }
                finally
                {
                    ReturnContext(dbContext);
                }
            }, "GetLeaveRequestSinGoceDiasByEmployeeMes", cancellationToken);
        }

        // Gobernanza / Meetings -- Fase 1
        public async Task<List<Models.Meeting>> GetMeetingsByBuildingAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.Meeting>(StoredProcedures.GET_MeetingsByBuilding, idBuilding);
            }, "GetMeetingsByBuilding", cancellationToken);
        }

        public async Task<Models.Meeting> GetMeetingByIdAsync(Guid idMeeting, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var result = await ExecuteQuerySingleAsync<Models.Meeting>(StoredProcedures.GET_MeetingById, idMeeting);
                return result ?? throw new EntityNotFoundException($"Meeting with ID {idMeeting} not found");
            }, "GetMeetingById", cancellationToken);
        }

        public async Task<List<Models.AgendaItem>> GetAgendaItemsByMeetingAsync(Guid idMeeting, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.AgendaItem>(StoredProcedures.GET_AgendaItemsByMeeting, idMeeting);
            }, "GetAgendaItemsByMeeting", cancellationToken);
        }

        public async Task<Models.AgendaItem> GetAgendaItemByIdAsync(Guid idAgendaItem, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var result = await ExecuteQuerySingleAsync<Models.AgendaItem>(StoredProcedures.GET_AgendaItemById, idAgendaItem);
                return result ?? throw new EntityNotFoundException($"AgendaItem with ID {idAgendaItem} not found");
            }, "GetAgendaItemById", cancellationToken);
        }

        public async Task<List<Models.Attendance>> GetAttendancesByMeetingAsync(Guid idMeeting, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.Attendance>(StoredProcedures.GET_AttendancesByMeeting, idMeeting);
            }, "GetAttendancesByMeeting", cancellationToken);
        }

        // Gobernanza / Votación -- Fase 2
        public async Task<List<Models.VotingRound>> GetVotingRoundsByAgendaItemAsync(Guid idAgendaItem, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.VotingRound>(StoredProcedures.GET_VotingRoundsByAgendaItem, idAgendaItem);
            }, "GetVotingRoundsByAgendaItem", cancellationToken);
        }

        public async Task<Models.VotingRound> GetVotingRoundByIdAsync(Guid idVotingRound, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var result = await ExecuteQuerySingleAsync<Models.VotingRound>(StoredProcedures.GET_VotingRoundById, idVotingRound);
                return result ?? throw new EntityNotFoundException($"VotingRound with ID {idVotingRound} not found");
            }, "GetVotingRoundById", cancellationToken);
        }

        public async Task<List<Models.Vote>> GetVotesByVotingRoundAsync(Guid idVotingRound, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.Vote>(StoredProcedures.GET_VotesByVotingRound, idVotingRound);
            }, "GetVotesByVotingRound", cancellationToken);
        }

        // Gobernanza / MeetingMinutes -- Fase 3. Devuelve null si la Meeting todavía no
        // tiene MeetingMinutes generada (no se trata como EntityNotFoundException -- es
        // un estado válido y esperado, no un error).
        public async Task<Models.MeetingMinutes?> GetMeetingMinutesByMeetingAsync(Guid idMeeting, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQuerySingleAsync<Models.MeetingMinutes>(StoredProcedures.GET_MeetingMinutesByMeeting, idMeeting);
            }, "GetMeetingMinutesByMeeting", cancellationToken);
        }

        // Junta Directiva (Docs/Pendientes-Negocio-Consolidado.md #34). Null si el
        // edificio nunca constituyó una Junta -- estado válido, no un error.
        public async Task<Models.BuildingBoard?> GetActiveBuildingBoardAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQuerySingleAsync<Models.BuildingBoard>(StoredProcedures.GET_ActiveBuildingBoard, idBuilding);
            }, "GetActiveBuildingBoard", cancellationToken);
        }

        public async Task<List<Models.BuildingBoardMemberView>> GetBuildingBoardMembersAsync(Guid idBuildingBoard, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.BuildingBoardMemberView>(StoredProcedures.GET_BuildingBoardMembers, idBuildingBoard);
            }, "GetBuildingBoardMembers", cancellationToken);
        }
        #endregion
    }
}