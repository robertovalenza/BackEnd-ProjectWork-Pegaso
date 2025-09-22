using System.IdentityModel.Tokens.Jwt;
using Backend_Banca_Aurora.Data;
using Backend_Banca_Aurora.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<LoanDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddScoped<IDecisionService, DecisionService>();
builder.Services.AddHttpClient();
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

var authority = builder.Configuration["Auth:Authority"]!;
var audience = builder.Configuration["Auth:Audience"]!;


builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
  .AddJwtBearer(o =>
  {
      o.Authority = builder.Configuration["Auth:Authority"];
      o.RequireHttpsMetadata = false;
      o.TokenValidationParameters = new TokenValidationParameters
      {
          ValidateIssuer = true,
          ValidateAudience = true,
          ValidAudience = builder.Configuration["Auth:Audience"], 
          ClockSkew = TimeSpan.FromMinutes(2)
      };
      o.Events = new JwtBearerEvents
      {
          OnAuthenticationFailed = ctx => {
              Console.WriteLine("[JWT] Auth failed: " + ctx.Exception.Message);
              return Task.CompletedTask;
          },
          OnChallenge = ctx => {
              Console.WriteLine("[JWT] Challenge: " + ctx.ErrorDescription);
              return Task.CompletedTask;
          },
          OnTokenValidated = ctx => {
              Console.WriteLine("[JWT] OK sub=" + ctx.Principal?.FindFirst("sub")?.Value);
              return Task.CompletedTask;
          }
      };
  });

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy
            .WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()
    );
});


builder.Services.AddAuthorization();
builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Loan API", Version = "v1" });
    c.AddSecurityDefinition("bearerAuth", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Name = "Authorization",
        Description = "Bearer {token}"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type=ReferenceType.SecurityScheme, Id="bearerAuth" } }, new string[]{} }
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LoanDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
