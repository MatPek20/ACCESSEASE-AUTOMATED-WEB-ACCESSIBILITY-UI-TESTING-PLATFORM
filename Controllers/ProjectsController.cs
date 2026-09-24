using AccessEase.Data;
using AccessEase.Models;
using AccessEase12.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccessEase12.Controllers
{
    [Authorize(Roles = "ProjectManager")]
    public class ProjectsController : Controller
    {
        private readonly AccessEaseDbContext _db;

        public ProjectsController(AccessEaseDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var projects = await _db.Projects
                .Include(p => p.ProjectMembers)
                .ThenInclude(pm => pm.AppUser)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View(projects);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new ProjectFormViewModel());

        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProjectFormViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var exists = await _db.Projects.AnyAsync(p => p.Name == model.Name);
            if (exists)
            {
                ModelState.AddModelError("Name", "Project name already exists.");
                return View(model);
            }

            var project = new Project
            {
                Name = model.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
                DefaultUrl = string.IsNullOrWhiteSpace(model.DefaultUrl) ? null : model.DefaultUrl.Trim()
            };

            _db.Projects.Add(project);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Project created successfully.";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id);
            if (project == null) return NotFound();

            var vm = new ProjectFormViewModel
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                DefaultUrl = project.DefaultUrl
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ProjectFormViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == model.Id);
            if (project == null) return NotFound();

            var duplicate = await _db.Projects.AnyAsync(p => p.Name == model.Name && p.Id != model.Id);
            if (duplicate)
            {
                ModelState.AddModelError("Name", "Project name already exists.");
                return View(model);
            }

            project.Name = model.Name.Trim();
            project.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
            project.DefaultUrl = string.IsNullOrWhiteSpace(model.DefaultUrl) ? null : model.DefaultUrl.Trim();

            await _db.SaveChangesAsync();

            TempData["Success"] = "Project updated successfully.";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> ManageMembers(int id)
        {
            var project = await _db.Projects
                .Include(p => p.ProjectMembers)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null) return NotFound();

            var developers = await _db.AppUsers
                .Where(u => u.Role == "Developer")
                .OrderBy(u => u.FullName)
                .ToListAsync();

            var vm = new ProjectManageMembersViewModel
            {
                ProjectId = project.Id,
                ProjectName = project.Name,
                AllDevelopers = developers,
                SelectedUserIds = project.ProjectMembers.Select(pm => pm.AppUserId).ToList()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManageMembers(ProjectManageMembersViewModel model)
        {
            var project = await _db.Projects
                .Include(p => p.ProjectMembers)
                .FirstOrDefaultAsync(p => p.Id == model.ProjectId);

            if (project == null) return NotFound();

            var existingMembers = project.ProjectMembers.ToList();
            _db.ProjectMembers.RemoveRange(existingMembers);

            if (model.SelectedUserIds != null && model.SelectedUserIds.Any())
            {
                var newMembers = model.SelectedUserIds.Distinct().Select(userId => new ProjectMember
                {
                    ProjectId = model.ProjectId,
                    AppUserId = userId
                });

                await _db.ProjectMembers.AddRangeAsync(newMembers);
            }

            await _db.SaveChangesAsync();

            TempData["Success"] = "Project members updated successfully.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var project = await _db.Projects
                .Include(p => p.ProjectMembers)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
            {
                TempData["Error"] = "Project not found.";
                return RedirectToAction("Index");
            }

            bool hasRelatedScans = await _db.ScanRecords.AnyAsync(s => s.ProjectId == id);
            bool hasRelatedBaselines = await _db.BaselineApprovals.AnyAsync(b => b.ProjectId == id);

            if (hasRelatedScans || hasRelatedBaselines)
            {
                TempData["Error"] = "This project cannot be deleted because it is already linked to scan or baseline records.";
                return RedirectToAction("Index");
            }

            if (project.ProjectMembers.Any())
            {
                _db.ProjectMembers.RemoveRange(project.ProjectMembers);
            }

            _db.Projects.Remove(project);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Project deleted successfully.";
            return RedirectToAction("Index");
        }
    }

}