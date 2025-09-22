using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Banca_Aurora.Models;

public enum ApplicationStatus { DRAFT, SUBMITTED, APPROVED, DECLINED, OFFERED, ACCEPTED }

public class Customer
{
    [Key] public Guid CustomerId { get; set; } = Guid.NewGuid();
    [Required, MaxLength(80)] public string FirstName { get; set; } = default!;
    [Required, MaxLength(80)] public string LastName { get; set; } = default!;
    [Required, MaxLength(32)] public string FiscalCode { get; set; } = default!;
    [Column(TypeName = "numeric(12,2)")] public decimal IncomeMonthly { get; set; }
    [Required, MaxLength(64)] public string KeycloakUserId { get; set; } = default!;
}

public class LoanApplication
{
    [Key] public Guid ApplicationId { get; set; } = Guid.NewGuid();
    [Required] public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }
    [Column(TypeName = "numeric(12,2)")] public decimal Amount { get; set; }
    public int Months { get; set; }
    [MaxLength(40)] public string? Purpose { get; set; }
    public ApplicationStatus Status { get; set; } = ApplicationStatus.SUBMITTED;
    public int? Score { get; set; }
    [Column(TypeName = "numeric(5,2)")] public decimal? Apr { get; set; }
    [Column(TypeName = "numeric(12,2)")] public decimal? MonthlyPayment { get; set; }
}

public record CreateCustomerDto(string FirstName, string LastName, string FiscalCode, decimal IncomeMonthly);
public record CreateApplicationDto(Guid CustomerId, decimal Amount, int Months, string? Purpose);
public record DecisionResultDto(string Status, decimal? Apr, decimal? MonthlyPayment, int? Score);
public record LoginRequest(string Username, string Password);
public record RegisterRequest(string Username, string Email, string Password, string? FirstName, string? LastName);
public record LogoutRequest(string RefreshToken);
public record UpdateIncomeDto([Range(0, 1_000_000, ErrorMessage = "Valore non valido")] decimal IncomeMonthly);
public record RefreshRequest(string RefreshToken);
