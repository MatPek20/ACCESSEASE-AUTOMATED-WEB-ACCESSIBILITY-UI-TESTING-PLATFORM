using System.Security.Claims;
using AccessEase.Data;
using AccessEase.Models;
using AccessEase12.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccessEase12.Controllers
{
    [Authorize(Roles = "ProjectManager")]
    public class BaselineController : Controller
    {
        private readonly AccessEaseDbContext _db;

        public BaselineController(AccessEaseDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? projectFilter)
        {
            var baselineQuery = _db.BaselineApprovals
                .Include(b => b.Project)
                .Include(b => b.ScanRecord)
                    .ThenInclude(s => s!.AppUser)
                .Include(b => b.ApprovedByUser)
                .AsQueryable();

            if (projectFilter.HasValue)
            {
                baselineQuery = baselineQuery.Where(b => b.ProjectId == projectFilter.Value);
            }

            var baselines = await baselineQuery
                .OrderByDescending(b => b.ApprovedAt)
                .ToListAsync();

            var candidateQuery = _db.ScanRecords
                .Include(s => s.Project)
                .Include(s => s.AppUser)
                .Where(s => s.ProjectId != null)
                .AsQueryable();

            if (projectFilter.HasValue)
            {
                candidateQuery = candidateQuery.Where(s => s.ProjectId == projectFilter.Value);
            }

            var candidates = await candidateQuery
                .OrderByDescending(s => s.ScanTime)
                .Take(50)
                .ToListAsync();

            ViewBag.Projects = await _db.Projects
                .OrderBy(p => p.Name)
                .ToListAsync();

            ViewBag.CurrentProjectFilter = projectFilter;
            ViewBag.CurrentBaselines = baselines;

            return View(candidates);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(BaselineApprovalViewModel model)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
                return RedirectToAction("Login", "Account");

            var scan = await _db.ScanRecords
                .Include(s => s.Project)
                .FirstOrDefaultAsync(s => s.Id == model.ScanRecordId);

            if (scan == null || scan.ProjectId == null || string.IsNullOrWhiteSpace(scan.Url))
            {
                TempData["Error"] = "Selected scan record is not valid for baseline approval.";
                return RedirectToAction("Index");
            }

            var existingBaseline = await _db.BaselineApprovals
                .FirstOrDefaultAsync(b => b.ProjectId == scan.ProjectId.Value && b.Url == scan.Url);

            if (existingBaseline == null)
            {
                var baseline = new BaselineApproval
                {
                    ProjectId = scan.ProjectId.Value,
                    Url = scan.Url,
                    ScanRecordId = scan.Id,
                    ApprovedByUserId = currentUserId,
                    ApprovedAt = DateTime.UtcNow,
                    Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim()
                };

                _db.BaselineApprovals.Add(baseline);
            }
            else
            {
                existingBaseline.ScanRecordId = scan.Id;
                existingBaseline.ApprovedByUserId = currentUserId;
                existingBaseline.ApprovedAt = DateTime.UtcNow;
                existingBaseline.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();
            }

            await _db.SaveChangesAsync();

            TempData["Success"] = "Baseline approved successfully.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int id)
        {
            var baseline = await _db.BaselineApprovals.FirstOrDefaultAsync(b => b.Id == id);
            if (baseline == null)
            {
                TempData["Error"] = "Baseline not found.";
                return RedirectToAction("Index");
            }

            _db.BaselineApprovals.Remove(baseline);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Baseline removed successfully.";
            return RedirectToAction("Index");
        }
    }
}