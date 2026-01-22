using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Database.EVCDTO;
using OCPP.Core.Management.Models.Auth;
using OCPP.Core.Management.Services;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace OCPP.Core.Management.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly OCPPCoreContext _dbContext;
        private readonly IJwtService _jwtService;
        private readonly ILogger<UserController> _logger;

        public UserController(
            OCPPCoreContext dbContext,
            IJwtService jwtService,
            ILogger<UserController> logger)
        {
            _dbContext = dbContext;
            _jwtService = jwtService;
            _logger = logger;
        }

        /// <summary>
        /// User registration endpoint
        /// </summary>
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Ok(new AuthResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                // Check if user already exists
                var existingUser = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.EMailID == request.EMailID || u.PhoneNumber == request.PhoneNumber);

                if (existingUser != null)
                {
                    return Ok(new AuthResponseDto
                    {
                        Success = false,
                        Message = "User with this email or phone number already exists"
                    });
                }

                // Create new user
                var user = new Users
                {
                    RecId = Guid.NewGuid().ToString(),
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    EMailID = request.EMailID,
                    PhoneNumber = request.PhoneNumber,
                    CountryCode = request.CountryCode ?? "+1",
                    Password = HashPassword(request.Password),
                    AddressLine1 = request.AddressLine1,
                    AddressLine2 = request.AddressLine2,
                    AddressLine3 = request.AddressLine3,
                    State = request.State,
                    City = request.City,
                    PinCode = request.PinCode,
                    ProfileCompleted = "No",
                    UserRole = "User",
                    Active = 1,
                    CreatedOn = DateTime.UtcNow,
                    UpdatedOn = DateTime.UtcNow
                };

                _dbContext.Users.Add(user);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"New user registered: {user.EMailID}");

                return Ok(new AuthResponseDto
                {
                    Success = true,
                    Message = "Registration successful",
                    User = MapToUserDto(user)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration");
                return Ok(new AuthResponseDto
                {
                    Success = false,
                    Message = "An error occurred during registration"
                });
            }
        }

        /// <summary>
        /// User login endpoint with JWT token generation
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Ok(new AuthResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                // Find user by email or phone
                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(u =>
                        (u.EMailID == request.EmailOrPhone || u.PhoneNumber == request.EmailOrPhone)
                        && u.Active == 1);

                if (user == null || !VerifyPassword(request.Password, user.Password))
                {
                    return Unauthorized(new AuthResponseDto
                    {
                        Success = false,
                        Message = "Invalid credentials"
                    });
                }

                // Generate tokens
                var accessToken = _jwtService.GenerateAccessToken(user);
                var refreshToken = _jwtService.GenerateRefreshToken(GetIpAddress());
                refreshToken.UserId = user.RecId;

                // Save refresh token to database
                _dbContext.RefreshTokens.Add(refreshToken);

                // Update last login
                user.LastLogin = DateTime.UtcNow.ToString("o");
                user.UpdatedOn = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                // Set tokens as HTTP-only cookies
                SetTokenCookies(accessToken, refreshToken.Token);

                _logger.LogInformation($"User logged in: {user.EMailID}");

                return Ok(new AuthResponseDto
                {
                    Success = true,
                    Message = "Login successful",
                    User = MapToUserDto(user)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login");
                return Ok(new AuthResponseDto
                {
                    Success = false,
                    Message = "An error occurred during login"
                });
            }
        }

        /// <summary>
        /// User logout endpoint
        /// </summary>
        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var refreshToken = Request.Cookies["refreshToken"];

                if (!string.IsNullOrEmpty(refreshToken))
                {
                    var token = await _dbContext.RefreshTokens
                        .FirstOrDefaultAsync(t => t.Token == refreshToken);

                    if (token != null)
                    {
                        token.RevokedAt = DateTime.UtcNow;
                        token.RevokedByIp = GetIpAddress();
                        await _dbContext.SaveChangesAsync();
                    }
                }

                // Clear cookies
                Response.Cookies.Delete("accessToken");
                Response.Cookies.Delete("refreshToken");

                _logger.LogInformation("User logged out");

                return Ok(new AuthResponseDto
                {
                    Success = true,
                    Message = "Logout successful"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return Ok(new AuthResponseDto
                {
                    Success = false,
                    Message = "An error occurred during logout"
                });
            }
        }

        /// <summary>
        /// Reset password endpoint
        /// </summary>
        [HttpPost("reset-password")]
        [Authorize]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Ok(new AuthResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                // Find user
                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(u =>
                        (u.EMailID == request.EmailOrPhone || u.PhoneNumber == request.EmailOrPhone)
                        && u.Active == 1);

                if (user == null || !VerifyPassword(request.OldPassword, user.Password))
                {
                    return Ok(new AuthResponseDto
                    {
                        Success = false,
                        Message = "Invalid credentials"
                    });
                }

                // Update password
                user.Password = HashPassword(request.NewPassword);
                user.UpdatedOn = DateTime.UtcNow;

                // Revoke all existing refresh tokens for this user
                var userTokens = await _dbContext.RefreshTokens
                    .Where(t => t.UserId == user.RecId && t.RevokedAt == null)
                    .ToListAsync();

                foreach (var token in userTokens)
                {
                    token.RevokedAt = DateTime.UtcNow;
                    token.RevokedByIp = GetIpAddress();
                }

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Password reset for user: {user.EMailID}");

                return Ok(new AuthResponseDto
                {
                    Success = true,
                    Message = "Password reset successful. Please login again."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during password reset");
                return Ok(new AuthResponseDto
                {
                    Success = false,
                    Message = "An error occurred during password reset"
                });
            }
        }

        /// <summary>
        /// Update user profile endpoint
        /// </summary>
        [HttpPut("profile-update")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Ok(new AuthResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                // Get user ID from token
                var userId = User.FindFirst("userId")?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new AuthResponseDto
                    {
                        Success = false,
                        Message = "Invalid token"
                    });
                }

                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.RecId == userId);
                if (user == null)
                {
                    return Ok(new AuthResponseDto
                    {
                        Success = false,
                        Message = "User not found"
                    });
                }

                // Update user properties
                if (!string.IsNullOrEmpty(request.FirstName))
                    user.FirstName = request.FirstName;
                if (!string.IsNullOrEmpty(request.LastName))
                    user.LastName = request.LastName;
                if (!string.IsNullOrEmpty(request.EMailID))
                    user.EMailID = request.EMailID;
                if (!string.IsNullOrEmpty(request.PhoneNumber))
                    user.PhoneNumber = request.PhoneNumber;
                if (!string.IsNullOrEmpty(request.CountryCode))
                    user.CountryCode = request.CountryCode;
                if (!string.IsNullOrEmpty(request.ProfileImageID))
                    user.ProfileImageID = request.ProfileImageID;
                if (!string.IsNullOrEmpty(request.AddressLine1))
                    user.AddressLine1 = request.AddressLine1;
                if (!string.IsNullOrEmpty(request.AddressLine2))
                    user.AddressLine2 = request.AddressLine2;
                if (!string.IsNullOrEmpty(request.AddressLine3))
                    user.AddressLine3 = request.AddressLine3;
                if (!string.IsNullOrEmpty(request.State))
                    user.State = request.State;
                if (!string.IsNullOrEmpty(request.City))
                    user.City = request.City;
                if (!string.IsNullOrEmpty(request.PinCode))
                    user.PinCode = request.PinCode;

                user.UpdatedOn = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Profile updated for user: {user.EMailID}");

                return Ok(new AuthResponseDto
                {
                    Success = true,
                    Message = "Profile updated successfully",
                    User = MapToUserDto(user)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during profile update");
                return Ok(new AuthResponseDto
                {
                    Success = false,
                    Message = "An error occurred during profile update"
                });
            }
        }

        /// <summary>
        /// Delete user account endpoint
        /// </summary>
        [HttpDelete("user-delete")]
        [Authorize]
        public async Task<IActionResult> DeleteUser()
        {
            try
            {
                // Get user ID from token
                var userId = User.FindFirst("userId")?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new AuthResponseDto
                    {
                        Success = false,
                        Message = "Invalid token"
                    });
                }

                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.RecId == userId);
                if (user == null)
                {
                    return Ok(new AuthResponseDto
                    {
                        Success = false,
                        Message = "User not found"
                    });
                }

                // Soft delete - set Active to 0
                user.Active = 0;
                user.UpdatedOn = DateTime.UtcNow;

                // Revoke all refresh tokens
                var userTokens = await _dbContext.RefreshTokens
                    .Where(t => t.UserId == userId && t.RevokedAt == null)
                    .ToListAsync();

                foreach (var token in userTokens)
                {
                    token.RevokedAt = DateTime.UtcNow;
                    token.RevokedByIp = GetIpAddress();
                }

                await _dbContext.SaveChangesAsync();

                // Clear cookies
                Response.Cookies.Delete("accessToken");
                Response.Cookies.Delete("refreshToken");

                _logger.LogInformation($"User account deleted: {user.EMailID}");

                return Ok(new AuthResponseDto
                {
                    Success = true,
                    Message = "Account deleted successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during account deletion");
                return Ok(new AuthResponseDto
                {
                    Success = false,
                    Message = "An error occurred during account deletion"
                });
            }
        }

        /// <summary>
        /// Refresh access token using refresh token
        /// </summary>
        [HttpPost("refresh-token")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshToken()
        {
            try
            {
                var refreshToken = Request.Cookies["refreshToken"];

                if (string.IsNullOrEmpty(refreshToken))
                {
                    return Unauthorized(new AuthResponseDto
                    {
                        Success = false,
                        Message = "Refresh token is required"
                    });
                }

                var token = await _dbContext.RefreshTokens
                    .Include(t => t.UserId)
                    .FirstOrDefaultAsync(t => t.Token == refreshToken);

                if (token == null || !token.IsActive)
                {
                    return Unauthorized(new AuthResponseDto
                    {
                        Success = false,
                        Message = "Invalid refresh token"
                    });
                }

                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.RecId == token.UserId);
                if (user == null || user.Active == 0)
                {
                    return Unauthorized(new AuthResponseDto
                    {
                        Success = false,
                        Message = "User not found or inactive"
                    });
                }

                // Generate new tokens
                var newAccessToken = _jwtService.GenerateAccessToken(user);
                var newRefreshToken = _jwtService.GenerateRefreshToken(GetIpAddress());
                newRefreshToken.UserId = user.RecId;

                // Revoke old refresh token
                token.RevokedAt = DateTime.UtcNow;
                token.RevokedByIp = GetIpAddress();
                token.ReplacedByToken = newRefreshToken.Token;

                // Save new refresh token
                _dbContext.RefreshTokens.Add(newRefreshToken);
                await _dbContext.SaveChangesAsync();

                // Set new tokens as cookies
                SetTokenCookies(newAccessToken, newRefreshToken.Token);

                return Ok(new AuthResponseDto
                {
                    Success = true,
                    Message = "Token refreshed successfully",
                    User = MapToUserDto(user)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token refresh");
                return Ok(new AuthResponseDto
                {
                    Success = false,
                    Message = "An error occurred during token refresh"
                });
            }
        }

        /// <summary>
        /// Get current user profile
        /// </summary>
        [HttpGet("profile")]
        [Authorize]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                var userId = User.FindFirst("userId")?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new AuthResponseDto
                    {
                        Success = false,
                        Message = "Invalid token"
                    });
                }

                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.RecId == userId && u.Active == 1);
                if (user == null)
                {
                    return Ok(new AuthResponseDto
                    {
                        Success = false,
                        Message = "User not found"
                    });
                }

                return Ok(new AuthResponseDto
                {
                    Success = true,
                    Message = "Profile retrieved successfully",
                    User = MapToUserDto(user)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving profile");
                return Ok(new AuthResponseDto
                {
                    Success = false,
                    Message = "An error occurred while retrieving profile"
                });
            }
        }

        /// <summary>
        /// Add credits to user wallet
        /// </summary>
        [HttpPost("add-wallet-credits")]
        [Authorize]
        public async Task<IActionResult> AddWalletCredits([FromBody] AddWalletCreditsRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Ok(new AuthResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                // Check if user exists
                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.RecId == request.UserId && u.Active == 1);
                if (user == null)
                {
                    return Ok(new AuthResponseDto
                    {
                        Success = false,
                        Message = "User not found"
                    });
                }

                // Get current balance
                var lastTransaction = await _dbContext.WalletTransactionLogs
                    .Where(w => w.UserId == request.UserId)
                    .OrderByDescending(w => w.CreatedOn)
                    .FirstOrDefaultAsync();

                decimal previousBalance = 0;
                if (lastTransaction != null && decimal.TryParse(lastTransaction.CurrentCreditBalance, out var lastBalance))
                {
                    previousBalance = lastBalance;
                }

                decimal newBalance = previousBalance + request.Amount;

                // Create transaction log
                var walletLog = new WalletTransactionLog
                {
                    RecId = Guid.NewGuid().ToString(),
                    UserId = request.UserId,
                    PreviousCreditBalance = previousBalance.ToString("F2"),
                    CurrentCreditBalance = newBalance.ToString("F2"),
                    TransactionType = request.TransactionType,
                    PaymentRecId = request.PaymentRecId,
                    AdditionalInfo1 = request.AdditionalInfo1,
                    AdditionalInfo2 = request.AdditionalInfo2,
                    AdditionalInfo3 = request.AdditionalInfo3,
                    Active = 1,
                    CreatedOn = DateTime.UtcNow,
                    UpdatedOn = DateTime.UtcNow
                };

                _dbContext.WalletTransactionLogs.Add(walletLog);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Wallet credits added for user: {request.UserId}, Amount: {request.Amount}");

                return Ok(new WalletResponseDto
                {
                    Success = true,
                    Message = "Credits added successfully",
                    Wallet = new WalletDto
                    {
                        UserId = request.UserId,
                        CurrentBalance = newBalance
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding wallet credits");
                return Ok(new AuthResponseDto
                {
                    Success = false,
                    Message = "An error occurred while adding credits"
                });
            }
        }

        /// <summary>
        /// Add user vehicle
        /// </summary>
        [HttpPost("user-vehicle-add")]
        [Authorize]
        public async Task<IActionResult> AddUserVehicle([FromBody] UserVehicleRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Ok(new AuthResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                // Get user ID from token
                var userId = User.FindFirst("userId")?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new AuthResponseDto
                    {
                        Success = false,
                        Message = "Invalid token"
                    });
                }

                // Check if user exists
                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.RecId == userId && u.Active == 1);
                if (user == null)
                {
                    return Ok(new AuthResponseDto
                    {
                        Success = false,
                        Message = "User not found"
                    });
                }

                // Check if registration number already exists
                var existingVehicle = await _dbContext.UserVehicles
                    .FirstOrDefaultAsync(v => v.CarRegistrationNumber == request.CarRegistrationNumber && v.Active == 1);

                if (existingVehicle != null)
                {
                    return Ok(new AuthResponseDto
                    {
                        Success = false,
                        Message = "Vehicle with this registration number already exists"
                    });
                }

                // If this is set as default, unset other defaults
                if (request.DefaultConfig == 1)
                {
                    var userVehicles = await _dbContext.UserVehicles
                        .Where(v => v.UserId == userId && v.Active == 1)
                        .ToListAsync();

                    foreach (var v in userVehicles)
                    {
                        v.DefaultConfig = 0;
                        v.UpdatedOn = DateTime.UtcNow;
                    }
                }

                var vehicle = new UserVehicle
                {
                    RecId = Guid.NewGuid().ToString(),
                    UserId = userId,
                    EVManufacturerID = request.EVManufacturerID,
                    CarModelID = request.CarModelID,
                    CarModelVariant = request.CarModelVariant,
                    CarRegistrationNumber = request.CarRegistrationNumber,
                    DefaultConfig = request.DefaultConfig,
                    BatteryTypeId = request.BatteryTypeId,
                    BatteryCapacityId = request.BatteryCapacityId,
                    Active = 1,
                    CreatedOn = DateTime.UtcNow,
                    UpdatedOn = DateTime.UtcNow
                };

                _dbContext.UserVehicles.Add(vehicle);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Vehicle added for user: {userId}");

                return Ok(new UserVehicleResponseDto
                {
                    Success = true,
                    Message = "Vehicle added successfully",
                    Vehicle = MapToUserVehicleDto(vehicle)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding user vehicle");
                return Ok(new AuthResponseDto
                {
                    Success = false,
                    Message = "An error occurred while adding vehicle"
                });
            }
        }

        /// <summary>
        /// Update user vehicle
        /// </summary>
        [HttpPut("user-vehicle-update")]
        [Authorize]
        public async Task<IActionResult> UpdateUserVehicle([FromBody] UserVehicleUpdateDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Ok(new AuthResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                // Get user ID from token
                var userId = User.FindFirst("userId")?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new AuthResponseDto
                    {
                        Success = false,
                        Message = "Invalid token"
                    });
                }

                var vehicle = await _dbContext.UserVehicles
                    .FirstOrDefaultAsync(v => v.RecId == request.RecId && v.UserId == userId && v.Active == 1);

                if (vehicle == null)
                {
                    return Ok(new AuthResponseDto
                    {
                        Success = false,
                        Message = "Vehicle not found"
                    });
                }

                // If this is being set as default, unset other defaults
                if (request.DefaultConfig == 1 && vehicle.DefaultConfig != 1)
                {
                    var userVehicles = await _dbContext.UserVehicles
                        .Where(v => v.UserId == userId && v.RecId != request.RecId && v.Active == 1)
                        .ToListAsync();

                    foreach (var v in userVehicles)
                    {
                        v.DefaultConfig = 0;
                        v.UpdatedOn = DateTime.UtcNow;
                    }
                }

                // Update vehicle properties
                if (!string.IsNullOrEmpty(request.EVManufacturerID))
                    vehicle.EVManufacturerID = request.EVManufacturerID;
                if (!string.IsNullOrEmpty(request.CarModelID))
                    vehicle.CarModelID = request.CarModelID;
                if (!string.IsNullOrEmpty(request.CarModelVariant))
                    vehicle.CarModelVariant = request.CarModelVariant;
                if (!string.IsNullOrEmpty(request.CarRegistrationNumber))
                    vehicle.CarRegistrationNumber = request.CarRegistrationNumber;
                vehicle.DefaultConfig = request.DefaultConfig;
                if (!string.IsNullOrEmpty(request.BatteryTypeId))
                    vehicle.BatteryTypeId = request.BatteryTypeId;
                if (!string.IsNullOrEmpty(request.BatteryCapacityId))
                    vehicle.BatteryCapacityId = request.BatteryCapacityId;

                vehicle.UpdatedOn = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Vehicle updated: {request.RecId}");

                return Ok(new UserVehicleResponseDto
                {
                    Success = true,
                    Message = "Vehicle updated successfully",
                    Vehicle = MapToUserVehicleDto(vehicle)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user vehicle");
                return Ok(new AuthResponseDto
                {
                    Success = false,
                    Message = "An error occurred while updating vehicle"
                });
            }
        }

        /// <summary>
        /// Delete user vehicle
        /// </summary>
        [HttpDelete("user-vehicle-delete/{vehicleId}")]
        [Authorize]
        public async Task<IActionResult> DeleteUserVehicle(string vehicleId)
        {
            try
            {
                // Get user ID from token
                var userId = User.FindFirst("userId")?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new AuthResponseDto
                    {
                        Success = false,
                        Message = "Invalid token"
                    });
                }

                var vehicle = await _dbContext.UserVehicles
                    .FirstOrDefaultAsync(v => v.RecId == vehicleId && v.UserId == userId && v.Active == 1);

                if (vehicle == null)
                {
                    return Ok(new AuthResponseDto
                    {
                        Success = false,
                        Message = "Vehicle not found"
                    });
                }

                // Soft delete
                vehicle.Active = 0;
                vehicle.UpdatedOn = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Vehicle deleted: {vehicleId}");

                return Ok(new AuthResponseDto
                {
                    Success = true,
                    Message = "Vehicle deleted successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user vehicle");
                return Ok(new AuthResponseDto
                {
                    Success = false,
                    Message = "An error occurred while deleting vehicle"
                });
            }
        }

        /// <summary>
        /// Get list of all users (Admin endpoint)
        /// </summary>
        [HttpGet("user-list")]
        [Authorize]
        public async Task<IActionResult> GetUserList([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                // Optional: Add role check for admin
                var users = await _dbContext.Users
                    .Where(u => u.Active == 1)
                    .OrderByDescending(u => u.CreatedOn)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var totalCount = await _dbContext.Users.CountAsync(u => u.Active == 1);

                return Ok(new UserListResponseDto
                {
                    Success = true,
                    Message = "Users retrieved successfully",
                    Users = users.Select(MapToUserDto).ToList(),
                    TotalCount = totalCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user list");
                return Ok(new AuthResponseDto
                {
                    Success = false,
                    Message = "An error occurred while retrieving users"
                });
            }
        }

        /// <summary>
        /// Get user details with wallet and vehicles
        /// </summary>
        [HttpGet("user-details/{userId}")]
        [Authorize]
        public async Task<IActionResult> GetUserDetails(string userId)
        {
            try
            {
                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.RecId == userId && u.Active == 1);
                if (user == null)
                {
                    return Ok(new AuthResponseDto
                    {
                        Success = false,
                        Message = "User not found"
                    });
                }

                // Get wallet details
                var walletTransactions = await _dbContext.WalletTransactionLogs
                    .Where(w => w.UserId == userId && w.Active == 1)
                    .OrderByDescending(w => w.CreatedOn)
                    .Take(10)
                    .ToListAsync();

                var lastTransaction = walletTransactions.FirstOrDefault();
                decimal currentBalance = 0;
                if (lastTransaction != null && decimal.TryParse(lastTransaction.CurrentCreditBalance, out var balance))
                {
                    currentBalance = balance;
                }

                var wallet = new WalletDto
                {
                    UserId = userId,
                    CurrentBalance = currentBalance,
                    RecentTransactions = walletTransactions.Select(MapToWalletTransactionDto).ToList()
                };

                // Get vehicles
                var vehicles = await _dbContext.UserVehicles
                    .Where(v => v.UserId == userId && v.Active == 1)
                    .ToListAsync();

                return Ok(new UserDetailsResponseDto
                {
                    Success = true,
                    Message = "User details retrieved successfully",
                    User = MapToUserDto(user),
                    Wallet = wallet,
                    Vehicles = vehicles.Select(MapToUserVehicleDto).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user details");
                return Ok(new AuthResponseDto
                {
                    Success = false,
                    Message = "An error occurred while retrieving user details"
                });
            }
        }

        /// <summary>
        /// Get wallet details for current user
        /// </summary>
        [HttpGet("wallet-details")]
        [Authorize]
        public async Task<IActionResult> GetWalletDetails()
        {
            try
            {
                var userId = User.FindFirst("userId")?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new AuthResponseDto
                    {
                        Success = false,
                        Message = "Invalid token"
                    });
                }

                var walletTransactions = await _dbContext.WalletTransactionLogs
                    .Where(w => w.UserId == userId && w.Active == 1)
                    .OrderByDescending(w => w.CreatedOn)
                    .Take(20)
                    .ToListAsync();

                var lastTransaction = walletTransactions.FirstOrDefault();
                decimal currentBalance = 0;
                if (lastTransaction != null && decimal.TryParse(lastTransaction.CurrentCreditBalance, out var balance))
                {
                    currentBalance = balance;
                }

                var wallet = new WalletDto
                {
                    UserId = userId,
                    CurrentBalance = currentBalance,
                    RecentTransactions = walletTransactions.Select(MapToWalletTransactionDto).ToList()
                };

                return Ok(new WalletResponseDto
                {
                    Success = true,
                    Message = "Wallet details retrieved successfully",
                    Wallet = wallet
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving wallet details");
                return Ok(new AuthResponseDto
                {
                    Success = false,
                    Message = "An error occurred while retrieving wallet details"
                });
            }
        }

        /// <summary>
        /// Get user vehicle list for current user
        /// </summary>
        [HttpGet("user-vehicle-list")]
        [Authorize]
        public async Task<IActionResult> GetUserVehicleList()
        {
            try
            {
                var userId = User.FindFirst("userId")?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new AuthResponseDto
                    {
                        Success = false,
                        Message = "Invalid token"
                    });
                }

                var vehicles = await _dbContext.UserVehicles
                    .Where(v => v.UserId == userId && v.Active == 1)
                    .OrderByDescending(v => v.DefaultConfig)
                    .ThenByDescending(v => v.CreatedOn)
                    .ToListAsync();

                return Ok(new UserVehicleListResponseDto
                {
                    Success = true,
                    Message = "Vehicles retrieved successfully",
                    Vehicles = vehicles.Select(MapToUserVehicleDto).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user vehicles");
                return Ok(new AuthResponseDto
                {
                    Success = false,
                    Message = "An error occurred while retrieving vehicles"
                });
            }
        }

        #region Helper Methods

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }

        private bool VerifyPassword(string password, string hashedPassword)
        {
            var hash = HashPassword(password);
            return hash == hashedPassword;
        }

        private void SetTokenCookies(string accessToken, string refreshToken)
        {
            Response.Cookies.Append("accessToken", accessToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = false, // Set to true in production with HTTPS
                SameSite = SameSiteMode.Lax, // Changed from Strict to Lax for better API compatibility
                Expires = DateTimeOffset.UtcNow.AddMinutes(15)
            });

            Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = false, // Set to true in production with HTTPS
                SameSite = SameSiteMode.Lax, // Changed from Strict to Lax for better API compatibility
                Expires = DateTimeOffset.UtcNow.AddDays(7)
            });
        }

        private string GetIpAddress()
        {
            if (Request.Headers.ContainsKey("X-Forwarded-For"))
                return Request.Headers["X-Forwarded-For"];
            else
                return HttpContext.Connection.RemoteIpAddress?.MapToIPv4()?.ToString();
        }

        private UserDto MapToUserDto(Users user)
        {
            return new UserDto
            {
                RecId = user.RecId,
                FirstName = user.FirstName,
                LastName = user.LastName,
                EMailID = user.EMailID,
                PhoneNumber = user.PhoneNumber,
                CountryCode = user.CountryCode,
                ProfileImageID = user.ProfileImageID,
                AddressLine1 = user.AddressLine1,
                AddressLine2 = user.AddressLine2,
                AddressLine3 = user.AddressLine3,
                State = user.State,
                City = user.City,
                PinCode = user.PinCode,
                ProfileCompleted = user.ProfileCompleted,
                UserRole = user.UserRole,
                CreatedOn = user.CreatedOn
            };
        }

        private UserVehicleDto MapToUserVehicleDto(UserVehicle vehicle)
        {
            return new UserVehicleDto
            {
                RecId = vehicle.RecId,
                UserId = vehicle.UserId,
                EVManufacturerID = vehicle.EVManufacturerID,
                CarModelID = vehicle.CarModelID,
                CarModelVariant = vehicle.CarModelVariant,
                CarRegistrationNumber = vehicle.CarRegistrationNumber,
                DefaultConfig = vehicle.DefaultConfig,
                BatteryTypeId = vehicle.BatteryTypeId,
                BatteryCapacityId = vehicle.BatteryCapacityId,
                CreatedOn = vehicle.CreatedOn,
                UpdatedOn = vehicle.UpdatedOn
            };
        }

        private WalletTransactionDto MapToWalletTransactionDto(WalletTransactionLog transaction)
        {
            return new WalletTransactionDto
            {
                RecId = transaction.RecId,
                PreviousCreditBalance = transaction.PreviousCreditBalance,
                CurrentCreditBalance = transaction.CurrentCreditBalance,
                TransactionType = transaction.TransactionType,
                PaymentRecId = transaction.PaymentRecId,
                ChargingSessionId = transaction.ChargingSessionId,
                AdditionalInfo1 = transaction.AdditionalInfo1,
                AdditionalInfo2 = transaction.AdditionalInfo2,
                AdditionalInfo3 = transaction.AdditionalInfo3,
                CreatedOn = transaction.CreatedOn
            };
        }

        #endregion
    }
}

