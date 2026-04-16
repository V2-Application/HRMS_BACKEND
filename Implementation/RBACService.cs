using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using HRMSAPI.Services;
using HRMSAPI.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Roomsy.DTOS.GenericsResponses;
using System.Net;

namespace HRMSAPI.Implementation
{
    public class RBACService : IRBACService
    {
        private readonly HRMSContext _context;
        private readonly ILogger<StoreRoutingService> _logger;
        private readonly IPermissionNotificationService _permissionNotificationService;

        public RBACService(HRMSContext context, ILogger<StoreRoutingService> logger, IPermissionNotificationService permissionNotificationService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _permissionNotificationService = permissionNotificationService ?? throw new ArgumentNullException(nameof(permissionNotificationService));
        }

        public async Task<ExecuteAndReponse> UpsertModules(List<ModuleDto> moduleDtos)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var now = DateTime.Now;

                // === 0. Validate duplicates in payload (by name within the same parent) ===
                foreach (var moduleDto in moduleDtos)
                {
                    var duplicateSubs = moduleDto.SubModules
                        .GroupBy(sm => sm.SubModuleName, StringComparer.OrdinalIgnoreCase)
                        .Where(g => g.Count() > 1)
                        .Select(g => g.Key)
                        .ToList();
                    if (duplicateSubs.Any())
                        throw new Exception($"Module '{moduleDto.ModuleName}' has duplicate submodules: {string.Join(", ", duplicateSubs)}");

                    foreach (var sm in moduleDto.SubModules)
                    {
                        var duplicateActions = sm.Actions
                            .GroupBy(a => a.ActionName, StringComparer.OrdinalIgnoreCase)
                            .Where(g => g.Count() > 1)
                            .Select(g => g.Key)
                            .ToList();
                        if (duplicateActions.Any())
                            throw new Exception($"Submodule '{sm.SubModuleName}' in module '{moduleDto.ModuleName}' has duplicate actions: {string.Join(", ", duplicateActions)}");

                        foreach (var act in sm.Actions)
                        {
                            if (act.FurtherParts != null)
                            {
                                var duplicateParts = act.FurtherParts
                                    .GroupBy(p => p.ActionFurtherPartName, StringComparer.OrdinalIgnoreCase)
                                    .Where(g => g.Count() > 1)
                                    .Select(g => g.Key)
                                    .ToList();
                                if (duplicateParts.Any())
                                    throw new Exception($"Action '{act.ActionName}' in submodule '{sm.SubModuleName}' has duplicate further parts: {string.Join(", ", duplicateParts)}");
                            }
                        }
                    }
                }

