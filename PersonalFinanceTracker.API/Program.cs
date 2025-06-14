using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FluentValidation;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using PersonalFinanceTrackerAPI;
using PersonalFinanceTrackerAPI.Data;
using PersonalFinanceTrackerAPI.Interfaces;
using PersonalFinanceTrackerAPI.Models;
using PersonalFinanceTrackerAPI.Repositories;
using PersonalFinanceTrackerAPI.Services;
using PersonalFinanceTrackerAPI.Filters;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Extensions;
using Swashbuckle.AspNetCore.SwaggerUI;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

string connectionString;
if (builder.Environment.IsDevelopment())
{
  connectionString = builder.Configuration.GetConnectionString("DevelopmentPostgreSQLConnection");
}
else
{
  connectionString = builder.Configuration.GetConnectionString("PostgreSQLConnection");
}

builder.Services.AddControllers()
  .AddJsonOptions(options =>
  {
    //options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.JsonSerializerOptions.Converters.Add(new DescriptionEnumConverter<AccountType>());
  });

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IFinancialGoalsRepository, FinancialGoalRepository>();
builder.Services.AddScoped<IFinancialGoalService, FinancialGoalService>();
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<IAccountService, AccountService>();

// Health check
builder.Services.AddHealthChecks().AddNpgSql(
  connectionString!, name: "Neon PostgreSQL");

// Add Authentication
builder.Services.AddAuthentication().AddBearerToken(IdentityConstants.BearerScheme);

//Add Authorization
builder.Services.AddAuthorization(options =>
{
  options.AddPolicy("AllowAnonymous", policy => policy.RequireAssertion(_ => true));
});

// Add Rate Limiting
builder.Services.AddRateLimiter(opt =>
{
  opt.AddSlidingWindowLimiter("SlidingWindowPolicy", opt =>
  {
    opt.Window = TimeSpan.FromSeconds(10);
    opt.PermitLimit = 4;
    opt.QueueLimit = 3;
    opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    opt.SegmentsPerWindow = 3;
  }).RejectionStatusCode = 429;

  opt.AddFixedWindowLimiter("HealthCheckPolicy", fixedOptions =>
 {
   fixedOptions.Window = TimeSpan.FromMinutes(1);
   fixedOptions.PermitLimit = 4;
   fixedOptions.QueueLimit = 2;
   fixedOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
 }).RejectionStatusCode = 429;
});

// Add Versioning
builder.Services.AddApiVersioning(opt =>
{
  opt.AssumeDefaultVersionWhenUnspecified = true;
  opt.DefaultApiVersion = new ApiVersion(1, 0);
  opt.ReportApiVersions = true;
  opt.ApiVersionReader = new UrlSegmentApiVersionReader();
});
// Config DbContext
builder.Services.AddDbContext<AppDbContext>(opt =>
{
  opt.UseNpgsql(connectionString);
});

builder.Services.AddIdentityCore<AppUser>(options =>
  {
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 8;
    options.Password.RequiredUniqueChars = 1;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.RequireUniqueEmail = true;
    
    // SignIn settings
    options.SignIn.RequireConfirmedEmail = false;  
  })
  .AddEntityFrameworkStores<AppDbContext>()
  .AddApiEndpoints();

builder.Services.AddSwaggerGen(c =>
{
  c.SwaggerDoc("v1", new OpenApiInfo
  {
    Title = "PersonalFinanceTrackerAPI",
    Version = "v1",
    Description = "API para el seguimiento de finanzas personales",
    Contact = new OpenApiContact
    {
      Name = "Miguel Astorga",
      Email = "mastorga.leiva@gmail.com",
      Url = new Uri("https://www.linkedin.com/in/mastorga-leiva/")
    }
  });
  //c.DocumentFilter<HealthCheckSwaggerFilter>();
  c.MapType<AccountType>(() => new OpenApiSchema { Type = "string" });
  c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
  {
    In = ParameterLocation.Header,
    Description = "Please enter your token",
    Name = "Authorization",
    Type = SecuritySchemeType.ApiKey
  });
  c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            }, new string[] {} }
    });

});
// Fluent Validation

builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly,includeInternalTypes:true);
builder.Services.AddFluentValidationAutoValidation();

// Add COR S
builder.Services.AddCors(options =>
{
  options.AddDefaultPolicy(builder =>
  {
    builder.AllowAnyOrigin()
             .AllowAnyMethod()
             .AllowAnyHeader();
  });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
  app.UseSwagger();
  app.UseSwaggerUI();
}
else
{
  app.UseSwagger();
  app.UseSwaggerUI(c =>
  {
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "PersonalFinanceTrackerAPI v1");
    // c.RoutePrefix = string.Empty;
    c.RoutePrefix = "api-docs"; // Cambiar la ruta por seguridad
    c.DocExpansion(DocExpansion.List); 
  });
}

app.UseCors();
app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
  context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
  context.Response.Headers.Append("X-Frame-Options", "DENY");
  context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
  context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
  context.Response.Headers.Append("X-Permitted-Cross-Domain-Policies", "none");
  await next();
});
app.MapHealthChecks("/health", new HealthCheckOptions()
{
  ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
  ResultStatusCodes =
  {
    [HealthStatus.Healthy] = StatusCodes.Status200OK,
    [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
    [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
  }
});//.RequireAuthorization().RequireRateLimiting("HealthCheckPolicy");
// app.UseAuthentication();
// app.UseAuthorization();
app.UseRateLimiter();
app.MapIdentityApi<AppUser>();
app.MapControllers();
app.Run();
