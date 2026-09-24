using AccessEase.Data;
using AccessEase.Models;
using AccessEase12.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccessEase12.Controllers
{
    [Authorize(Roles = "ProjectManager")]
    public class RemediationController : Controller
    {
        private readonly AccessEaseDbContext _db;

        public RemediationController(AccessEaseDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? projectFilter, int? userFilter, string? statusFilter)
        {
            var query = _db.RemediationIssues
                .Include(r => r.ScanRecord)
                    .ThenInclude(s => s!.AppUser)
                .Include(r => r.ScanRecord)
                    .ThenInclude(s => s!.Project)
                .AsQueryable();

            if (projectFilter.HasValue)
            {
                query = query.Where(r => r.ScanRecord != null && r.ScanRecord.ProjectId == projectFilter.Value);
            }

            if (userFilter.HasValue)
            {
                query = query.Where(r => r.ScanRecord != null && r.ScanRecord.AppUserId == userFilter.Value);
            }

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                query = query.Where(r => r.Status == statusFilter);
            }

            var issues = await query
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            ViewBag.Projects = await _db.Projects.OrderBy(p => p.Name).ToListAsync();
            ViewBag.Users = await _db.AppUsers
                .Where(u => u.Role == "Developer")
                .OrderBy(u => u.FullName)
                .ToListAsync();

            ViewBag.CurrentProjectFilter = projectFilter;
            ViewBag.CurrentUserFilter = userFilter;
            ViewBag.CurrentStatusFilter = statusFilter;

            ViewBag.PendingCount = issues.Count(i => i.Status == "Pending");
            ViewBag.InProgressCount = issues.Count(i => i.Status == "InProgress");
            ViewBag.OnHoldCount = issues.Count(i => i.Status == "OnHold");
            ViewBag.FixedCount = issues.Count(i => i.Status == "Fixed");

            return View(issues);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(RemediationUpdateViewModel model)
        {
            var allowedStatuses = new[] { "Pending", "InProgress", "OnHold", "Fixed" };

            if (!allowedStatuses.Contains(model.Status))
            {
                TempData["Error"] = "Invalid remediation status.";
                return RedirectToAction("Index");
            }

            var issue = await _db.RemediationIssues.FirstOrDefaultAsync(r => r.Id == model.Id);
            if (issue == null)
            {
                TempData["Error"] = "Remediation issue not found.";
                return RedirectToAction("Index");
            }

            issue.Status = model.Status;
            issue.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["Success"] = "Remediation status updated successfully.";
            return RedirectToAction("Index");
        }
    }
}