                // Also ensure no duplicate module names within payload
                var duplicateModuleNames = moduleDtos
                    .GroupBy(m => m.ModuleName, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();
                if (duplicateModuleNames.Any())
                    throw new Exception($"Duplicate modules in payload: {string.Join(", ", duplicateModuleNames)}");

                // === 1A. Validate name uniqueness against DB (rollback on any conflict) ===
                // Modules
                var proposedModules = moduleDtos
                    .Select(m => new { m.Id, Name = (m.ModuleName ?? string.Empty).Trim() })
                    .ToList();
                var proposedModuleNamesLower = proposedModules.Select(m => m.Name.ToLower()).Distinct().ToList();
                if (proposedModuleNamesLower.Any())
                {
                    var dbModulesByName = await _context.ModuleMasters
                        .AsQueryable()
                        .Where(m => m.IsDeleted != true && proposedModuleNamesLower.Contains(m.ModuleName.ToLower()))
                        .Select(m => new { m.Id, m.ModuleName })
                    .ToListAsync();

                    foreach (var pm in proposedModules)
                    {
                        var conflict = dbModulesByName.Any(dbm => dbm.ModuleName.Equals(pm.Name, StringComparison.OrdinalIgnoreCase) && dbm.Id != pm.Id);
                        if (conflict)
                            throw new Exception($"Module name '{pm.Name}' already exists");
                    }
                }

                // SubModules (only for existing modules since new module has no DB siblings yet)
                foreach (var moduleDto in moduleDtos.Where(md => md.Id > 0))
                {
                    var subNames = moduleDto.SubModules.Select(sm => (sm.SubModuleName ?? string.Empty).Trim().ToLower()).Distinct().ToList();
                    if (!subNames.Any()) continue;

                    var dbSubs = await _context.SubModuleMasters
                        .AsQueryable()
                        .Where(sm => sm.IsDeleted != true && sm.ModuleId == moduleDto.Id && subNames.Contains(sm.SubModuleName.ToLower()))
                        .Select(sm => new { sm.Id, sm.SubModuleName })
                    .ToListAsync();

                    foreach (var subDto in moduleDto.SubModules)
                    {
                        var name = (subDto.SubModuleName ?? string.Empty).Trim();
                        var conflict = dbSubs.Any(db => db.SubModuleName.Equals(name, StringComparison.OrdinalIgnoreCase) && db.Id != subDto.Id);
                        if (conflict)
                            throw new Exception($"Submodule name '{name}' already exists in module '{moduleDto.ModuleName}'");
                    }
                }

                // Actions (only for existing submodules)
                foreach (var subDto in moduleDtos.SelectMany(m => m.SubModules).Where(s => s.Id > 0))
                {
                    var actionNames = subDto.Actions.Select(a => (a.ActionName ?? string.Empty).Trim().ToLower()).Distinct().ToList();
                    if (!actionNames.Any()) continue;

                    var dbActions = await _context.ActionMasters
                        .AsQueryable()
                        .Where(a => a.IsDeleted != true && a.SubModuleId == subDto.Id && actionNames.Contains(a.ActionName.ToLower()))
                        .Select(a => new { a.Id, a.ActionName })
                    .ToListAsync();

                    foreach (var actionDto in subDto.Actions)
                    {
                        var name = (actionDto.ActionName ?? string.Empty).Trim();
                        var conflict = dbActions.Any(db => db.ActionName.Equals(name, StringComparison.OrdinalIgnoreCase) && db.Id != actionDto.Id);
                        if (conflict)
                            throw new Exception($"Action name '{name}' already exists in submodule '{subDto.SubModuleName}'");
                    }
                }

                // Further parts (only for existing actions)
                foreach (var actionDto in moduleDtos.SelectMany(m => m.SubModules).SelectMany(s => s.Actions).Where(a => a.Id > 0))
                {
                    var partNames = (actionDto.FurtherParts ?? new List<FurtherPartDto>())
                        .Select(p => (p.ActionFurtherPartName ?? string.Empty).Trim().ToLower())
                        .Distinct()
                        .ToList();
                    if (!partNames.Any()) continue;

                    var dbParts = await _context.ActionFurtherParts
                        .AsQueryable()
                        .Where(p => p.IsDeleted != true && p.ActionId == actionDto.Id && partNames.Contains(p.ActionFurtherPartName.ToLower()))
                        .Select(p => new { p.Id, p.ActionFurtherPartName })
                    .ToListAsync();

                    foreach (var partDto in actionDto.FurtherParts ?? new List<FurtherPartDto>())
                    {
                        var name = (partDto.ActionFurtherPartName ?? string.Empty).Trim();
                        var conflict = dbParts.Any(db => db.ActionFurtherPartName.Equals(name, StringComparison.OrdinalIgnoreCase) && db.Id != partDto.Id);
                        if (conflict)
                            throw new Exception($"Further part name '{name}' already exists in action '{actionDto.ActionName}'");
                    }
                }

                // === 1. Fetch existing data by IDs from payload ===
                var payloadModuleIds = moduleDtos.Select(m => m.Id).Where(id => id > 0).Distinct().ToList();
                var payloadSubModuleIds = moduleDtos.SelectMany(m => m.SubModules)
                    .Select(sm => sm.Id).Where(id => id > 0).Distinct().ToList();
                var payloadActionIds = moduleDtos.SelectMany(m => m.SubModules)
                    .SelectMany(sm => sm.Actions)
                    .Select(a => a.Id).Where(id => id > 0).Distinct().ToList();
                var payloadPartIds = moduleDtos.SelectMany(m => m.SubModules)
                    .SelectMany(sm => sm.Actions)
                    .SelectMany(a => a.FurtherParts ?? new List<FurtherPartDto>())
                    .Select(p => p.Id).Where(id => id > 0).Distinct().ToList();

                var existingModules = payloadModuleIds.Any()
                    ? await _context.ModuleMasters.AsQueryable()
                        .Where(m => payloadModuleIds.Contains(m.Id))
                        .ToListAsync()
                    : new List<ModuleMaster>();
                var existingSubModules = payloadSubModuleIds.Any()
                    ? await _context.SubModuleMasters.AsQueryable()
                        .Where(s => payloadSubModuleIds.Contains(s.Id))
                        .ToListAsync()
                    : new List<SubModuleMaster>();
                var existingActions = payloadActionIds.Any()
                    ? await _context.ActionMasters.AsQueryable()
                        .Where(a => payloadActionIds.Contains(a.Id))
                        .ToListAsync()
                    : new List<ActionMaster>();
                var existingParts = payloadPartIds.Any()
                    ? await _context.ActionFurtherParts.AsQueryable()
                        .Where(p => payloadPartIds.Contains(p.Id))
                        .ToListAsync()
                    : new List<ActionFurtherPart>();

                var moduleById = existingModules.ToDictionary(m => m.Id);
                var subModuleById = existingSubModules.ToDictionary(s => s.Id);
                var actionById = existingActions.ToDictionary(a => a.Id);
                var partById = existingParts.ToDictionary(p => p.Id);

                // === 2. Prepare batches and mappings (to avoid per-entity SaveChanges) ===
                var newModules = new List<ModuleMaster>();
                var newSubModules = new List<SubModuleMaster>();
                var newActions = new List<ActionMaster>();
                var newParts = new List<ActionFurtherPart>();

                var dtoModuleToEntity = new Dictionary<ModuleDto, ModuleMaster>();
                var dtoSubModuleToEntity = new Dictionary<SubModuleDto, SubModuleMaster>();
                var dtoActionToEntity = new Dictionary<ActionDto, ActionMaster>();

                // === 3. Stage modules (updates and inserts) ===
                foreach (var moduleDto in moduleDtos)
                {
                    if (moduleDto.Id == 0)
                    {
                        var module = new ModuleMaster
                        {
                            ModuleName = moduleDto.ModuleName,
                            CreatedBy = moduleDto.CreatedBy,
                            CreatedOn = now,
                            IsActive = true,
                            IsDeleted = false
                        };
                        newModules.Add(module);
                        dtoModuleToEntity[moduleDto] = module;
                    }
                    else
                    {
                        if (!moduleById.TryGetValue(moduleDto.Id, out var module))
                            throw new Exception($"Module with Id {moduleDto.Id} not found");
                        module.ModuleName = moduleDto.ModuleName;
                        module.UpdatedBy = moduleDto.CreatedBy;
                        module.UpdatedOn = now;
                        module.IsActive = true;
                        module.IsDeleted = false;
                        // For updated modules, map so children can use the Id directly
                        dtoModuleToEntity[moduleDto] = module;
                    }
                }

                if (newModules.Any())
                {
                    _context.ModuleMasters.AddRange(newModules);
                    await _context.SaveChangesAsync();
                }

                // === 4. Stage submodules ===
                foreach (var moduleDto in moduleDtos)
                {
                    var parentModuleEntity = dtoModuleToEntity[moduleDto];
                    foreach (var subDto in moduleDto.SubModules)
                    {
                        if (subDto.Id == 0)
                        {
                            var subModule = new SubModuleMaster
                            {
                                SubModuleName = subDto.SubModuleName,
                                ModuleId = parentModuleEntity.Id,
                                CreatedBy = subDto.CreatedBy,
                                CreatedOn = now,
                                IsActive = true,
                                IsDeleted = false
                            };
                            newSubModules.Add(subModule);
                            dtoSubModuleToEntity[subDto] = subModule;
                        }
                        else
                        {
                            if (!subModuleById.TryGetValue(subDto.Id, out var subModule))
                                throw new Exception($"SubModule with Id {subDto.Id} not found");
                            subModule.SubModuleName = subDto.SubModuleName;
                            subModule.ModuleId = parentModuleEntity.Id;
                            subModule.UpdatedBy = subDto.CreatedBy;
                            subModule.UpdatedOn = now;
                            subModule.IsActive = true;
                            subModule.IsDeleted = false;
                            dtoSubModuleToEntity[subDto] = subModule;
                        }
                    }
                }

                if (newSubModules.Any())
                {
                    _context.SubModuleMasters.AddRange(newSubModules);
                    await _context.SaveChangesAsync();
                }

                // === 5. Stage actions ===
                foreach (var moduleDto in moduleDtos)
                {
                    foreach (var subDto in moduleDto.SubModules)
                    {
                        var parentSubModuleEntity = dtoSubModuleToEntity[subDto];
                        foreach (var actionDto in subDto.Actions)
                        {
                            if (actionDto.Id == 0)
                            {
                                var actionEntity = new ActionMaster
                                {
                                    ActionName = actionDto.ActionName,
                                    ModuleId = parentSubModuleEntity.ModuleId,
                                    SubModuleId = parentSubModuleEntity.Id,
                                    CreatedBy = actionDto.CreatedBy,
                                    CreatedOn = now,
                                    IsActive = true,
                                    IsDeleted = false
                                };
                                newActions.Add(actionEntity);
                                dtoActionToEntity[actionDto] = actionEntity;
                            }
                            else
                            {
                                if (!actionById.TryGetValue(actionDto.Id, out var actionEntity))
                                    throw new Exception($"Action with Id {actionDto.Id} not found");
                                actionEntity.ActionName = actionDto.ActionName;
                                actionEntity.ModuleId = parentSubModuleEntity.ModuleId;
                                actionEntity.SubModuleId = parentSubModuleEntity.Id;
                                actionEntity.UpdatedBy = actionDto.CreatedBy;
                                actionEntity.UpdatedOn = now;
                                actionEntity.IsActive = true;
                                actionEntity.IsDeleted = false;
                                dtoActionToEntity[actionDto] = actionEntity;
                            }
                        }
                    }
                }

                if (newActions.Any())
                {
                    _context.ActionMasters.AddRange(newActions);
                    await _context.SaveChangesAsync();
                }

                // === 6. Stage further parts ===
                foreach (var moduleDto in moduleDtos)
                {
                    foreach (var subDto in moduleDto.SubModules)
                    {
                        foreach (var actionDto in subDto.Actions)
                        {
                            var parentActionEntity = dtoActionToEntity[actionDto];
                            if (actionDto.FurtherParts == null) continue;

                            foreach (var partDto in actionDto.FurtherParts)
                            {
                                if (partDto.Id == 0)
                                {
                                    var partEntity = new ActionFurtherPart
                                    {
                                        ActionFurtherPartName = partDto.ActionFurtherPartName,
                                        ModuleId = parentActionEntity.ModuleId,
                                        SubModuleId = parentActionEntity.SubModuleId,
                                        ActionId = parentActionEntity.Id,
                                        CreatedBy = partDto.CreatedBy,
                                        CreatedOn = now,
                                        IsActive = true,
                                        IsDeleted = false
                                    };
                                    newParts.Add(partEntity);
                                }
                                else
                                {
                                    if (!partById.TryGetValue(partDto.Id, out var partEntity))
                                        throw new Exception($"FurtherPart with Id {partDto.Id} not found");
                                    partEntity.ActionFurtherPartName = partDto.ActionFurtherPartName;
                                    partEntity.ModuleId = parentActionEntity.ModuleId;
                                    partEntity.SubModuleId = parentActionEntity.SubModuleId;
                                    partEntity.ActionId = parentActionEntity.Id;
                                    partEntity.UpdatedBy = partDto.CreatedBy;
                                    partEntity.UpdatedOn = now;
                                    partEntity.IsActive = true;
                                    partEntity.IsDeleted = false;
                                }
                            }
                        }
                    }
                }

                if (newParts.Any())
                {
                    _context.ActionFurtherParts.AddRange(newParts);
                    await _context.SaveChangesAsync();
                }

                // Persist any remaining updates
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Notify all users about module structure changes
                await _permissionNotificationService.NotifyAllUsersAsync("ModuleStructureChanged");

                return new ExecuteAndReponse
                {
                    Status = true,
                    Message = "Modules hierarchy upserted successfully by IDs",
                    Code = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.BadRequest
                };
            }
        }

