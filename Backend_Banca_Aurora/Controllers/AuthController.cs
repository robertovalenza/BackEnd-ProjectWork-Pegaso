using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Backend_Banca_Aurora.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _cfg;
    private readonly IHttpClientFactory _httpFactory;

    public AuthController(IConfiguration cfg, IHttpClientFactory httpFactory)
    {
        _cfg = cfg;
        _httpFactory = httpFactory;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var authority = _cfg["Auth:Authority"]!.TrimEnd('/');
        var tokenUrl = $"{authority}/protocol/openid-connect/token";
        var client = _httpFactory.CreateClient();

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = _cfg["Auth:ClientId"]!,
            ["client_secret"] = _cfg["Auth:ClientSecret"]!,
            ["username"] = req.Username,
            ["password"] = req.Password
        };

        var resp = await client.PostAsync(tokenUrl, new FormUrlEncodedContent(form));
        var json = await resp.Content.ReadAsStringAsync();
        return Content(json, "application/json");
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        try
        {
            var authority = _cfg["Auth:Authority"]!.TrimEnd('/'); 
            if (!authority.Contains("/realms/"))
                return BadRequest(new { message = "Auth:Authority non contiene /realms/{realm}" });

            var parts = authority.Split("/realms/");
            var serverBase = parts[0]; 
            var realm = parts[1];      

            var tokenUrl = $"{authority}/protocol/openid-connect/token";
            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _cfg["Auth:ClientId"]!,
                ["client_secret"] = _cfg["Auth:ClientSecret"]!
            };

            var http = _httpFactory.CreateClient();
            var tokenResp = await http.PostAsync(tokenUrl, new FormUrlEncodedContent(form));
            var tokenBody = await tokenResp.Content.ReadAsStringAsync();
            if (!tokenResp.IsSuccessStatusCode)
                return StatusCode((int)tokenResp.StatusCode, new { step = "token", tokenUrl, tokenBody });

            string? adminToken;
            try
            {
                adminToken = JsonDocument.Parse(tokenBody).RootElement.GetProperty("access_token").GetString();
            }
            catch
            {
                return StatusCode(500, new { step = "token-parse", tokenBody });
            }
            if (string.IsNullOrWhiteSpace(adminToken))
                return StatusCode(500, new { step = "token-empty", tokenBody });

            var usersUrl = $"{serverBase}/admin/realms/{realm}/users";
            var admin = _httpFactory.CreateClient();
            admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            var createUser = new
            {
                username = req.Username,
                email = req.Email,
                firstName = req.FirstName,
                lastName = req.LastName,
                enabled = true,
                emailVerified = true 
            };
            var createBody = new StringContent(JsonSerializer.Serialize(createUser), Encoding.UTF8, "application/json");
            var cuResp = await admin.PostAsync(usersUrl, createBody);
            var cuText = await cuResp.Content.ReadAsStringAsync();

            if (cuResp.StatusCode == HttpStatusCode.Conflict)
                return Conflict(new { step = "create-user", message = "Questo utente esiste già" });
            if (!cuResp.IsSuccessStatusCode)
                return StatusCode((int)cuResp.StatusCode, new { step = "create-user", url = usersUrl, response = cuText });

            var userId = cuResp.Headers.Location?.ToString()?.Split('/').Last();
            if (string.IsNullOrWhiteSpace(userId))
                return StatusCode(500, new { step = "location-missing", location = cuResp.Headers.Location?.ToString() });

            var pwdUrl = $"{serverBase}/admin/realms/{realm}/users/{userId}/reset-password";
            var pwdPayload = new { type = "password", value = req.Password, temporary = false };
            var pwResp = await admin.PutAsync(pwdUrl,
                new StringContent(JsonSerializer.Serialize(pwdPayload), Encoding.UTF8, "application/json"));
            var pwText = await pwResp.Content.ReadAsStringAsync();

            if (!pwResp.IsSuccessStatusCode)
                return StatusCode((int)pwResp.StatusCode, new { step = "set-password", url = pwdUrl, response = pwText });

            return Created($"/auth/register/{userId}", new { userId });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "exception", ex.Message, ex.StackTrace });
        }
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.RefreshToken))
            return BadRequest(new { message = "Manca il refresh_token" });

        var authority = _cfg["Auth:Authority"]!.TrimEnd('/'); 
        var tokenUrl = $"{authority}/protocol/openid-connect/logout";

        var form = new Dictionary<string, string>
        {
            ["client_id"] = _cfg["Auth:ClientId"]!,
            ["client_secret"] = _cfg["Auth:ClientSecret"]!,
            ["refresh_token"] = req.RefreshToken
        };

        var http = _httpFactory.CreateClient();
        var resp = await http.PostAsync(tokenUrl, new FormUrlEncodedContent(form));
        if (!resp.IsSuccessStatusCode)
            return StatusCode((int)resp.StatusCode, await resp.Content.ReadAsStringAsync());

        return NoContent();
    }

    [HttpPost("refresh")]
    [AllowAnonymous] // il refresh non richiede l'access token, basta il refresh token
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.RefreshToken))
            return BadRequest(new { message = "Missing refresh_token" });

        var authority = _cfg["Auth:Authority"]!.TrimEnd('/'); // es: http://localhost:8080/realms/loan-realm
        var tokenUrl = $"{authority}/protocol/openid-connect/token";

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = _cfg["Auth:ClientId"]!,        // es: loan-api
            ["client_secret"] = _cfg["Auth:ClientSecret"]!,    // es: loan-api-secret
            ["refresh_token"] = req.RefreshToken
        };

        var http = _httpFactory.CreateClient();
        var resp = await http.PostAsync(tokenUrl, new FormUrlEncodedContent(form));
        var body = await resp.Content.ReadAsStringAsync();

        // Keycloak risponde con JSON: { access_token, refresh_token, expires_in, refresh_expires_in, token_type, ... }
        if (!resp.IsSuccessStatusCode)
            return StatusCode((int)resp.StatusCode, body);

        return Content(body, "application/json");
    }

}

