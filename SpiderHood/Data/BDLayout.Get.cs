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

        public async Task<List<TransactionBankDetail>> GetBankTransactionsNoConciliedAsync(Guid idBuilding, DateTime star, DateTime end, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<TransactionBankDetail>(
                    StoredProcedures.GET_BankTransactionsNoConcilied,
                    idBuilding, star, end);
            }, "GetBankTransactionsNoConcilied", cancellationToken);
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

                return units;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                throw;
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

        public async Task<List<ServiceReadingDetail>> GetServiceReadingDetailbyPeriodAsync(DateTime period, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<ServiceReadingDetail>(
                    StoredProcedures.GET_ServiceReadingDetailList,
                    period);
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

        public async Task<List<Expense>> GetExpensesByBuildingAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Expense>(
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

        public async Task<List<Models.IncidentComment>> GetIncidentCommentsAsync(Guid idIncident, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.IncidentComment>(StoredProcedures.GET_IncidentCommentsByIncident, idIncident);
            }, "GetIncidentComments", cancellationToken);
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
        #endregion
    }
}