        public async Task<FetchAndResponse> GetRbacHierarchyAsync()
        {
            try
            {
                var flatData = await _context.vw_RBACHierarchies.ToListAsync();

                if (flatData == null || flatData.Count < 1)
                    throw new Exception("No Data Found");

                var result = flatData
                .GroupBy(r => new { r.RoleId, r.RoleName })
                .Select(roleGrp => new
                {
                    roleGrp.Key.RoleId,
                    roleGrp.Key.RoleName,
                    Modules = roleGrp
                        .Where(m => m.ModuleRefId != null && m.ModuleRefId > 0)
                        .GroupBy(m => new { m.ModuleId, m.ModuleRefId, m.ModuleName, m.ModuleStatus })
                        .Select(modGrp => new
                        {
                            modGrp.Key.ModuleId,
                            modGrp.Key.ModuleRefId,
                            modGrp.Key.ModuleName,
                            ModuleStatus = modGrp.Key.ModuleStatus,
                            SubModules = modGrp
                                .Where(sm => sm.SubModuleRefId != null && sm.SubModuleRefId > 0) // only include existing submodules by RefId
                                .GroupBy(sm => new { sm.SubModuleId, sm.SubModuleRefId, sm.SubModuleName, sm.SubModuleStatus })
                                .Select(subGrp => new
                                {
                                    subGrp.Key.SubModuleId,
                                    subGrp.Key.SubModuleRefId,
                                    subGrp.Key.SubModuleName,
                                    SubModuleStatus = subGrp.Key.SubModuleStatus,
                                    Actions = subGrp
                                        .Where(a => a.ActionRefId != null && a.ActionRefId > 0) // only include existing actions by RefId
                                        .GroupBy(a => new { a.ActionId, a.ActionRefId, a.ActionName, a.ActionStatus })
                                        .Select(actGrp => new
                                        {
                                            actGrp.Key.ActionId,
                                            actGrp.Key.ActionRefId,
                                            actGrp.Key.ActionName,
                                            ActionStatus = actGrp.Key.ActionStatus,
                                            FurtherParts = actGrp
                                                .Where(fp => fp.ActionFurtherPartRefId != null && fp.ActionFurtherPartRefId > 0)
                                                .GroupBy(a => new { a.ActionFurtherPartId, a.ActionFurtherPartRefId, a.ActionFurtherPartName, a.FurtherPartStatus })
                                                .Select(fp => new
                                                {
                                                    fp.Key.ActionFurtherPartId,
                                                    fp.Key.ActionFurtherPartRefId,
                                                    fp.Key.ActionFurtherPartName,
                                                    FurtherPartStatus = fp.Key.FurtherPartStatus
                                                }).ToList()
                                        }).ToList()
                                }).ToList()
                        }).ToList()
                }).ToList();

                return new FetchAndResponse
                {
                    Status = true,
                    Message = "Fetched Successfully",
                    Data = result,
                    Code = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                return new FetchAndResponse
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.OK
                };
            }
        }

