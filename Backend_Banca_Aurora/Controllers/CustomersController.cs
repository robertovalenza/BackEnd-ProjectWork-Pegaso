using System.Security.Claims;
using Backend_Banca_Aurora.Data;
using Backend_Banca_Aurora.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens; 
namespace Backend_Banca_Aurora.Controllers;

[ApiController]
[Route("customers")]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly LoanDbContext _db;
    public CustomersController(LoanDbContext db) { _db = db; }

    [HttpGet("GetOwnCustomerData")]
    public async Task<IActionResult> GetMe()
    {
        var sub = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(sub)) return Unauthorized();

        var c = await _db.Customers.AsNoTracking()
            .Where(x => x.KeycloakUserId == sub)
            .Select(x => new
            {
                customerId = x.CustomerId,
                firstName = x.FirstName,
                lastName = x.LastName,
                fiscalCode = x.FiscalCode,
                incomeMonthly = x.IncomeMonthly
            })
            .FirstOrDefaultAsync();

        return c is null ? NotFound() : Ok(c);
    }

    [HttpPost("CreateCustomer")]
    public async Task<IActionResult> CreateMe([FromBody] CreateCustomerDto dto)
    {
        var sub = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(sub)) return Unauthorized();

        if (await _db.Customers.AnyAsync(x => x.KeycloakUserId == sub))
            return Conflict(new { message = "Il profilo esiste già per questo utente" });

        if (await _db.Customers.AnyAsync(x => x.FiscalCode == dto.FiscalCode))
            return Conflict(new { message = "Codice Fiscale già presente" });

        var c = new Customer
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            FiscalCode = dto.FiscalCode,
            IncomeMonthly = dto.IncomeMonthly,
            KeycloakUserId = sub
        };
        _db.Customers.Add(c);
        await _db.SaveChangesAsync();
        return Created($"/customers/{c.CustomerId}", new { customerId = c.CustomerId });
    }

    [HttpPut("UpdateIncomeMonthly")]
    public async Task<IActionResult> UpdateMyIncome([FromBody] UpdateIncomeDto dto)
    {
        string? sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                   ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(sub)) return Unauthorized();

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.KeycloakUserId == sub);
        if (customer is null) return NotFound(new { message = "Profilo cliente non trovato. Creane uno prima." });

        customer.IncomeMonthly = dto.IncomeMonthly;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            customerId = customer.CustomerId,
            incomeMonthly = customer.IncomeMonthly
        });

    }
}