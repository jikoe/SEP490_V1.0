using AgriSustain.Domain.Entities;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Application.Interfaces.UnitOfWork;
using AgriSustain.Application.Services;
using AgriSustain.Application.Services.Authentication;
using AgriSustain.Application.Services.Orders;
using AgriSustain.Application.Services.Profiles;
using AgriSustain.Application.Validators.Authentication;
using AgriSustain.Application.Validators.Orders;
using AgriSustain.Application.Validators.Profiles;
using AgriSustain.Application.DTOs.Authentication;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.DTOs.Profiles;
using AgriSustain.Infrastructure.Data.Demo;
using AgriSustain.Infrastructure.Repositories;
using AgriSustain.Infrastructure.UnitOfWork;
using AgriSustain.Api.Security.Authentication;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AgriSustainContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MyCnn")));

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();

// Existing team auth services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// Standard Repositories and UnitOfWork
builder.Services.AddSingleton<SqlDemoDependencies>();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IProfileRepository, ProfileRepository>();
builder.Services.AddScoped<IOrderTrackingRepository, OrderTrackingRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// DuND Application Services
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IOrderTrackingService, OrderTrackingService>();

// DuND Validators
builder.Services.AddScoped<IValidator<RegisterRequest>, RegisterRequestValidator>();
builder.Services.AddScoped<IValidator<VerifyOtpRequest>, VerifyOtpRequestValidator>();
builder.Services.AddScoped<IValidator<ResendOtpRequest>, ResendOtpRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateConsumerProfileRequest>, UpdateConsumerProfileValidator>();
builder.Services.AddScoped<IValidator<UpdateFarmerProfileRequest>, UpdateFarmerProfileValidator>();
builder.Services.AddScoped<IValidator<OrderQuery>, OrderQueryValidator>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["JWT:ValidIssuer"],
        ValidAudience = builder.Configuration["JWT:ValidAudience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JWT:Secret"]!))
    };
});
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
