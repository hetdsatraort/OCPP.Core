using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Management.Models.HardwareMaster;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace OCPP.Core.Management.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HardwareMasterController : ControllerBase
    {
        private readonly OCPPCoreContext _dbContext;
        private readonly ILogger<HardwareMasterController> _logger;

        public HardwareMasterController(
            OCPPCoreContext dbContext,
            ILogger<HardwareMasterController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        #region Charger Type Management

        [HttpPost("charger-type-add")]
        [Authorize]
        public async Task<IActionResult> AddChargerType([FromBody] ChargerTypeRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Ok(new HardwareMasterResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                var chargerType = new Database.EVCDTO.ChargerTypeMaster
                {
                    RecId = Guid.NewGuid().ToString(),
                    ChargerType = request.ChargerType,
                    ChargerTypeImage = request.ChargerTypeImage,
                    Additional_Info_1 = request.AdditionalInfo1,
                    Active = 1,
                    CreatedOn = DateTime.UtcNow,
                    UpdatedOn = DateTime.UtcNow
                };

                _dbContext.ChargerTypeMasters.Add(chargerType);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Charger type added: {chargerType.RecId} - {chargerType.ChargerType}");

                return Ok(new HardwareMasterResponseDto
                {
                    Success = true,
                    Message = "Charger type added successfully",
                    Data = MapToChargerTypeDto(chargerType)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding charger type");
                return Ok(new HardwareMasterResponseDto
                {
                    Success = false,
                    Message = "An error occurred while adding charger type"
                });
            }
        }

        [HttpPut("charger-type-update")]
        [Authorize]
        public async Task<IActionResult> UpdateChargerType([FromBody] ChargerTypeUpdateDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Ok(new HardwareMasterResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                var chargerType = await _dbContext.ChargerTypeMasters
                    .FirstOrDefaultAsync(c => c.RecId == request.RecId);

                if (chargerType == null)
                {
                    return Ok(new HardwareMasterResponseDto
                    {
                        Success = false,
                        Message = "Charger type not found"
                    });
                }

                chargerType.ChargerType = request.ChargerType;
                chargerType.ChargerTypeImage = request.ChargerTypeImage;
                chargerType.Additional_Info_1 = request.AdditionalInfo1;
                chargerType.Active = request.Active;
                chargerType.UpdatedOn = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Charger type updated: {chargerType.RecId}");

                return Ok(new HardwareMasterResponseDto
                {
                    Success = true,
                    Message = "Charger type updated successfully",
                    Data = MapToChargerTypeDto(chargerType)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating charger type");
                return Ok(new HardwareMasterResponseDto
                {
                    Success = false,
                    Message = "An error occurred while updating charger type"
                });
            }
        }

        [HttpDelete("charger-type-delete/{recId}")]
        [Authorize]
        public async Task<IActionResult> DeleteChargerType(string recId)
        {
            try
            {
                var chargerType = await _dbContext.ChargerTypeMasters
                    .FirstOrDefaultAsync(c => c.RecId == recId);

                if (chargerType == null)
                {
                    return Ok(new HardwareMasterResponseDto
                    {
                        Success = false,
                        Message = "Charger type not found"
                    });
                }

                chargerType.Active = 0;
                chargerType.UpdatedOn = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Charger type soft deleted: {recId}");

                return Ok(new HardwareMasterResponseDto
                {
                    Success = true,
                    Message = "Charger type deleted successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting charger type");
                return Ok(new HardwareMasterResponseDto
                {
                    Success = false,
                    Message = "An error occurred while deleting charger type"
                });
            }
        }

        [HttpGet("charger-type-list")]
        public async Task<IActionResult> GetChargerTypeList()
        {
            try
            {
                var chargerTypes = await _dbContext.ChargerTypeMasters
                    .Where(c => c.Active == 1)
                    .OrderBy(c => c.ChargerType)
                    .ToListAsync();

                var result = chargerTypes.Select(MapToChargerTypeDto).ToList();

                return Ok(new HardwareMasterResponseDto
                {
                    Success = true,
                    Message = "Charger types retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving charger type list");
                return Ok(new HardwareMasterResponseDto
                {
                    Success = false,
                    Message = "An error occurred while retrieving charger types"
                });
            }
        }

        #endregion

        #region Battery Type Management

        [HttpPost("battery-type-add")]
        [Authorize]
        public async Task<IActionResult> AddBatteryType([FromBody] BatteryTypeRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Ok(new HardwareMasterResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                var batteryType = new Database.EVCDTO.BatteryTypeMaster
                {
                    RecId = Guid.NewGuid().ToString(),
                    BatteryType = request.BatteryType,
                    Active = 1,
                    CreatedOn = DateTime.UtcNow,
                    UpdatedOn = DateTime.UtcNow
                };

                _dbContext.BatteryTypeMasters.Add(batteryType);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Battery type added: {batteryType.RecId} - {batteryType.BatteryType}");

                return Ok(new HardwareMasterResponseDto
                {
                    Success = true,
                    Message = "Battery type added successfully",
                    Data = MapToBatteryTypeDto(batteryType)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding battery type");
                return Ok(new HardwareMasterResponseDto
                {
                    Success = false,
                    Message = "An error occurred while adding battery type"
                });
            }
        }

        [HttpPut("battery-type-update")]
        [Authorize]
        public async Task<IActionResult> UpdateBatteryType([FromBody] BatteryTypeUpdateDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Ok(new HardwareMasterResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                var batteryType = await _dbContext.BatteryTypeMasters
                    .FirstOrDefaultAsync(b => b.RecId == request.RecId);

                if (batteryType == null)
                {
                    return Ok(new HardwareMasterResponseDto
                    {
                        Success = false,
                        Message = "Battery type not found"
                    });
                }

                batteryType.BatteryType = request.BatteryType;
                batteryType.Active = request.Active;
                batteryType.UpdatedOn = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Battery type updated: {batteryType.RecId}");

                return Ok(new HardwareMasterResponseDto
                {
                    Success = true,
                    Message = "Battery type updated successfully",
                    Data = MapToBatteryTypeDto(batteryType)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating battery type");
                return Ok(new HardwareMasterResponseDto
                {
                    Success = false,
                    Message = "An error occurred while updating battery type"
                });
            }
        }

        [HttpDelete("battery-type-delete/{recId}")]
        [Authorize]
        public async Task<IActionResult> DeleteBatteryType(string recId)
        {
            try
            {
                var batteryType = await _dbContext.BatteryTypeMasters
                    .FirstOrDefaultAsync(b => b.RecId == recId);

                if (batteryType == null)
                {
                    return Ok(new HardwareMasterResponseDto
                    {
                        Success = false,
                        Message = "Battery type not found"
                    });
                }

                batteryType.Active = 0;
                batteryType.UpdatedOn = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Battery type soft deleted: {recId}");

                return Ok(new HardwareMasterResponseDto
                {
                    Success = true,
                    Message = "Battery type deleted successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting battery type");
                return Ok(new HardwareMasterResponseDto
                {
                    Success = false,
                    Message = "An error occurred while deleting battery type"
                });
            }
        }

        [HttpGet("battery-type-list")]
        public async Task<IActionResult> GetBatteryTypeList()
        {
            try
            {
                var batteryTypes = await _dbContext.BatteryTypeMasters
                    .Where(b => b.Active == 1)
                    .OrderBy(b => b.BatteryType)
                    .ToListAsync();

                var result = batteryTypes.Select(MapToBatteryTypeDto).ToList();

                return Ok(new HardwareMasterResponseDto
                {
                    Success = true,
                    Message = "Battery types retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving battery type list");
                return Ok(new HardwareMasterResponseDto
                {
                    Success = false,
                    Message = "An error occurred while retrieving battery types"
                });
            }
        }

        #endregion

        #region Battery Capacity Management

        [HttpPost("battery-capacity-add")]
        [Authorize]
        public async Task<IActionResult> AddBatteryCapacity([FromBody] BatteryCapacityRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Ok(new HardwareMasterResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                var batteryCapacity = new Database.EVCDTO.BatteryCapacityMaster
                {
                    RecId = Guid.NewGuid().ToString(),
                    BatteryCapcacity = request.BatteryCapacity,
                    BatteryCapcacityUnit = request.BatteryCapacityUnit,
                    Active = 1,
                    CreatedOn = DateTime.UtcNow,
                    UpdatedOn = DateTime.UtcNow
                };

                _dbContext.BatteryCapacityMasters.Add(batteryCapacity);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Battery capacity added: {batteryCapacity.RecId} - {batteryCapacity.BatteryCapcacity}");

                return Ok(new HardwareMasterResponseDto
                {
                    Success = true,
                    Message = "Battery capacity added successfully",
                    Data = MapToBatteryCapacityDto(batteryCapacity)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding battery capacity");
                return Ok(new HardwareMasterResponseDto
                {
                    Success = false,
                    Message = "An error occurred while adding battery capacity"
                });
            }
        }

        [HttpPut("battery-capacity-update")]
        [Authorize]
        public async Task<IActionResult> UpdateBatteryCapacity([FromBody] BatteryCapacityUpdateDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Ok(new HardwareMasterResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                var batteryCapacity = await _dbContext.BatteryCapacityMasters
                    .FirstOrDefaultAsync(b => b.RecId == request.RecId);

                if (batteryCapacity == null)
                {
                    return Ok(new HardwareMasterResponseDto
                    {
                        Success = false,
                        Message = "Battery capacity not found"
                    });
                }

                batteryCapacity.BatteryCapcacity = request.BatteryCapacity;
                batteryCapacity.BatteryCapcacityUnit = request.BatteryCapacityUnit;
                batteryCapacity.Active = request.Active;
                batteryCapacity.UpdatedOn = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Battery capacity updated: {batteryCapacity.RecId}");

                return Ok(new HardwareMasterResponseDto
                {
                    Success = true,
                    Message = "Battery capacity updated successfully",
                    Data = MapToBatteryCapacityDto(batteryCapacity)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating battery capacity");
                return Ok(new HardwareMasterResponseDto
                {
                    Success = false,
                    Message = "An error occurred while updating battery capacity"
                });
            }
        }

        [HttpDelete("battery-capacity-delete/{recId}")]
        [Authorize]
        public async Task<IActionResult> DeleteBatteryCapacity(string recId)
        {
            try
            {
                var batteryCapacity = await _dbContext.BatteryCapacityMasters
                    .FirstOrDefaultAsync(b => b.RecId == recId);

                if (batteryCapacity == null)
                {
                    return Ok(new HardwareMasterResponseDto
                    {
                        Success = false,
                        Message = "Battery capacity not found"
                    });
                }

                batteryCapacity.Active = 0;
                batteryCapacity.UpdatedOn = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Battery capacity soft deleted: {recId}");

                return Ok(new HardwareMasterResponseDto
                {
                    Success = true,
                    Message = "Battery capacity deleted successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting battery capacity");
                return Ok(new HardwareMasterResponseDto
                {
                    Success = false,
                    Message = "An error occurred while deleting battery capacity"
                });
            }
        }

        [HttpGet("battery-capacity-list")]
        public async Task<IActionResult> GetBatteryCapacityList()
        {
            try
            {
                var batteryCapacities = await _dbContext.BatteryCapacityMasters
                    .Where(b => b.Active == 1)
                    .OrderBy(b => b.BatteryCapcacity)
                    .ToListAsync();

                var result = batteryCapacities.Select(MapToBatteryCapacityDto).ToList();

                return Ok(new HardwareMasterResponseDto
                {
                    Success = true,
                    Message = "Battery capacities retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving battery capacity list");
                return Ok(new HardwareMasterResponseDto
                {
                    Success = false,
                    Message = "An error occurred while retrieving battery capacities"
                });
            }
        }

        #endregion

        #region Car Manufacturer Management

        [HttpPost("car-manufacturer-add")]
        [Authorize]
        public async Task<IActionResult> AddCarManufacturer([FromBody] CarManufacturerRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Ok(new HardwareMasterResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                var manufacturer = new Database.EVCDTO.CarManufacturerMaster
                {
                    RecId = Guid.NewGuid().ToString(),
                    ManufacturerName = request.ManufacturerName,
                    ManufacturerLogoImage = request.ManufacturerLogoImage,
                    Active = 1,
                    CreatedOn = DateTime.UtcNow,
                    UpdatedOn = DateTime.UtcNow
                };

                _dbContext.CarManufacturerMasters.Add(manufacturer);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Car manufacturer added: {manufacturer.RecId} - {manufacturer.ManufacturerName}");

                return Ok(new HardwareMasterResponseDto
                {
                    Success = true,
                    Message = "Car manufacturer added successfully",
                    Data = MapToCarManufacturerDto(manufacturer)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding car manufacturer");
                return Ok(new HardwareMasterResponseDto
                {
                    Success = false,
                    Message = "An error occurred while adding car manufacturer"
                });
            }
        }

        [HttpPut("car-manufacturer-update")]
        [Authorize]
        public async Task<IActionResult> UpdateCarManufacturer([FromBody] CarManufacturerUpdateDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Ok(new HardwareMasterResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                var manufacturer = await _dbContext.CarManufacturerMasters
                    .FirstOrDefaultAsync(m => m.RecId == request.RecId);

                if (manufacturer == null)
                {
                    return Ok(new HardwareMasterResponseDto
                    {
                        Success = false,
                        Message = "Car manufacturer not found"
                    });
                }

                manufacturer.ManufacturerName = request.ManufacturerName;
                manufacturer.ManufacturerLogoImage = request.ManufacturerLogoImage;
                manufacturer.Active = request.Active;
                manufacturer.UpdatedOn = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Car manufacturer updated: {manufacturer.RecId}");

                return Ok(new HardwareMasterResponseDto
                {
                    Success = true,
                    Message = "Car manufacturer updated successfully",
                    Data = MapToCarManufacturerDto(manufacturer)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating car manufacturer");
                return Ok(new HardwareMasterResponseDto
                {
                    Success = false,
                    Message = "An error occurred while updating car manufacturer"
                });
            }
        }

        [HttpDelete("car-manufacturer-delete/{recId}")]
        [Authorize]
        public async Task<IActionResult> DeleteCarManufacturer(string recId)
        {
            try
            {
                var manufacturer = await _dbContext.CarManufacturerMasters
                    .FirstOrDefaultAsync(m => m.RecId == recId);

                if (manufacturer == null)
                {
                    return Ok(new HardwareMasterResponseDto
                    {
                        Success = false,
                        Message = "Car manufacturer not found"
                    });
                }

                manufacturer.Active = 0;
                manufacturer.UpdatedOn = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Car manufacturer soft deleted: {recId}");

                return Ok(new HardwareMasterResponseDto
                {
                    Success = true,
                    Message = "Car manufacturer deleted successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting car manufacturer");
                return Ok(new HardwareMasterResponseDto
                {
                    Success = false,
                    Message = "An error occurred while deleting car manufacturer"
                });
            }
        }

        [HttpGet("car-manufacturer-list")]
        public async Task<IActionResult> GetCarManufacturerList()
        {
            try
            {
                var manufacturers = await _dbContext.CarManufacturerMasters
                    .Where(m => m.Active == 1)
                    .OrderBy(m => m.ManufacturerName)
                    .ToListAsync();

                var result = manufacturers.Select(MapToCarManufacturerDto).ToList();

                return Ok(new HardwareMasterResponseDto
                {
                    Success = true,
                    Message = "Car manufacturers retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving car manufacturer list");
                return Ok(new HardwareMasterResponseDto
                {
                    Success = false,
                    Message = "An error occurred while retrieving car manufacturers"
                });
            }
        }

        [HttpGet("car-manufacturer-details/{recId}")]
        public async Task<IActionResult> GetCarManufacturerDetails(string recId)
        {
            try
            {
                var manufacturer = await _dbContext.CarManufacturerMasters
                    .FirstOrDefaultAsync(m => m.RecId == recId && m.Active == 1);

                if (manufacturer == null)
                {
                    return Ok(new HardwareMasterResponseDto
                    {
                        Success = false,
                        Message = "Car manufacturer not found"
                    });
                }

                return Ok(new HardwareMasterResponseDto
                {
                    Success = true,
                    Message = "Car manufacturer details retrieved successfully",
                    Data = MapToCarManufacturerDto(manufacturer)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving car manufacturer details");
                return Ok(new HardwareMasterResponseDto
                {
                    Success = false,
                    Message = "An error occurred while retrieving car manufacturer details"
                });
            }
        }

        #endregion

        #region EV Model Management

        [HttpPost("ev-model-add")]
        [Authorize]
        public async Task<IActionResult> AddEVModel([FromBody] EVModelRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Ok(new HardwareMasterResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                var evModel = new Database.EVCDTO.EVModelMaster
                {
                    RecId = Guid.NewGuid().ToString(),
                    ModelName = request.ModelName,
                    ManufacturerId = request.ManufacturerId,
                    Variant = request.Variant,
                    BatterytypeId = request.BatteryTypeId,
                    BatteryCapacityId = request.BatteryCapacityId,
                    CarModelImage = request.CarModelImage,
                    TypeASupport = request.TypeASupport,
                    TypeBSupport = request.TypeBSupport,
                    ChadeMOSupport = request.ChadeMOSupport,
                    CCSSupport = request.CCSSupport,
                    Active = 1,
                    CreatedOn = DateTime.UtcNow,
                    UpdatedOn = DateTime.UtcNow
                };

                _dbContext.EVModelMasters.Add(evModel);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"EV model added: {evModel.RecId} - {evModel.ModelName}");

                return Ok(new HardwareMasterResponseDto
                {
                    Success = true,
                    Message = "EV model added successfully",
                    Data = await MapToEVModelDto(evModel)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding EV model");
                return Ok(new HardwareMasterResponseDto
                {
                    Success = false,
                    Message = "An error occurred while adding EV model"
                });
            }
        }

        [HttpPut("ev-model-update")]
        [Authorize]
        public async Task<IActionResult> UpdateEVModel([FromBody] EVModelUpdateDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Ok(new HardwareMasterResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                var evModel = await _dbContext.EVModelMasters
                    .FirstOrDefaultAsync(e => e.RecId == request.RecId);

                if (evModel == null)
                {
                    return Ok(new HardwareMasterResponseDto
                    {
                        Success = false,
                        Message = "EV model not found"
                    });
                }

                evModel.ModelName = request.ModelName;
                evModel.ManufacturerId = request.ManufacturerId;
                evModel.Variant = request.Variant;
                evModel.BatterytypeId = request.BatteryTypeId;
                evModel.BatteryCapacityId = request.BatteryCapacityId;
                evModel.CarModelImage = request.CarModelImage;
                evModel.TypeASupport = request.TypeASupport;
                evModel.TypeBSupport = request.TypeBSupport;
                evModel.ChadeMOSupport = request.ChadeMOSupport;
                evModel.CCSSupport = request.CCSSupport;
                evModel.Active = request.Active;
                evModel.UpdatedOn = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"EV model updated: {evModel.RecId}");

                return Ok(new HardwareMasterResponseDto
                {
                    Success = true,
                    Message = "EV model updated successfully",
                    Data = await MapToEVModelDto(evModel)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating EV model");
                return Ok(new HardwareMasterResponseDto
                {
                    Success = false,
                    Message = "An error occurred while updating EV model"
                });
            }
        }

        [HttpDelete("ev-model-delete/{recId}")]
        [Authorize]
        public async Task<IActionResult> DeleteEVModel(string recId)
        {
            try
            {
                var evModel = await _dbContext.EVModelMasters
                    .FirstOrDefaultAsync(e => e.RecId == recId);

                if (evModel == null)
                {
                    return Ok(new HardwareMasterResponseDto
                    {
                        Success = false,
                        Message = "EV model not found"
                    });
                }

                evModel.Active = 0;
                evModel.UpdatedOn = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"EV model soft deleted: {recId}");

                return Ok(new HardwareMasterResponseDto
                {
                    Success = true,
                    Message = "EV model deleted successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting EV model");
                return Ok(new HardwareMasterResponseDto
                {
                    Success = false,
                    Message = "An error occurred while deleting EV model"
                });
            }
        }

        [HttpGet("ev-model-list")]
        public async Task<IActionResult> GetEVModelList()
        {
            try
            {
                var evModels = await _dbContext.EVModelMasters
                    .Where(e => e.Active == 1)
                    .OrderBy(e => e.ModelName)
                    .ToListAsync();

                var result = new System.Collections.Generic.List<EVModelDto>();
                foreach (var model in evModels)
                {
                    result.Add(await MapToEVModelDto(model));
                }

                return Ok(new HardwareMasterResponseDto
                {
                    Success = true,
                    Message = "EV models retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving EV model list");
                return Ok(new HardwareMasterResponseDto
                {
                    Success = false,
                    Message = "An error occurred while retrieving EV models"
                });
            }
        }

        [HttpGet("ev-model-list-by-manufacturer/{manufacturerId}")]
        public async Task<IActionResult> GetEVModelListByManufacturer(string manufacturerId)
        {
            try
            {
                var evModels = await _dbContext.EVModelMasters
                    .Where(e => e.Active == 1 && e.ManufacturerId == manufacturerId)
                    .OrderBy(e => e.ModelName)
                    .ToListAsync();

                var result = new System.Collections.Generic.List<EVModelDto>();
                foreach (var model in evModels)
                {
                    result.Add(await MapToEVModelDto(model));
                }

                return Ok(new HardwareMasterResponseDto
                {
                    Success = true,
                    Message = "EV models retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving EV model list by manufacturer");
                return Ok(new HardwareMasterResponseDto
                {
                    Success = false,
                    Message = "An error occurred while retrieving EV models"
                });
            }
        }

        [HttpGet("ev-model-details/{recId}")]
        public async Task<IActionResult> GetEVModelDetails(string recId)
        {
            try
            {
                var evModel = await _dbContext.EVModelMasters
                    .FirstOrDefaultAsync(e => e.RecId == recId && e.Active == 1);

                if (evModel == null)
                {
                    return Ok(new HardwareMasterResponseDto
                    {
                        Success = false,
                        Message = "EV model not found"
                    });
                }

                return Ok(new HardwareMasterResponseDto
                {
                    Success = true,
                    Message = "EV model details retrieved successfully",
                    Data = await MapToEVModelDto(evModel)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving EV model details");
                return Ok(new HardwareMasterResponseDto
                {
                    Success = false,
                    Message = "An error occurred while retrieving EV model details"
                });
            }
        }

        #endregion

        #region Mapping Methods

        private ChargerTypeDto MapToChargerTypeDto(Database.EVCDTO.ChargerTypeMaster chargerType)
        {
            return new ChargerTypeDto
            {
                RecId = chargerType.RecId,
                ChargerType = chargerType.ChargerType,
                ChargerTypeImage = chargerType.ChargerTypeImage,
                AdditionalInfo1 = chargerType.Additional_Info_1,
                Active = chargerType.Active,
                CreatedOn = chargerType.CreatedOn,
                UpdatedOn = chargerType.UpdatedOn
            };
        }

        private BatteryTypeDto MapToBatteryTypeDto(Database.EVCDTO.BatteryTypeMaster batteryType)
        {
            return new BatteryTypeDto
            {
                RecId = batteryType.RecId,
                BatteryType = batteryType.BatteryType,
                Active = batteryType.Active,
                CreatedOn = batteryType.CreatedOn,
                UpdatedOn = batteryType.UpdatedOn
            };
        }

        private BatteryCapacityDto MapToBatteryCapacityDto(Database.EVCDTO.BatteryCapacityMaster batteryCapacity)
        {
            return new BatteryCapacityDto
            {
                RecId = batteryCapacity.RecId,
                BatteryCapacity = batteryCapacity.BatteryCapcacity,
                BatteryCapacityUnit = batteryCapacity.BatteryCapcacityUnit,
                Active = batteryCapacity.Active,
                CreatedOn = batteryCapacity.CreatedOn,
                UpdatedOn = batteryCapacity.UpdatedOn
            };
        }

        private CarManufacturerDto MapToCarManufacturerDto(Database.EVCDTO.CarManufacturerMaster manufacturer)
        {
            return new CarManufacturerDto
            {
                RecId = manufacturer.RecId,
                ManufacturerName = manufacturer.ManufacturerName,
                ManufacturerLogoImage = manufacturer.ManufacturerLogoImage,
                Active = manufacturer.Active,
                CreatedOn = manufacturer.CreatedOn,
                UpdatedOn = manufacturer.UpdatedOn
            };
        }

        private async Task<EVModelDto> MapToEVModelDto(Database.EVCDTO.EVModelMaster evModel)
        {
            var manufacturer = await _dbContext.CarManufacturerMasters
                .FirstOrDefaultAsync(m => m.RecId == evModel.ManufacturerId);

            var batteryType = await _dbContext.BatteryTypeMasters
                .FirstOrDefaultAsync(b => b.RecId == evModel.BatterytypeId);

            var batteryCapacity = await _dbContext.BatteryCapacityMasters
                .FirstOrDefaultAsync(b => b.RecId == evModel.BatteryCapacityId);

            return new EVModelDto
            {
                RecId = evModel.RecId,
                ModelName = evModel.ModelName,
                ManufacturerId = evModel.ManufacturerId,
                ManufacturerName = manufacturer?.ManufacturerName,
                Variant = evModel.Variant,
                BatteryTypeId = evModel.BatterytypeId,
                BatteryTypeName = batteryType?.BatteryType,
                BatteryCapacityId = evModel.BatteryCapacityId,
                BatteryCapacityValue = batteryCapacity?.BatteryCapcacity,
                CarModelImage = evModel.CarModelImage,
                TypeASupport = evModel.TypeASupport,
                TypeBSupport = evModel.TypeBSupport,
                ChadeMOSupport = evModel.ChadeMOSupport,
                CCSSupport = evModel.CCSSupport,
                Active = evModel.Active,
                CreatedOn = evModel.CreatedOn,
                UpdatedOn = evModel.UpdatedOn
            };
        }

        #endregion
    }
}
