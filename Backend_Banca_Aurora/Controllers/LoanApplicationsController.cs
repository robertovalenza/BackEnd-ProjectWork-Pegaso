using System;
using System.Threading.Tasks;
using Backend_Banca_Aurora.Data;       
using Backend_Banca_Aurora.Models;     
using Backend_Banca_Aurora.Services;   
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend_Banca_Aurora.Controllers
{
    [ApiController]
    [Route("loan-applications")]
    [Authorize]
    public class LoanApplicationsController : ControllerBase
    {
        private readonly LoanDbContext _db;
        private readonly IDecisionService _decision;

        public LoanApplicationsController(LoanDbContext db, IDecisionService decision)
        {
            _db = db;
            _decision = decision;
        }

        [HttpPost("SendLoanRequest")]
        public async Task<IActionResult> Create([FromBody] CreateApplicationDto dto)
        {
            var customer = await _db.Customers.FindAsync(dto.CustomerId);
            if (customer is null)
                return NotFound(new { message = "Cliente non trovato" });

            var app = new LoanApplication
            {
                CustomerId = dto.CustomerId,
                Amount = dto.Amount,
                Months = dto.Months,
                Purpose = dto.Purpose,
                Status = ApplicationStatus.SUBMITTED
            };

            _db.LoanApplications.Add(app);
            await _db.SaveChangesAsync();

            return Created($"/loan-applications/{app.ApplicationId}",
                new { applicationId = app.ApplicationId, status = app.Status.ToString() });
        }

        [HttpGet("GetLoanRequested/{id:guid}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var app = await _db.LoanApplications.AsNoTracking()
                .FirstOrDefaultAsync(a => a.ApplicationId == id);

            if (app is null) return NotFound();

            return Ok(new
            {
                applicationId = app.ApplicationId,
                customerId = app.CustomerId,
                amount = app.Amount,
                months = app.Months,
                purpose = app.Purpose,
                status = app.Status.ToString(),
                score = app.Score,
                apr = app.Apr,
                monthlyPayment = app.MonthlyPayment
            });
        }

        [HttpPost("{id:guid}/decision")]
        public async Task<IActionResult> Decide(Guid id)
        {
            var app = await _db.LoanApplications.FindAsync(id);
            if (app is null) return NotFound();

            var customer = await _db.Customers.FindAsync(app.CustomerId);
            if (customer is null) return BadRequest(new { message = "Cliente non trovato" });

            var res = _decision.Decide(customer, app);

            if (!Enum.TryParse<ApplicationStatus>(res.Status, out var newStatus))
                newStatus = ApplicationStatus.SUBMITTED;

            app.Status = newStatus;
            app.Score = res.Score;
            app.Apr = res.Apr;
            app.MonthlyPayment = res.MonthlyPayment;

            await _db.SaveChangesAsync();
            return Ok(res);
        }

        [HttpGet("GetLoans")]
        public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] Guid? customerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string sort = "createdDesc")
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

            var q = _db.LoanApplications.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(status) &&
                Enum.TryParse<ApplicationStatus>(status, true, out var st))
            {
                q = q.Where(a => a.Status == st);
            }

            if (customerId.HasValue)
                q = q.Where(a => a.CustomerId == customerId.Value);

            q = sort switch
            {
                "createdAsc" => q.OrderBy(a => a.ApplicationId), 
                "amountAsc" => q.OrderBy(a => a.Amount).ThenBy(a => a.ApplicationId),
                "amountDesc" => q.OrderByDescending(a => a.Amount).ThenBy(a => a.ApplicationId),
                _ => q.OrderByDescending(a => a.ApplicationId)
            };

            var total = await q.CountAsync();

            var items = await q
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new {
                    applicationId = a.ApplicationId,
                    customerId = a.CustomerId,
                    amount = a.Amount,
                    months = a.Months,
                    purpose = a.Purpose,
                    status = a.Status.ToString(),
                    score = a.Score,
                    apr = a.Apr,
                    monthlyPayment = a.MonthlyPayment
                })
                .ToListAsync();

            return Ok(new
            {
                page,
                pageSize,
                total,
                items
            });
        }
    }
}
