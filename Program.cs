using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using UPDSjudgeB.data;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// 1. Base de datos (PostgreSQL)
// ============================================================
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("UPDSjudge")));

// ============================================================
// 1.5. CORS (para que el frontend en React pueda consumir la API)
// ============================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDev", policy =>
    {
        policy.WithOrigins("http://localhost:8085")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
// ============================================================
// 2. Configuración de Seguridad (JWT)
// ============================================================
// Leer parámetros JWT desde appsettings.json
var jwtKey = builder.Configuration["Jwt:Key"] ?? "ClaveSecretaDeRespaldoDe32CaracteresMinimo";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "UPDSJudge";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "UPDSJudgeUsers";
var key = Encoding.UTF8.GetBytes(jwtKey);

// Registrar JwtService: servicio encargado únicamente de generar tokens
builder.Services.AddScoped<UPDSjudgeB.Services.JwtService>();

// Registrar AuthService: lógica de registro de usuarios
builder.Services.AddScoped<UPDSjudgeB.Services.AuthService>();

// Configurar autenticación JWT Bearer como esquema por defecto
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        // Validar Issuer y Audience para que coincidan con los valores usados al generar el token
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// Habilitar autorización basada en roles ([Authorize], [Authorize(Roles="...")])
builder.Services.AddAuthorization();

// ============================================================
// 3. Controladores y Swagger
// ============================================================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    // Usamos la ruta completa para evitar el error CS0234
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Mini Juez API",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Ingrese el token JWT"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ============================================================
// 4. Construcción y Middleware
// ============================================================
var app = builder.Build();

// Habilitar Swagger siempre en desarrollo para pruebas rápidas
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Mini Juez v1");
    });
}

app.UseHttpsRedirection();
app.UseCors("FrontendDev");
// El orden aquí es vida o muerte para el proyecto:
app.UseAuthentication(); // 1. ¿Quién eres?
app.UseAuthorization();  // 2. ¿Qué puedes hacer?

app.MapControllers();

app.Run();