        public async Task<ExecuteAndReponse> UpsertRbacNodes(List<RolePermissionPost> postrequest)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Collect all existing IDs to fetch in bulk
                var allModuleIds = postrequest.SelectMany(r => r.Modules ?? Enumerable.Empty<ModuleRolePermissionPostDto>())
                    .Where(m => m.ModuleId != 0)
                    .Select(m => m.ModuleId)
                    .Distinct()
                    .ToList();

                var allSubModuleIds = postrequest.SelectMany(r => r.Modules ?? Enumerable.Empty<ModuleRolePermissionPostDto>())
                    .SelectMany(m => m.SubModules ?? Enumerable.Empty<SubModuleRolePermissionPostDto>())
                    .Where(sm => sm.SubModuleId != 0)
                    .Select(sm => sm.SubModuleId)
                    .Distinct()
                    .ToList();

                var allActionIds = postrequest.SelectMany(r => r.Modules ?? Enumerable.Empty<ModuleRolePermissionPostDto>())
                    .SelectMany(m => m.SubModules ?? Enumerable.Empty<SubModuleRolePermissionPostDto>())
                    .SelectMany(sm => sm.Actions ?? Enumerable.Empty<ActionRolePermissionPostDto>())
                    .Where(a => a.ActionId != 0)
                    .Select(a => a.ActionId)
                    .Distinct()
                    .ToList();

                var allFurtherPartIds = postrequest.SelectMany(r => r.Modules ?? Enumerable.Empty<ModuleRolePermissionPostDto>())
                    .SelectMany(m => m.SubModules ?? Enumerable.Empty<SubModuleRolePermissionPostDto>())
                    .SelectMany(sm => sm.Actions ?? Enumerable.Empty<ActionRolePermissionPostDto>())
                    .SelectMany(a => a.FurtherParts ?? Enumerable.Empty<FurtherPartRolePermissionPostDto>())
                    .Where(fp => fp.ActionFurtherPartId != 0)
                    .Select(fp => fp.ActionFurtherPartId)
                    .Distinct()
                    .ToList();

                // Bulk fetch existing entities
                var existingModules = allModuleIds.Any()
                    ? await _context.RBACNodes
                        .AsQueryable()
                        .Where(n => allModuleIds.Contains(n.Id) && n.NodeType == "Module")
                        .ToDictionaryAsync(n => n.Id, n => n)
                    : new Dictionary<int, RBACNode>();

                var existingSubModules = allSubModuleIds.Any()
                    ? await _context.RBACNodes
                        .AsQueryable()
                        .Where(n => allSubModuleIds.Contains(n.Id) && n.NodeType == "SubModule")
                        .ToDictionaryAsync(n => n.Id, n => n)
                    : new Dictionary<int, RBACNode>();

                var existingActions = allActionIds.Any()
                    ? await _context.RBACNodes
                        .AsQueryable()
                        .Where(n => allActionIds.Contains(n.Id) && n.NodeType == "Action")
                        .ToDictionaryAsync(n => n.Id, n => n)
                    : new Dictionary<int, RBACNode>();

