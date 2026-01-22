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
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OCPP.Core.Database;
using OCPP.Core.Management.Services;

namespace OCPP.Core.Management
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }


        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOCPPDbContext(Configuration);

            services.AddControllersWithViews();

            // Configure CORS for API access
            services.AddCors(options =>
            {
                options.AddPolicy("AllowAngularApp", builder =>
                {
                    builder.WithOrigins("http://localhost:4200", "https://localhost:4200")
                           .AllowCredentials()
                           .AllowAnyHeader()
                           .AllowAnyMethod()
                           .SetIsOriginAllowed(origin => true); // For development
                });
            });

            services.AddLocalization(opts => { opts.ResourcesPath = "Resources"; });
            services.AddMvc()
                .AddViewLocalization(
                    LanguageViewLocationExpanderFormat.Suffix,
                    opts => { opts.ResourcesPath = "Resources"; })
                .AddDataAnnotationsLocalization();

            // JWT Authentication Configuration
            var jwtSecret = Configuration["JwtSettings:Secret"] ?? "YourSuperSecretKeyThatIsAtLeast32CharactersLong!";
            var jwtIssuer = Configuration["JwtSettings:Issuer"] ?? "OCPPCore";
            var jwtAudience = Configuration["JwtSettings:Audience"] ?? "OCPPCoreUsers";
            var key = Encoding.ASCII.GetBytes(jwtSecret);

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false; // Set to true in production
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = jwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = jwtAudience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                // Support token from both cookie (for admin panel) and Authorization header (for API)
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        // First check Authorization header
                        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        {
                            context.Token = authHeader.Substring("Bearer ".Length).Trim();
                        }
                        // If no Authorization header, check cookie (for admin panel)
                        else if (context.Request.Cookies.ContainsKey("accessToken"))
                        {
                            context.Token = context.Request.Cookies["accessToken"];
                        }
                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                        {
                            context.Response.Headers.Add("Token-Expired", "true");
                        }
                        return Task.CompletedTask;
                    },
                    OnChallenge = context =>
                    {
                        // For web pages, redirect to login
                        if (context.Request.Path.StartsWithSegments("/Home") || 
                            context.Request.Path.StartsWithSegments("/ChargePoint") ||
                            !context.Request.Path.StartsWithSegments("/api"))
                        {
                            context.HandleResponse();
                            context.Response.Redirect("/Account/Login");
                        }
                        return Task.CompletedTask;
                    }
                };
            });

            services.AddAuthorization(options =>
            {
                options.AddPolicy("AdminOnly", policy => policy.RequireRole("Administrator", "Admin"));
                options.AddPolicy("UserOrAdmin", policy => policy.RequireRole("User", "Administrator", "Admin"));
            });

            services.AddScoped<IJwtService, JwtService>();
            services.AddScoped<IUserManager, UserManager>();
            services.AddScoped<IFileStorageService, FileStorageService>();
            services.AddDistributedMemoryCache();
            
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "OCPP.Core Management API",
                    Version = "v1",
                    Description = "API for managing OCPP charging infrastructure, sessions, and users",
                    Contact = new OpenApiContact
                    {
                        Name = "OCPP.Core",
                        Url = new Uri("https://github.com/dallmann-consulting/OCPP.Core")
                    },
                    License = new OpenApiLicense
                    {
                        Name = "GPL-3.0",
                        Url = new Uri("https://www.gnu.org/licenses/gpl-3.0.html")
                    }
                });

                // Add JWT Authentication to Swagger
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();

            app.UseCors("AllowAngularApp");

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "OCPP.Core Management API v1");
                c.RoutePrefix = "swagger";
                c.DocumentTitle = "OCPP.Core API Documentation";
                c.DisplayRequestDuration();
            });

            app.UseAuthentication();
            app.UseRouting();
            app.UseAuthorization();

            var supportedCultures = new[] { "en", "de" };
            var localizationOptions = new RequestLocalizationOptions().SetDefaultCulture(supportedCultures[0])
                .AddSupportedCultures(supportedCultures)
                .AddSupportedUICultures(supportedCultures);
            app.UseRequestLocalization(localizationOptions);

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}/{connectorId?}/{param?}/");
            });
        }
    }
}
