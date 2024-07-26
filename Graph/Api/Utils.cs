using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;

namespace Graph.Api;

public static class Utils
{
    public static decimal? ToDecimal(string? value, string key)
    {
        if (value == null) return null;

        if (decimal.TryParse(value, out var result))
            return result;

        throw new GraphQLException($"Could not parse {value} as a decimal (key = {key}).");
    }

    public static List<decimal?>? ToDecimalList(List<string?>? value, string key)
    {
        if (value == null) return null;

        var decimals = new List<decimal?>();
        foreach (var str in value)
            decimals.Add(ToDecimal(str, key));

        return decimals;
    }

    public static readonly string PartyIdClaim = "partyId";
    public static readonly string PartyNameClaim = "partyName";
    public static readonly string AllowedActionsClaim = "allowedActions";
    public static readonly string SchemaInstanceIdClaim = "schemaInstanceId";
    public static string GenerateToken(int userId, string[] permissions)
    {

        var secret = "5I2kE3aVacKZ19UObPSMvShikjigTyhc";
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

        var issuer = "https://localhost:5001";
        var audience = "https://localhost:8000";

        var tokenHandler = new JwtSecurityTokenHandler();
        var tz = new DateTime(1970, 1, 1, 0, 0, 0, 0);
        var exp = tz.AddHours(1);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new Claim[]
            {
                new Claim("userId", userId.ToString()),
                new Claim("permissions", string.Join(",", permissions)),
                new Claim("user", "Ryan")
            }),
            NotBefore = tz,
            Expires = exp,
            IssuedAt = tz,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        var splitToken = tokenString.Split('.');
        var header = splitToken[0];
        var payload = splitToken[1];
        var signature = splitToken[2];

        Console.WriteLine($"Header: {header}");
        Console.WriteLine($"Payload: {payload}");
        Console.WriteLine($"Signature: {signature}");

        return tokenString;

    }

    public static string GenerateTokenRaw(int userId, string[] permissions)
    {

        var secret = "5I2kE3aVacKZ19UObPSMvShikjigTyhc";
        var secretBytes = Encoding.UTF8.GetBytes(secret);
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

        var headers = new Dictionary<string, object>();
        headers.Add("alg", "HS256");
        headers.Add("typ", "JWT");

        var tz = new DateTime(1970, 1, 1, 0, 0, 0, 0);
        var exp = tz.AddHours(1);

        var dtoTz = new DateTimeOffset(tz);
        var dtoExp = new DateTimeOffset(exp);

        var payload = new Dictionary<string, object>();
        payload.Add("userId", userId.ToString());
        payload.Add("permissions", string.Join(",", permissions));
        payload.Add("user", "Ryan");
        payload.Add("nbf", dtoTz.ToUnixTimeSeconds());
        payload.Add("exp", dtoExp.ToUnixTimeSeconds());
        payload.Add("iat", dtoTz.ToUnixTimeSeconds());
        payload.Add("iss", "https://localhost:5001");
        payload.Add("aud", "https://localhost:8000");

        var jsonHeaders = JsonSerializer.Serialize(headers);
        var jsonPayload = JsonSerializer.Serialize(payload);

        var base64Headers = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonHeaders));
        var base64Payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonPayload));

        var msBase64Headers = Base64UrlEncoder.Encode(jsonHeaders);
        var msBase64Payload = Base64UrlEncoder.Encode(jsonPayload);

        var combinedBase64 = msBase64Headers + "." + msBase64Payload;

        var combinedBase64Bytes = Encoding.UTF8.GetBytes(combinedBase64);

        var hmac = new HMACSHA256(secretBytes);

        var signature = hmac.ComputeHash(combinedBase64Bytes);

        var base64Signature = Convert.ToBase64String(signature);
        var msBase64Signature = Base64UrlEncoder.Encode(signature);

        var token = msBase64Headers + "." + msBase64Payload + "." + msBase64Signature;

        Console.WriteLine($"Header: {base64Headers}");
        Console.WriteLine($"Payload: {base64Payload}");
        Console.WriteLine($"Signature: {base64Signature}");

        return token;
    }

    public static bool ValidateToken(string token)
    {
        var secret = "5I2kE3aVacKZ19UObPSMvShikjigTyhc";
        var secretBytes = Encoding.UTF8.GetBytes(secret);
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

        var tokenHandler = new JwtSecurityTokenHandler();

        try
        {
            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = securityKey,
                ValidateIssuer = true,
                ValidIssuer = "https://localhost:5001",
                ValidateAudience = true,
                ValidAudience = "https://localhost:8000",
                ValidateLifetime = false,
                RefreshBeforeValidation = false,
                RequireExpirationTime = false,
            }, out SecurityToken validatedToken);
        }
        catch
        {
            return false;
        }
        return true;
    }

    public static string GetClaim(string token, string claim)
    {
        if (ValidateToken(token) == false)
            throw new Exception("Invalid token.");

        var tokenHandler = new JwtSecurityTokenHandler();

        var jwtToken = tokenHandler.ReadJwtToken(token);

        var claimValue = jwtToken.Claims.First(c => c.Type == claim).Value;

        return claimValue;
    }
}
