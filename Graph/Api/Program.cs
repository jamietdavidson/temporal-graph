using System.Text;
using Graph.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var secret = "5I2kE3aVacKZ19UObPSMvShikjigTyhc";
var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = false,
            RefreshBeforeValidation = false,
            RequireExpirationTime = false,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "https://localhost:5001",
            ValidAudience = "https://localhost:8000",
            IssuerSigningKey = securityKey
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CustomName", policy => policy.RequireAssertion(context => context.User.HasClaim("user", "Ryan")));
});
builder.Services
    .AddGraphQLServer()
    .AddAuthorization()
    .AddQueryType<QueryType>()
    .AddMutationType<MutationType>();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapGraphQL();
app.Run();