
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using WalletLedger.Api.Application.Interfaces;
using WalletLedger.Api.Application.Services;
using WalletLedger.Api.Auth;
using WalletLedger.Api.Data;
using WalletLedger.Api.Middleware;

namespace WalletLedger.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Just for building the class, the actual obj of the class i.e app uses other methods which act as middlewares
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. Enter your token in the text input below (without 'Bearer' prefix).",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "Bearer",
                    BearerFormat = "JWT"
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
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

            // Add DbContext
            builder.Services.AddDbContext<WalletLedgerDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


            builder.Services.AddScoped<IWalletService, WalletService>();
            builder.Services.AddScoped<ILedgerService, LedgerService>();

            // from appsettings pick up the jwt section
            var jwtSettings = builder.Configuration.GetSection("Jwt");

            // Configure JWT to preserve original claim names (don't map to Microsoft claim types)
            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
            JwtSecurityTokenHandler.DefaultOutboundClaimTypeMap.Clear();

            //add authentication with validation for issuer, aud, expiry and the key. all of which we are getting from jwtsettings["something"]
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,//must validate isser coming from below ValidIssuer
                        ValidateAudience = true, // same
                        ValidateLifetime = true,//expiry 
                        ValidateIssuerSigningKey = true,//key must be right

                        ValidIssuer = jwtSettings["Issuer"],
                        ValidAudience = jwtSettings["Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(
                                Encoding.UTF8.GetBytes(jwtSettings["Key"]!)
                        ),
                        // Map the 'sub' claim to ClaimTypes.NameIdentifier for easier access
                        NameClaimType = JwtRegisteredClaimNames.Sub,
                        // Explicitly set role claim type so authorization policies can find roles
                        RoleClaimType = ClaimTypes.Role
                    };
                });

            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("UserOnly", policy =>
                    policy.RequireRole(Roles.User));

                options.AddPolicy("AdminOnly", policy =>
                    policy.RequireRole(Roles.Admin));
            });


            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthentication();

            app.UseAuthorization();


            app.UseMiddleware<ExceptionHandlingMiddleware>();

            app.MapControllers();

            app.Run();
        }
    }
}
