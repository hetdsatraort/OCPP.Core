/*
 * OCPP.Core - https://github.com/dallmann-consulting/OCPP.Core
 * Copyright (C) 2020-2021 dallmann consulting GmbH.
 * All Rights Reserved.
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Management.Models;
using OCPP.Core.Management.Services;
using Microsoft.EntityFrameworkCore;
using OCPP.Core.Database.EVCDTO;

namespace OCPP.Core.Management.Controllers
{
    public class AccountController : BaseController
    {
        private readonly IJwtService _jwtService;

        public AccountController(
            IUserManager userManager,
            ILoggerFactory loggerFactory,
            IConfiguration config,
            OCPPCoreContext dbContext,
            IJwtService jwtService) : base(userManager, loggerFactory, config, dbContext)
        {
            Logger = loggerFactory.CreateLogger<AccountController>();
            _jwtService = jwtService;
        }

        // GET: /Account/Login
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(UserModel userModel, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                try
                {
                    // Check if user exists in Users table (new JWT system)
                    var dbUser = await DbContext.Users
                        .FirstOrDefaultAsync(u => 
                            (u.EMailID == userModel.Username || u.PhoneNumber == userModel.Username) 
                            && u.Active == 1);

                    bool isAuthenticated = false;
                    bool isAdmin = false;
                    Users authenticatedUser = null;

                    if (dbUser != null)
                    {
                        // Verify password using SHA256 hash
                        var hashedPassword = HashPassword(userModel.Password);
                        if (dbUser.Password == hashedPassword)
                        {
                            isAuthenticated = true;
                            isAdmin = dbUser.UserRole == "Administrator" || dbUser.UserRole == "Admin";
                            authenticatedUser = dbUser;
                        }
                    }
                    else
                    {
                        // Fallback to config-based users for backward compatibility
                        IEnumerable<Microsoft.Extensions.Configuration.IConfigurationSection> cfgUsers = 
                            Config.GetSection("Users").GetChildren();

                        foreach (var cfgUser in cfgUsers)
                        {
                            if (cfgUser.GetValue<string>("Username") == userModel.Username &&
                                cfgUser.GetValue<string>("Password") == userModel.Password)
                            {
                                isAdmin = cfgUser.GetValue<bool>("Administrator");
                                isAuthenticated = true;

                                // Create a temporary user object for config-based users
                                authenticatedUser = new Users
                                {
                                    RecId = Guid.NewGuid().ToString(),
                                    FirstName = userModel.Username,
                                    LastName = "",
                                    EMailID = userModel.Username,
                                    PhoneNumber = "",
                                    UserRole = isAdmin ? "Administrator" : "User"
                                };
                                break;
                            }
                        }
                    }

                    if (isAuthenticated && authenticatedUser != null)
                    {
                        // Generate JWT token
                        var accessToken = _jwtService.GenerateAccessToken(authenticatedUser);
                        var refreshToken = _jwtService.GenerateRefreshToken(GetIpAddress());
                        refreshToken.UserId = authenticatedUser.RecId;

                        if(dbUser != null)
                        {
                            // Save refresh token to database
                            DbContext.RefreshTokens.Add(refreshToken);
                            dbUser.LastLogin = DateTime.UtcNow.ToString("o");
                            dbUser.UpdatedOn = DateTime.UtcNow;
                        }
                        
                        await DbContext.SaveChangesAsync();

                        // Set tokens as HTTP-only cookies
                        SetTokenCookies(accessToken, refreshToken.Token);

                        Logger.LogInformation("User '{0}' logged in successfully", userModel.Username);
                        WriteMessageLog("Login", $"Success - User '{userModel.Username}'");

                        return RedirectToLocal(returnUrl);
                    }
                    else
                    {
                        Logger.LogInformation("Invalid login attempt: User '{0}'", userModel.Username);
                        WriteMessageLog("Login", $"Failure - User '{userModel.Username}'");
                        ModelState.AddModelError(string.Empty, "Invalid login attempt");
                        return View(userModel);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error during login");
                    ModelState.AddModelError(string.Empty, "An error occurred during login");
                    return View(userModel);
                }
            }

            // If we got this far, something failed, redisplay form
            return View(userModel);
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var refreshToken = Request.Cookies["refreshToken"];

                if (!string.IsNullOrEmpty(refreshToken))
                {
                    var token = await DbContext.RefreshTokens
                        .FirstOrDefaultAsync(t => t.Token == refreshToken);

                    if (token != null)
                    {
                        token.RevokedAt = DateTime.UtcNow;
                        token.RevokedByIp = GetIpAddress();
                        await DbContext.SaveChangesAsync();
                    }
                }

                // Clear cookies
                Response.Cookies.Delete("accessToken");
                Response.Cookies.Delete("refreshToken");

                Logger.LogInformation("User logged out");
                WriteMessageLog("Logout", $"User logged out");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error during logout");
            }

            return RedirectToAction(nameof(Login), "Account");
        }

        private IActionResult RedirectToLocal(string returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction(nameof(HomeController.Index), Constants.HomeController);
            }
        }

        private string HashPassword(string password)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashedBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }

        private void SetTokenCookies(string accessToken, string refreshToken)
        {
            Response.Cookies.Append("accessToken", accessToken, new Microsoft.AspNetCore.Http.CookieOptions
            {
                HttpOnly = true,
                Secure = true, // Set to true in production with HTTPS
                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.None,
                Expires = DateTimeOffset.UtcNow.AddMinutes(15)
            });

            Response.Cookies.Append("refreshToken", refreshToken, new Microsoft.AspNetCore.Http.CookieOptions
            {
                HttpOnly = true,
                Secure = true, // Set to true in production with HTTPS
                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.None,
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

        private void WriteMessageLog(string message, string result)
        {
            try
            {
                MessageLog msgLog = new MessageLog();
                msgLog.ChargePointId = "AccountController";
                msgLog.LogTime = DateTime.UtcNow;
                msgLog.Message = message;
                msgLog.Result = result;
                DbContext.MessageLogs.Add(msgLog);
                DbContext.SaveChanges();
            }
            catch
            {
            }
        }
    }
}