                var existingFurtherParts = allFurtherPartIds.Any()
                    ? await _context.RBACNodes
                        .AsQueryable()
                        .Where(n => allFurtherPartIds.Contains(n.Id) && n.NodeType == "FurtherPart")
                        .ToDictionaryAsync(n => n.Id, n => n)
                    : new Dictionary<int, RBACNode>();

                var now = DateTime.Now;
                var entitiesToAdd = new List<RBACNode>();
                var entitiesToUpdate = new List<RBACNode>();
                var newNodeParentMap = new Dictionary<RBACNode, RBACNode>();

                foreach (var role in postrequest)
                {
                    var roleId = role.RoleId;

                    foreach (var module in role.Modules ?? Enumerable.Empty<ModuleRolePermissionPostDto>())
                    {
                        RBACNode moduleEntry;

                        if (module.ModuleId == 0)
                        {
                            // Skip placeholder if no RefId provided or RefId <= 0
                            if (!(module.ModuleRefId.HasValue && module.ModuleRefId.Value > 0)) continue;
                            moduleEntry = new RBACNode
                            {
                                RoleId = roleId,
                                NodeType = "Module",
                                RefId = module.ModuleRefId.Value,
                                ParentNodeId = 0,
                                IsChecked = module.ModuleStatus,
                                CreatedBy = "System",
                                CreatedOn = now
                            };
                            entitiesToAdd.Add(moduleEntry);
                        }
                        else
                        {
                            if (!existingModules.TryGetValue(module.ModuleId, out moduleEntry))
                            {
                                throw new Exception($"Module Id {module.ModuleId} is not correct.");
                            }

                            moduleEntry.IsChecked = module.ModuleStatus;
                            moduleEntry.UpdatedOn = now;
                            moduleEntry.UpdatedBy = "System";
                            entitiesToUpdate.Add(moduleEntry);
                        }

                        // Process SubModules
                        if (module.SubModules != null)
                        {
                            foreach (var subModule in module.SubModules ?? Enumerable.Empty<SubModuleRolePermissionPostDto>())
                            {
                                RBACNode subModuleEntry;

                                if (subModule.SubModuleId == 0)
                                {
                                    // Skip placeholder if no RefId provided or RefId <= 0
                                    if (!(subModule.SubModuleRefId.HasValue && subModule.SubModuleRefId.Value > 0)) continue;
                                    subModuleEntry = new RBACNode
                                    {
                                        RoleId = roleId,
                                        NodeType = "SubModule",
                                        RefId = subModule.SubModuleRefId.Value,
                                        ParentNodeId = 0,
                                        IsChecked = subModule.SubModuleStatus,
                                        CreatedBy = "System",
                                        CreatedOn = now
                                    };
                                    entitiesToAdd.Add(subModuleEntry);
                                    newNodeParentMap[subModuleEntry] = moduleEntry;
                                }
                                else
                                {
                                    if (!existingSubModules.TryGetValue(subModule.SubModuleId, out subModuleEntry))
                                    {
                                        throw new Exception($"SubModule Id {subModule.SubModuleId} is not correct.");
                                    }

                                    subModuleEntry.IsChecked = subModule.SubModuleStatus;
                                    subModuleEntry.UpdatedOn = now;
                                    subModuleEntry.UpdatedBy = "System";
                                    entitiesToUpdate.Add(subModuleEntry);
                                }

                                // Process Actions
                                if (subModule.Actions != null)
                                {
                                    foreach (var action in subModule.Actions ?? Enumerable.Empty<ActionRolePermissionPostDto>())
                                    {
                                        RBACNode actionEntry;

                                        if (action.ActionId == 0)
                                        {
                                            // Skip placeholder if no RefId provided or RefId <= 0
                                            if (!(action.ActionRefId.HasValue && action.ActionRefId.Value > 0)) continue;
                                            actionEntry = new RBACNode
                                            {
                                                RoleId = roleId,
                                                NodeType = "Action",
                                                RefId = action.ActionRefId.Value,
                                                ParentNodeId = 0,
                                                IsChecked = action.ActionStatus,
                                                CreatedBy = "System",
                                                CreatedOn = now
                                            };
                                            entitiesToAdd.Add(actionEntry);
                                            newNodeParentMap[actionEntry] = subModuleEntry;
                                        }
                                        else
                                        {
                                            if (!existingActions.TryGetValue(action.ActionId, out actionEntry))
                                            {
                                                throw new Exception($"Action Id {action.ActionId} is not correct.");
                                            }

                                            actionEntry.IsChecked = action.ActionStatus;
                                            actionEntry.UpdatedOn = now;
                                            actionEntry.UpdatedBy = "System";
                                            entitiesToUpdate.Add(actionEntry);
                                        }

                                        // Process Further Parts
                                        if (action.FurtherParts != null)
                                        {
                                            foreach (var furtherPart in action.FurtherParts ?? Enumerable.Empty<FurtherPartRolePermissionPostDto>())
                                            {
                                                if (furtherPart.ActionFurtherPartId == 0)
                                                {
                                                    // Skip placeholder if no RefId provided or RefId <= 0
                                                    if (!(furtherPart.ActionFurtherPartRefId.HasValue && furtherPart.ActionFurtherPartRefId.Value > 0)) continue;
                                                    var furtherPartEntry = new RBACNode
                                                    {
                                                        RoleId = roleId,
                                                        NodeType = "FurtherPart",
                                                        RefId = furtherPart.ActionFurtherPartRefId.Value,
                                                        ParentNodeId = 0,
                                                        IsChecked = furtherPart.FurtherPartStatus,
                                                        CreatedBy = "System",
                                                        CreatedOn = now
                                                    };
                                                    entitiesToAdd.Add(furtherPartEntry);
                                                    newNodeParentMap[furtherPartEntry] = actionEntry;
                                                }
                                                else
                                                {
                                                    if (!existingFurtherParts.TryGetValue(furtherPart.ActionFurtherPartId, out var furtherPartEntry))
                                                    {
                                                        throw new Exception($"Further Part Id {furtherPart.ActionFurtherPartId} is not correct.");
                                                    }

                                                    furtherPartEntry.IsChecked = furtherPart.FurtherPartStatus;
                                                    furtherPartEntry.UpdatedOn = now;
                                                    furtherPartEntry.UpdatedBy = "System";
                                                    entitiesToUpdate.Add(furtherPartEntry);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // Bulk add new entities
                if (entitiesToAdd.Any())
                {
                    await _context.RBACNodes.AddRangeAsync(entitiesToAdd);
                    await _context.SaveChangesAsync();

                    // Update parent IDs for newly created entities using in-memory parent map
                    foreach (var kvp in newNodeParentMap)
                    {
                        var child = kvp.Key;
                        var parent = kvp.Value;
                        child.ParentNodeId = parent.Id;
                    }

                    await _context.SaveChangesAsync();
                }

                // Bulk update existing entities
                if (entitiesToUpdate.Any())
                {
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                // Notify all users about permission changes
                var affectedRoleIds = postrequest.Select(r => r.RoleId).Distinct().ToList();
                foreach (var roleId in affectedRoleIds)
                {
                    await _permissionNotificationService.NotifyPermissionChangeAsync(roleId, "PermissionsUpdated");
                }

                return new ExecuteAndReponse
                {
                    Status = true,
                    Message = "RBAC nodes upserted successfully",
                    Code = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.BadRequest
                };
            }
        }

        public async Task<FetchAndResponse> GetModulesForUpsertAsync()
        {
            try
            {
                var modules = await _context.ModuleMasters
                    .AsQueryable()
                    .Where(m => m.IsDeleted != true)
                    .ToListAsync();

                var subModules = await _context.SubModuleMasters
                    .AsQueryable()
                    .Where(sm => sm.IsDeleted != true)
                    .ToListAsync();

                var actions = await _context.ActionMasters
                    .AsQueryable()
                    .Where(a => a.IsDeleted != true)
                    .ToListAsync();

                var parts = await _context.ActionFurtherParts
                    .AsQueryable()
                    .Where(p => p.IsDeleted != true)
                    .ToListAsync();

                var result = modules
                    .Select(m => new ModuleDto
                    {
                        Id = m.Id,
                        ModuleName = m.ModuleName,
                        CreatedBy = m.CreatedBy,
                        SubModules = subModules
                            .Where(sm => sm.ModuleId == m.Id)
                            .Select(sm => new SubModuleDto
                            {
                                Id = sm.Id,
                                SubModuleName = sm.SubModuleName,
                                CreatedBy = sm.CreatedBy,
                                Actions = actions
                                    .Where(a => a.SubModuleId == sm.Id)
                                    .Select(a => new ActionDto
                                    {
                                        Id = a.Id,
                                        ActionName = a.ActionName,
                                        CreatedBy = a.CreatedBy,
                                        FurtherParts = parts
                                            .Where(p => p.ActionId == a.Id)
                                            .Select(p => new FurtherPartDto
                                            {
                                                Id = p.Id,
                                                ActionFurtherPartName = p.ActionFurtherPartName,
                                                CreatedBy = p.CreatedBy
                                            })
                                            .ToList()
                                    })
                                    .ToList()
                            })
                            .ToList()
                    })
                    .ToList();

                return new FetchAndResponse
                {
                    Status = true,
                    Message = "Fetched modules for upsert",
                    Data = result,
                    Code = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                return new FetchAndResponse
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.BadRequest
                };
            }
        }

        public async Task<ExecuteAndReponse> DeleteModuleAsync(int id)
        {
            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var module = await _context.ModuleMasters.AsQueryable().FirstOrDefaultAsync(m => m.Id == id);
                if (module == null) throw new Exception($"Module {id} not found");
                module.IsDeleted = true; module.IsActive = false; module.UpdatedOn = DateTime.Now; module.UpdatedBy = "System";

                var subModules = await _context.SubModuleMasters.AsQueryable().Where(sm => sm.ModuleId == id).ToListAsync();
                foreach (var sm in subModules)
                {
                    sm.IsDeleted = true; sm.IsActive = false; sm.UpdatedOn = DateTime.Now; sm.UpdatedBy = "System";
                }

                var subModuleIds = subModules.Select(sm => sm.Id).ToList();
                var actions = await _context.ActionMasters.AsQueryable().Where(a => subModuleIds.Contains((int)a.SubModuleId)).ToListAsync();
                foreach (var a in actions)
                {
                    a.IsDeleted = true; a.IsActive = false; a.UpdatedOn = DateTime.Now; a.UpdatedBy = "System";
                }

                var actionIds = actions.Select(a => a.Id).ToList();
                var parts = await _context.ActionFurtherParts.AsQueryable().Where(p => actionIds.Contains((int)p.ActionId)).ToListAsync();
                foreach (var p in parts)
                {
                    p.IsDeleted = true; p.IsActive = false; p.UpdatedOn = DateTime.Now; p.UpdatedBy = "System";
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return new ExecuteAndReponse { Status = true, Message = "Module deleted", Code = HttpStatusCode.OK };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return new ExecuteAndReponse { Status = false, Message = ex.Message, Code = HttpStatusCode.BadRequest };
            }
        }

        public async Task<ExecuteAndReponse> DeleteSubModuleAsync(int id)
        {
            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var subModule = await _context.SubModuleMasters.AsQueryable().FirstOrDefaultAsync(sm => sm.Id == id);
                if (subModule == null) throw new Exception($"SubModule {id} not found");
                subModule.IsDeleted = true; subModule.IsActive = false; subModule.UpdatedOn = DateTime.Now; subModule.UpdatedBy = "System";

                var actions = await _context.ActionMasters.AsQueryable().Where(a => a.SubModuleId == id).ToListAsync();
                foreach (var a in actions)
                {
                    a.IsDeleted = true; a.IsActive = false; a.UpdatedOn = DateTime.Now; a.UpdatedBy = "System";
                }

                var actionIds = actions.Select(a => a.Id).ToList();
                var parts = await _context.ActionFurtherParts.AsQueryable().Where(p => actionIds.Contains((int)p.ActionId)).ToListAsync();
                foreach (var p in parts)
                {
                    p.IsDeleted = true; p.IsActive = false; p.UpdatedOn = DateTime.Now; p.UpdatedBy = "System";
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return new ExecuteAndReponse { Status = true, Message = "SubModule deleted", Code = HttpStatusCode.OK };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return new ExecuteAndReponse { Status = false, Message = ex.Message, Code = HttpStatusCode.BadRequest };
            }
        }

        public async Task<ExecuteAndReponse> DeleteActionAsync(int id)
        {
            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var action = await _context.ActionMasters.AsQueryable().FirstOrDefaultAsync(a => a.Id == id);
                if (action == null) throw new Exception($"Action {id} not found");
                action.IsDeleted = true; action.IsActive = false; action.UpdatedOn = DateTime.Now; action.UpdatedBy = "System";

                var parts = await _context.ActionFurtherParts.AsQueryable().Where(p => p.ActionId == id).ToListAsync();
                foreach (var p in parts)
                {
                    p.IsDeleted = true; p.IsActive = false; p.UpdatedOn = DateTime.Now; p.UpdatedBy = "System";
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return new ExecuteAndReponse { Status = true, Message = "Action deleted", Code = HttpStatusCode.OK };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return new ExecuteAndReponse { Status = false, Message = ex.Message, Code = HttpStatusCode.BadRequest };
            }
        }

        public async Task<ExecuteAndReponse> DeleteFurtherPartAsync(int id)
        {
            try
            {
                var part = await _context.ActionFurtherParts.AsQueryable().FirstOrDefaultAsync(p => p.Id == id);
                if (part == null) throw new Exception($"FurtherPart {id} not found");
                part.IsDeleted = true; part.IsActive = false; part.UpdatedOn = DateTime.Now; part.UpdatedBy = "System";
                await _context.SaveChangesAsync();
                return new ExecuteAndReponse { Status = true, Message = "FurtherPart deleted", Code = HttpStatusCode.OK };
            }
            catch (Exception ex)
            {
                return new ExecuteAndReponse { Status = false, Message = ex.Message, Code = HttpStatusCode.BadRequest };
            }
        }

        public async Task<ExecuteAndReponse> UpsertRoleAsync(RoleDto roleDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(roleDto.RoleName))
                    throw new Exception("Role name cannot be empty");

                var now = DateTime.Now;
                var roleName = roleDto.RoleName.Trim();

                if (roleDto.Id == 0)
                {
                    // Create new role - check if name already exists
                    var existingRole = await _context.tblRoles
                        .AsQueryable()
                        .Where(r => r.RoleName == roleName)
                        .FirstOrDefaultAsync();

                    if (existingRole != null)
                        throw new Exception($"Role name '{roleName}' already exists");

                    var newRole = new tblRole
                    {
                        RoleName = roleName,
                        Description = roleDto.Description?.Trim(),
                        CreatedBy = roleDto.CreatedBy ?? "System",
                        CreatedOn = now,
                        IsActive = true
                    };

                    _context.tblRoles.Add(newRole);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // Notify all users about new role
                    await _permissionNotificationService.NotifyAllUsersAsync("RoleCreated");

                    return new ExecuteAndReponse
                    {
                        Status = true,
                        Message = "Role created successfully",
                        Code = HttpStatusCode.OK
                    };
                }
                else
                {
                    // Update existing role
                    var existingRole = await _context.tblRoles
                        .AsQueryable()
                        .FirstOrDefaultAsync(r => r.RoleId == roleDto.Id);

                    if (existingRole == null)
                        throw new Exception($"Role with Id {roleDto.Id} not found");

                    // Check if new name conflicts with other roles (excluding current role)
                    var nameConflict = await _context.tblRoles
                        .AsQueryable()
                        .Where(r => r.RoleId != roleDto.Id && r.RoleName == roleName)
                        .FirstOrDefaultAsync();

                    if (nameConflict != null)
                        throw new Exception($"Role name '{roleName}' already exists");

                    // Update role
                    existingRole.RoleName = roleName;
                    existingRole.Description = roleDto.Description?.Trim();
                    existingRole.LastUpdatedBy = roleDto.CreatedBy ?? "System";
                    existingRole.IsActive = true;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // Notify all users about role update
                    await _permissionNotificationService.NotifyAllUsersAsync("RoleUpdated");

                    return new ExecuteAndReponse
                    {
                        Status = true,
                        Message = "Role updated successfully",
                        Code = HttpStatusCode.OK
                    };
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.BadRequest
                };
            }
        }

        public async Task<ExecuteAndReponse> DeleteRoleAsync(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var role = await _context.tblRoles
                    .AsQueryable()
                    .FirstOrDefaultAsync(r => r.RoleId == id);

                if (role == null)
                    throw new Exception($"Role with Id {id} not found");

                // Soft delete the role
                role.IsActive = false;

                // Also soft delete related RBAC nodes if they exist
                //var rbacNodes = await _context.RBACNodes
                //	.AsQueryable()
                //	.Where(rn => rn.RoleId == id)
                //	.ToListAsync();

                //foreach (var node in rbacNodes)
                //{
                //	node. = false;
                //	node.IsDeleted = true;
                //	node.UpdatedOn = DateTime.Now;
                //	node.UpdatedBy = "System";
                //}

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Notify all users about role deletion
                await _permissionNotificationService.NotifyAllUsersAsync("RoleDeleted");

                return new ExecuteAndReponse
                {
                    Status = true,
                    Message = "Role deleted successfully",
                    Code = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.BadRequest
                };
            }
        }

        public async Task<ExecuteAndReponse> UpsertEmployeeRoleAsync(EmployeeRoleDto employeeRoleDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Validate employee exists
                var employee = await _context.tblEmployees
                    .AsQueryable()
                    .FirstOrDefaultAsync(e => e.EmployeeId == employeeRoleDto.EmployeeId);

                if (employee == null)
                    throw new Exception($"Employee with Id {employeeRoleDto.EmployeeId} not found");

                // Validate role exists
                var role = await _context.tblRoles
                    .AsQueryable()
                    .FirstOrDefaultAsync(r => r.RoleId == employeeRoleDto.RoleId);

                if (role == null)
                    throw new Exception($"Role with Id {employeeRoleDto.RoleId} not found");

                // Check if employee already has this role
                var existingEmployeeRole = await _context.tblEmployeeRoles
                    .AsQueryable()
                    .FirstOrDefaultAsync(er => er.EmployeeId == employeeRoleDto.EmployeeId && er.RoleId == employeeRoleDto.RoleId);

                if (existingEmployeeRole != null)
                {
                    // Update existing role assignment
                    existingEmployeeRole.LastUpdatedBy = employeeRoleDto.LastUpdatedBy ?? employeeRoleDto.AssignedBy ?? "System";
                    existingEmployeeRole.LastUpdatedOn = DateTime.Now;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return new ExecuteAndReponse
                    {
                        Status = true,
                        Message = "Employee role updated successfully",
                        Code = HttpStatusCode.OK
                    };
                }
                else
                {
                    // Create new role assignment
                    var newEmployeeRole = new tblEmployeeRole
                    {
                        EmployeeId = employeeRoleDto.EmployeeId,
                        RoleId = employeeRoleDto.RoleId,
                        AssignedBy = employeeRoleDto.AssignedBy ?? "System",
                        AssignedOn = DateTime.Now,
                        LastUpdatedBy = employeeRoleDto.LastUpdatedBy ?? employeeRoleDto.AssignedBy ?? "System",
                        LastUpdatedOn = DateTime.Now
                    };

                    _context.tblEmployeeRoles.Add(newEmployeeRole);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return new ExecuteAndReponse
                    {
                        Status = true,
                        Message = "Employee role assigned successfully",
                        Code = HttpStatusCode.OK
                    };
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.BadRequest
                };
            }
        }

        public async Task<FetchAndResponse> GetEmployeeRolesAsync(long employeeId)
        {
            try
            {
                // Validate employee exists
                var employee = await _context.tblEmployees
                    .AsQueryable()
                    .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);

                if (employee == null)
                    throw new Exception($"Employee with Id {employeeId} not found");

                // Get employee roles with role details
                var employeeRoles = await _context.tblEmployeeRoles
                    .AsQueryable()
                    .Where(er => er.EmployeeId == employeeId)
                    .Join(_context.tblRoles,
                        er => er.RoleId,
                        r => r.RoleId,
                        (er, r) => new EmployeeRoleResponseDto
                        {
                            EmployeeRoleId = er.EmployeeRoleId,
                            EmployeeId = er.EmployeeId,
                            RoleId = er.RoleId,
                            RoleName = r.RoleName,
                            RoleDescription = r.Description,
                            AssignedOn = er.AssignedOn,
                            AssignedBy = er.AssignedBy,
                            LastUpdatedOn = er.LastUpdatedOn,
                            LastUpdatedBy = er.LastUpdatedBy
                        })
                    .ToListAsync();

                return new FetchAndResponse
                {
                    Status = true,
                    Message = $"Found {employeeRoles.Count} role(s) for employee {employeeId}",
                    Data = employeeRoles,
                    Code = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                return new FetchAndResponse
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.BadRequest
                };
            }
        }

        public async Task<ExecuteAndReponse> DeleteEmployeeRoleAsync(long employeeId, int roleId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Validate employee exists
                var employee = await _context.tblEmployees
                    .AsQueryable()
                    .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);

                if (employee == null)
                    throw new Exception($"Employee with Id {employeeId} not found");

                // Validate role exists
                var role = await _context.tblRoles
                    .AsQueryable()
                    .FirstOrDefaultAsync(r => r.RoleId == roleId);

                if (role == null)
                    throw new Exception($"Role with Id {roleId} not found");

                // Find and delete the employee role assignment
                var employeeRole = await _context.tblEmployeeRoles
                    .AsQueryable()
                    .FirstOrDefaultAsync(er => er.EmployeeId == employeeId && er.RoleId == roleId);

                if (employeeRole == null)
                    throw new Exception($"Employee {employeeId} does not have role {roleId} assigned");

                _context.tblEmployeeRoles.Remove(employeeRole);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return new ExecuteAndReponse
                {
                    Status = true,
                    Message = $"Role {role.RoleName} removed from employee {employeeId} successfully",
                    Code = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.BadRequest
                };
            }
        }
    }
}
