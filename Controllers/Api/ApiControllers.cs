using AcxiomCRM.Data;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api
{
    [ApiController]
    [Route("api")]
    public class ApiControllers : ControllerBase
    {
    }

    [ApiController]
    [Route("api/auth")]
    public class AuthApiController : ControllerBase
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _auditService;

        public AuthApiController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IAuditService auditService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _auditService = auditService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = await _userManager.FindByEmailAsync(request.EmailOrUsername)
                       ?? await _userManager.FindByNameAsync(request.EmailOrUsername);

            if (user == null || !user.IsActive)
            {
                await _auditService.LogAsync("Failed Login", "AuthApi", null, null, null, $"API login failed for {request.EmailOrUsername}", "Failed");
                return Unauthorized(new { message = "Invalid credentials or inactive account." });
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                await _signInManager.SignInAsync(user, isPersistent: true);
                var roles = await _userManager.GetRolesAsync(user);
                await _auditService.LogAsync("Login", "AuthApi", user.Id, null, null, $"API login successful for {user.Email}", "Success");

                return Ok(new
                {
                    message = "Login successful.",
                    userId = user.Id,
                    email = user.Email,
                    fullName = user.FullName,
                    roles
                });
            }

            if (result.IsLockedOut)
            {
                await _auditService.LogAsync("Failed Login (Lockout)", "AuthApi", user.Id, null, null, $"API account lockout for {user.Email}", "Failed");
                return StatusCode(StatusCodes.Status423Locked, new { message = "Account locked out due to multiple failed login attempts." });
            }

            await _auditService.LogAsync("Failed Login", "AuthApi", user.Id, null, null, $"API invalid password for {user.Email}", "Failed");
            return Unauthorized(new { message = "Invalid credentials." });
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return Ok(new { message = "Logged out successfully." });
        }
    }

    [ApiController]
    [Route("api/customers")]
    [Authorize]
    public class CustomersApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserService _userService;
        private readonly IAuditService _auditService;

        public CustomersApiController(ApplicationDbContext context, IUserService userService, IAuditService auditService)
        {
            _context = context;
            _userService = userService;
            _auditService = auditService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CustomerDto>>> GetCustomers([FromQuery] string? search)
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Unauthorized();

            var role = await _userService.GetPrimaryRoleAsync(user);
            var query = _context.Customers.Include(c => c.Owner).AsQueryable();

            if (role != Roles.Admin && role != Roles.Manager)
            {
                query = query.Where(c => c.OwnerId == user.Id || c.CreatedBy == user.Id);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(c => c.CustomerName.Contains(search) || c.Email.Contains(search) || c.Phone.Contains(search));
            }

            var list = await query.Select(c => new CustomerDto
            {
                CustomerId = c.CustomerId,
                CustomerCode = c.CustomerCode,
                CustomerName = c.CustomerName,
                Email = c.Email,
                Phone = c.Phone,
                CompanyName = c.CompanyName,
                Address = c.Address,
                City = c.City,
                State = c.State,
                Status = c.Status,
                CreatedDate = c.CreatedDate,
                OwnerName = c.Owner != null ? c.Owner.FullName : null
            }).ToListAsync();

            return Ok(list);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CustomerDto>> GetCustomer(int id)
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Unauthorized();

            var customer = await _context.Customers.Include(c => c.Owner).FirstOrDefaultAsync(c => c.CustomerId == id);
            if (customer == null) return NotFound(new { message = $"Customer with ID {id} not found." });

            var role = await _userService.GetPrimaryRoleAsync(user);
            if (role != Roles.Admin && role != Roles.Manager && customer.OwnerId != user.Id && customer.CreatedBy != user.Id)
            {
                return Forbid();
            }

            return Ok(new CustomerDto
            {
                CustomerId = customer.CustomerId,
                CustomerCode = customer.CustomerCode,
                CustomerName = customer.CustomerName,
                Email = customer.Email,
                Phone = customer.Phone,
                CompanyName = customer.CompanyName,
                Address = customer.Address,
                City = customer.City,
                State = customer.State,
                Status = customer.Status,
                CreatedDate = customer.CreatedDate,
                OwnerName = customer.Owner?.FullName
            });
        }

        [HttpPost]
        public async Task<ActionResult<CustomerDto>> CreateCustomer([FromBody] CreateCustomerDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Unauthorized();

            if (await _context.Customers.AnyAsync(c => c.Email.ToLower() == dto.Email.ToLower()))
            {
                return Conflict(new { message = "Customer with this email address already exists." });
            }

            if (await _context.Customers.AnyAsync(c => c.Phone == dto.Phone))
            {
                return Conflict(new { message = "Customer with this phone number already exists." });
            }

            var customer = new Customer
            {
                CustomerCode = $"CUST-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(1000, 9999)}",
                CustomerName = dto.CustomerName,
                Email = dto.Email,
                Phone = dto.Phone,
                CompanyName = dto.CompanyName,
                Address = dto.Address,
                City = dto.City,
                State = dto.State,
                Status = dto.Status,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = user.Id,
                OwnerId = user.Id
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Create", "Customer", customer.CustomerId.ToString(), null, customer, $"API created customer {customer.CustomerName}");

            return CreatedAtAction(nameof(GetCustomer), new { id = customer.CustomerId }, new CustomerDto
            {
                CustomerId = customer.CustomerId,
                CustomerCode = customer.CustomerCode,
                CustomerName = customer.CustomerName,
                Email = customer.Email,
                Phone = customer.Phone,
                CompanyName = customer.CompanyName,
                Address = customer.Address,
                City = customer.City,
                State = customer.State,
                Status = customer.Status,
                CreatedDate = customer.CreatedDate,
                OwnerName = user.FullName
            });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCustomer(int id, [FromBody] UpdateCustomerDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Unauthorized();

            var customer = await _context.Customers.FindAsync(id);
            if (customer == null) return NotFound(new { message = $"Customer with ID {id} not found." });

            var role = await _userService.GetPrimaryRoleAsync(user);
            if (role != Roles.Admin && role != Roles.Manager && customer.OwnerId != user.Id && customer.CreatedBy != user.Id)
            {
                return Forbid();
            }

            if (await _context.Customers.AnyAsync(c => c.CustomerId != id && c.Email.ToLower() == dto.Email.ToLower()))
            {
                return Conflict(new { message = "Another customer exists with this email address." });
            }

            if (await _context.Customers.AnyAsync(c => c.CustomerId != id && c.Phone == dto.Phone))
            {
                return Conflict(new { message = "Another customer exists with this phone number." });
            }

            var oldState = new { customer.CustomerName, customer.Email, customer.Phone, customer.Status };

            customer.CustomerName = dto.CustomerName;
            customer.Email = dto.Email;
            customer.Phone = dto.Phone;
            customer.CompanyName = dto.CompanyName;
            customer.Address = dto.Address;
            customer.City = dto.City;
            customer.State = dto.State;
            customer.Status = dto.Status;

            await _context.SaveChangesAsync();
            await _auditService.LogAsync("Update", "Customer", customer.CustomerId.ToString(), oldState, customer, $"API updated customer {customer.CustomerName}");

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCustomer(int id)
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Unauthorized();

            var customer = await _context.Customers.FindAsync(id);
            if (customer == null) return NotFound(new { message = $"Customer with ID {id} not found." });

            var role = await _userService.GetPrimaryRoleAsync(user);
            if (role != Roles.Admin && role != Roles.Manager && customer.OwnerId != user.Id)
            {
                return Forbid();
            }

            customer.Status = "Inactive";
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Delete", "Customer", customer.CustomerId.ToString(), null, new { Status = "Inactive" }, $"API deactivated customer {customer.CustomerName}");

            return NoContent();
        }
    }

    [ApiController]
    [Route("api/leads")]
    [Authorize]
    public class LeadsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserService _userService;
        private readonly IAuditService _auditService;

        public LeadsApiController(ApplicationDbContext context, IUserService userService, IAuditService auditService)
        {
            _context = context;
            _userService = userService;
            _auditService = auditService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<LeadDto>>> GetLeads([FromQuery] string? search, [FromQuery] string? status)
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Unauthorized();

            var role = await _userService.GetPrimaryRoleAsync(user);
            var query = _context.Leads.Include(l => l.Assignee).AsQueryable();

            if (role != Roles.Admin && role != Roles.Manager)
            {
                query = query.Where(l => l.AssignedTo == user.Id);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(l => l.LeadName.Contains(search) || l.Email.Contains(search) || (l.CompanyName != null && l.CompanyName.Contains(search)));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(l => l.Status == status);
            }

            var list = await query.Select(l => new LeadDto
            {
                LeadId = l.LeadId,
                LeadCode = l.LeadCode,
                LeadName = l.LeadName,
                Email = l.Email,
                Phone = l.Phone,
                CompanyName = l.CompanyName,
                Source = l.Source,
                Status = l.Status,
                Priority = l.Priority,
                ExpectedValue = l.ExpectedValue,
                CreatedDate = l.CreatedDate,
                AssignedToName = l.Assignee != null ? l.Assignee.FullName : null
            }).ToListAsync();

            return Ok(list);
        }

        [HttpPost]
        public async Task<ActionResult<LeadDto>> CreateLead([FromBody] CreateLeadDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Unauthorized();

            if (!LeadStatuses.All.Contains(dto.Status))
            {
                return BadRequest(new { message = "Invalid lead status." });
            }

            var lead = new Lead
            {
                LeadCode = $"LEAD-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(1000, 9999)}",
                LeadName = dto.LeadName,
                Email = dto.Email,
                Phone = dto.Phone,
                CompanyName = dto.CompanyName,
                Source = dto.Source,
                Status = dto.Status,
                Priority = dto.Priority,
                ExpectedValue = dto.ExpectedValue,
                Notes = dto.Notes,
                CreatedDate = DateTime.UtcNow,
                AssignedTo = user.Id
            };

            _context.Leads.Add(lead);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Create", "Lead", lead.LeadId.ToString(), null, lead, $"API created lead {lead.LeadName}");

            return CreatedAtAction(nameof(GetLeads), new { id = lead.LeadId }, new LeadDto
            {
                LeadId = lead.LeadId,
                LeadCode = lead.LeadCode,
                LeadName = lead.LeadName,
                Email = lead.Email,
                Phone = lead.Phone,
                CompanyName = lead.CompanyName,
                Source = lead.Source,
                Status = lead.Status,
                Priority = lead.Priority,
                ExpectedValue = lead.ExpectedValue,
                CreatedDate = lead.CreatedDate,
                AssignedToName = user.FullName
            });
        }
    }

    [ApiController]
    [Route("api/opportunities")]
    [Authorize]
    public class OpportunitiesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserService _userService;
        private readonly IAuditService _auditService;

        public OpportunitiesApiController(ApplicationDbContext context, IUserService userService, IAuditService auditService)
        {
            _context = context;
            _userService = userService;
            _auditService = auditService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<OpportunityDto>>> GetOpportunities([FromQuery] string? stage)
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Unauthorized();

            var role = await _userService.GetPrimaryRoleAsync(user);
            var query = _context.Opportunities.Include(o => o.Customer).Include(o => o.Assignee).AsQueryable();

            if (role != Roles.Admin && role != Roles.Manager)
            {
                query = query.Where(o => o.AssignedTo == user.Id);
            }

            if (!string.IsNullOrWhiteSpace(stage))
            {
                query = query.Where(o => o.Stage == stage);
            }

            var list = await query.Select(o => new OpportunityDto
            {
                OpportunityId = o.OpportunityId,
                OpportunityName = o.OpportunityName,
                CustomerId = o.CustomerId,
                CustomerName = o.Customer != null ? o.Customer.CustomerName : null,
                Amount = o.Amount,
                Stage = o.Stage,
                Probability = o.Probability,
                WeightedAmount = (o.Amount * o.Probability) / 100m,
                ExpectedCloseDate = o.ExpectedCloseDate,
                Status = o.Status,
                CreatedDate = o.CreatedDate,
                AssignedToName = o.Assignee != null ? o.Assignee.FullName : null
            }).ToListAsync();

            return Ok(list);
        }

        [HttpPost]
        public async Task<ActionResult<OpportunityDto>> CreateOpportunity([FromBody] CreateOpportunityDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Unauthorized();

            if (dto.Amount <= 0)
            {
                return BadRequest(new { message = "Opportunity Amount must be greater than 0." });
            }

            if (dto.Probability < 0 || dto.Probability > 100)
            {
                return BadRequest(new { message = "Probability must be between 0 and 100." });
            }

            if (dto.ExpectedCloseDate.Date < DateTime.UtcNow.Date)
            {
                return BadRequest(new { message = "Expected Close Date cannot be in the past." });
            }

            var opp = new Opportunity
            {
                OpportunityName = dto.OpportunityName,
                CustomerId = dto.CustomerId,
                LeadId = dto.LeadId,
                Amount = dto.Amount,
                Stage = dto.Stage,
                Probability = dto.Probability,
                ExpectedCloseDate = dto.ExpectedCloseDate,
                Notes = dto.Notes,
                Status = dto.Stage == OpportunityStages.Won ? "Won" : (dto.Stage == OpportunityStages.Lost ? "Lost" : "Open"),
                CreatedDate = DateTime.UtcNow,
                AssignedTo = user.Id
            };

            _context.Opportunities.Add(opp);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Create", "Opportunity", opp.OpportunityId.ToString(), null, opp, $"API created opportunity {opp.OpportunityName}");

            return CreatedAtAction(nameof(GetOpportunities), new { id = opp.OpportunityId }, new OpportunityDto
            {
                OpportunityId = opp.OpportunityId,
                OpportunityName = opp.OpportunityName,
                CustomerId = opp.CustomerId,
                Amount = opp.Amount,
                Stage = opp.Stage,
                Probability = opp.Probability,
                WeightedAmount = (opp.Amount * opp.Probability) / 100m,
                ExpectedCloseDate = opp.ExpectedCloseDate,
                Status = opp.Status,
                CreatedDate = opp.CreatedDate,
                AssignedToName = user.FullName
            });
        }
    }

    [ApiController]
    [Route("api/followups")]
    [Authorize]
    public class FollowUpsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserService _userService;
        private readonly IAuditService _auditService;

        public FollowUpsApiController(ApplicationDbContext context, IUserService userService, IAuditService auditService)
        {
            _context = context;
            _userService = userService;
            _auditService = auditService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<FollowUpDto>>> GetFollowUps()
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Unauthorized();

            var role = await _userService.GetPrimaryRoleAsync(user);
            var query = _context.FollowUps.Include(f => f.Customer).Include(f => f.Lead).Include(f => f.Assignee).AsQueryable();

            if (role != Roles.Admin && role != Roles.Manager)
            {
                query = query.Where(f => f.AssignedTo == user.Id);
            }

            var list = await query.Select(f => new FollowUpDto
            {
                FollowUpId = f.FollowUpId,
                Subject = f.Subject,
                FollowUpDate = f.FollowUpDate,
                FollowUpType = f.FollowUpType,
                Status = f.Status,
                Remarks = f.Remarks,
                TargetName = f.Customer != null ? f.Customer.CustomerName : (f.Lead != null ? f.Lead.LeadName : null),
                AssignedToName = f.Assignee != null ? f.Assignee.FullName : null
            }).ToListAsync();

            return Ok(list);
        }

        [HttpPost]
        public async Task<ActionResult<FollowUpDto>> CreateFollowUp([FromBody] CreateFollowUpDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Unauthorized();

            if (dto.FollowUpDate.Date < DateTime.UtcNow.Date)
            {
                return BadRequest(new { message = "Follow-up date cannot be earlier than today." });
            }

            var f = new FollowUp
            {
                CustomerId = dto.CustomerId,
                LeadId = dto.LeadId,
                OpportunityId = dto.OpportunityId,
                Subject = dto.Subject,
                FollowUpDate = dto.FollowUpDate,
                FollowUpType = dto.FollowUpType,
                Remarks = dto.Remarks,
                Status = FollowUpStatuses.Planned,
                CreatedDate = DateTime.UtcNow,
                AssignedTo = user.Id
            };

            _context.FollowUps.Add(f);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Create", "FollowUp", f.FollowUpId.ToString(), null, f, $"API scheduled follow-up {f.Subject}");

            return CreatedAtAction(nameof(GetFollowUps), new { id = f.FollowUpId }, new FollowUpDto
            {
                FollowUpId = f.FollowUpId,
                Subject = f.Subject,
                FollowUpDate = f.FollowUpDate,
                FollowUpType = f.FollowUpType,
                Status = f.Status,
                Remarks = f.Remarks,
                AssignedToName = user.FullName
            });
        }
    }

    [ApiController]
    [Route("api/reports")]
    [Authorize]
    public class ReportsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserService _userService;

        public ReportsApiController(ApplicationDbContext context, IUserService userService)
        {
            _context = context;
            _userService = userService;
        }

        [HttpGet("pipeline")]
        public async Task<IActionResult> GetPipelineReport()
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Unauthorized();

            var role = await _userService.GetPrimaryRoleAsync(user);
            var query = _context.Opportunities.Include(o => o.Assignee).Where(o => o.Status == "Open").AsQueryable();

            if (role != Roles.Admin && role != Roles.Manager)
            {
                query = query.Where(o => o.AssignedTo == user.Id);
            }

            var opps = await query.ToListAsync();

            var totalAmount = opps.Sum(o => o.Amount);
            var weightedAmount = opps.Sum(o => (o.Amount * o.Probability) / 100m);

            var byStage = opps.GroupBy(o => o.Stage).ToDictionary(
                g => g.Key,
                g => new
                {
                    Count = g.Count(),
                    Amount = g.Sum(x => x.Amount),
                    Weighted = g.Sum(x => (x.Amount * x.Probability) / 100m)
                });

            return Ok(new
            {
                totalOpportunities = opps.Count,
                totalPipelineAmount = totalAmount,
                totalWeightedPipelineAmount = weightedAmount,
                stages = byStage
            });
        }
    }
}
