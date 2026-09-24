using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AccessEase.Data;
using AccessEase.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace AccessEase12.Controllers
{
    [ApiController]
    [Route("api/scan")]
    public class ApiScanController : ControllerBase
    {
        private readonly AccessEaseDbContext _db;
        private readonly IConfiguration _configuration;

        public ApiScanController(AccessEaseDbContext db, IConfiguration configuration)
        {
            _db = db;
            _configuration = configuration;
        }

        [HttpPost("trigger")]
        public IActionResult Trigger([FromBody] CiCdScanRequest request)
        {
            var expectedToken = _configuration["CiCd:TriggerToken"];

            if (string.IsNullOrWhiteSpace(expectedToken) || request.Token != expectedToken)
            {
                return Unauthorized(new { message = "Invalid CI/CD trigger token." });
            }

            request.Url = (request.Url ?? "").Trim();
            request.Mode = (request.Mode ?? "axe").ToLower();
            request.Severity = (request.Severity ?? "all").ToLower();

            if (string.IsNullOrWhiteSpace(request.Url))
            {
                return BadRequest(new { message = "URL cannot be empty." });
            }

            if (request.Mode != "axe" && request.Mode != "ui" && request.Mode != "both")
            {
                return BadRequest(new { message = "Invalid scan mode. Use axe, ui, or both." });
            }

            var allowedSeverities = new[] { "all", "critical", "serious", "moderate", "minor" };
            if (!allowedSeverities.Contains(request.Severity))
            {
                return BadRequest(new { message = "Invalid severity value." });
            }

            var project = _db.Projects.FirstOrDefault(p => p.Id == request.ProjectId);
            if (project == null)
            {
                return BadRequest(new { message = "Invalid project ID." });
            }

            var projectDeveloper = _db.ProjectMembers
                .Include(pm => pm.AppUser)
                .Where(pm => pm.ProjectId == request.ProjectId &&
                             pm.AppUser != null &&
                             pm.AppUser.Role == "Developer")
                .Select(pm => pm.AppUser!)
                .FirstOrDefault();

            if (projectDeveloper == null)
            {
                return BadRequest(new { message = "No developer assigned to this project." });
            }

            IWebDriver? driver = null;

            try
            {
                EnsureStorageFolders();

                driver = CreateLocalChromeDriver();
                driver.Navigate().GoToUrl(request.Url);
                Thread.Sleep(1200);

                List<AxeIssue> axeResults = new();
                List<AxeIssue> uiResults = new();

                if (request.Mode == "axe" || request.Mode == "both")
                {
                    axeResults = RunAxeCoreScanWithEvidence(driver, request.Severity, request.Url);
                }

                if (request.Mode == "ui" || request.Mode == "both")
                {
                    var screenshotPath = SaveFullScreenshot(driver, request.Url, "ui");

                    var uiSettings = _db.ProjectUiValidationSettings
                        .FirstOrDefault(x => x.ProjectId == request.ProjectId);

                    uiResults = RunUiValidationBasic(driver, uiSettings);

                    foreach (var issue in uiResults)
                    {
                        issue.Rule = "ui-" + issue.Rule;
                        issue.ScreenshotUrl = screenshotPath;
                    }
                }

                var allIssues = axeResults.Concat(uiResults).ToList();

                var record = new ScanRecord
                {
                    Url = request.Url,
                    ScanTime = DateTime.UtcNow,
                    IssueCount = allIssues.Count,
                    IssuesJson = JsonSerializer.Serialize(allIssues),
                    AppUserId = projectDeveloper.Id,
                    ProjectId = request.ProjectId
                };

                _db.ScanRecords.Add(record);
                _db.SaveChanges();

                var remediationIssues = allIssues.Select(issue => new RemediationIssue
                {
                    ScanRecordId = record.Id,
                    Impact = issue.Impact ?? "",
                    Rule = issue.Rule ?? "",
                    Target = issue.Target,
                    FixTip = issue.FixTip,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow
                }).ToList();

                _db.RemediationIssues.AddRange(remediationIssues);
                _db.SaveChanges();

                var baseline = _db.BaselineApprovals
                    .Include(b => b.ScanRecord)
                    .FirstOrDefault(b => b.ProjectId == request.ProjectId && b.Url == request.Url);

                object baselineComparison;

                if (baseline != null && baseline.ScanRecord != null)
                {
                    int baselineIssueCount = baseline.ScanRecord.IssueCount;
                    int currentIssueCount = record.IssueCount;
                    int difference = currentIssueCount - baselineIssueCount;

                    string status;
                    string message;

                    if (difference < 0)
                    {
                        status = "Improved";
                        message = $"Improved: current scan found {-difference} fewer issue(s) than the approved baseline.";
                    }
                    else if (difference > 0)
                    {
                        status = "Regressed";
                        message = $"Regressed: current scan found {difference} more issue(s) than the approved baseline.";
                    }
                    else
                    {
                        status = "NoChange";
                        message = "No Change: current scan has the same number of issues as the approved baseline.";
                    }

                    baselineComparison = new
                    {
                        baselineExists = true,
                        baselineScanId = baseline.ScanRecordId,
                        baselineIssueCount,
                        currentIssueCount,
                        difference,
                        status,
                        message
                    };
                }
                else
                {
                    baselineComparison = new
                    {
                        baselineExists = false,
                        message = "No approved baseline found for this project and URL yet."
                    };
                }

                return Ok(new
                {
                    message = "CI/CD scan completed successfully.",
                    scanRecordId = record.Id,
                    issueCount = record.IssueCount,
                    project = project.Name,
                    scannedBy = projectDeveloper.FullName,
                    baselineComparison
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "CI/CD scan failed.",
                    error = ex.InnerException?.Message ?? ex.Message
                });
            }
            finally
            {
                try { driver?.Quit(); } catch { }
            }
        }

        private IWebDriver CreateLocalChromeDriver()
        {
            var options = new ChromeOptions();

            if (OperatingSystem.IsLinux())
            {
                options.BinaryLocation = "/usr/bin/google-chrome";
            }

            options.AddArgument("--headless=new");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--window-size=1920,1080");
            options.AddArgument("--disable-notifications");
            options.AddArgument("--ignore-certificate-errors");

            return new ChromeDriver(options);
        }

        private void EnsureStorageFolders()
        {
            Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "Storage"));
            Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "Storage", "ui"));
            Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "Storage", "evidence"));
        }

        private string UrlKey(string url)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(url.Trim().ToLower()));
            return Convert.ToHexString(bytes).ToLower();
        }

        private byte[] CaptureScreenshotBytes(IWebDriver driver)
        {
            return ((ITakesScreenshot)driver).GetScreenshot().AsByteArray;
        }

        private string SaveFullScreenshot(IWebDriver driver, string url, string folder)
        {
            var key = UrlKey(url);
            var fileName = $"{key}_{DateTime.Now:yyyyMMdd_HHmmss}.png";

            var physical = Path.Combine(Directory.GetCurrentDirectory(), "Storage", folder, fileName);
            System.IO.File.WriteAllBytes(physical, CaptureScreenshotBytes(driver));

            return $"/storage/{folder}/{fileName}";
        }

        private string TryGetOuterHtmlSnippet(IWebDriver driver, string cssSelector)
        {
            try
            {
                var el = driver.FindElement(By.CssSelector(cssSelector));
                var html = el.GetAttribute("outerHTML") ?? "";
                html = html.Replace("\r", " ").Replace("\n", " ").Trim();
                if (html.Length > 180) html = html[..180] + "...";
                return html;
            }
            catch
            {
                return "";
            }
        }

        private string TryCaptureElementEvidence(IWebDriver driver, string cssSelector, string url)
        {
            try
            {
                var el = driver.FindElement(By.CssSelector(cssSelector));
                ((IJavaScriptExecutor)driver).ExecuteScript(
                    "arguments[0].scrollIntoView({block:'center', inline:'nearest'});", el);
                Thread.Sleep(300);

                var bytes = CaptureScreenshotBytes(driver);
                using var img = Image.Load<Rgba32>(bytes);

                var loc = el.Location;
                var size = el.Size;

                int x = Math.Max(loc.X, 0);
                int y = Math.Max(loc.Y, 0);
                int w = Math.Min(size.Width, img.Width - x);
                int h = Math.Min(size.Height, img.Height - y);

                if (w <= 0 || h <= 0) return "";

                int pad = 6;
                x = Math.Max(x - pad, 0);
                y = Math.Max(y - pad, 0);
                w = Math.Min(w + pad * 2, img.Width - x);
                h = Math.Min(h + pad * 2, img.Height - y);

                img.Mutate(c => c.Crop(new Rectangle(x, y, w, h)));

                var key = UrlKey(url);
                var shortId = Guid.NewGuid().ToString("N")[..6];
                var fileName = $"{key}_{DateTime.Now:yyyyMMdd_HHmmss}_{shortId}.png";

                var physical = Path.Combine(Directory.GetCurrentDirectory(), "Storage", "evidence", fileName);
                img.Save(physical);

                return $"/storage/evidence/{fileName}";
            }
            catch
            {
                return "";
            }
        }

        private string CssEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("\\", "\\\\").Replace("'", "\\'");
        }

        private AxeIssue NewUiIssue(string impact, string rule, string fixTip)
        {
            return new AxeIssue
            {
                Impact = impact,
                Rule = rule,
                Count = 1,
                Target = "-",
                HelpUrl = "",
                Help = "UI validation rule",
                FixTip = fixTip,
                HtmlSnippet = "",
                EvidenceUrl = "",
                ScreenshotUrl = "",
                ApproxHtmlLine = null,
                Category = GetUiCategory(rule)
            };
        }

        private List<AxeIssue> RunUiValidationBasic(IWebDriver driver, ProjectUiValidationSetting? settings = null)
        {
            settings ??= new ProjectUiValidationSetting();

            var issues = new List<AxeIssue>();
            var js = (IJavaScriptExecutor)driver;

            bool hasHeader = driver.FindElements(By.TagName("header")).Any();
            bool hasMain = driver.FindElements(By.TagName("main")).Any() ||
                           driver.FindElements(By.CssSelector("[role='main']")).Any();
            bool hasFooter = driver.FindElements(By.TagName("footer")).Any();
            bool hasNav = driver.FindElements(By.TagName("nav")).Any() ||
                          driver.FindElements(By.CssSelector("[role='navigation']")).Any();

            if (settings.RequireHeader && !hasHeader)
                issues.Add(NewUiIssue("moderate", "landmark-header-missing",
                    "Add a <header> (or role=banner) for consistent page structure."));

            if (settings.RequireMain && !hasMain)
                issues.Add(NewUiIssue("serious", "landmark-main-missing",
                    "Add <main> (or role=main). This improves structure and navigation."));

            if (settings.RequireFooter && !hasFooter)
                issues.Add(NewUiIssue("minor", "landmark-footer-missing",
                    "Add a <footer> to complete the page structure."));

            if (settings.RequireNav && !hasNav)
                issues.Add(NewUiIssue("moderate", "landmark-nav-missing",
                    "Add <nav> (or role=navigation) for primary navigation links."));

            if (settings.RequirePageTitle)
            {
                try
                {
                    var title = driver.Title?.Trim() ?? "";
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        issues.Add(NewUiIssue("serious", "page-title-missing",
                            "Add a meaningful page title so users can identify the page clearly."));
                    }
                }
                catch { }
            }

            if (settings.RequireSingleH1)
            {
                var h1Count = driver.FindElements(By.TagName("h1")).Count;
                if (h1Count == 0)
                {
                    issues.Add(NewUiIssue("moderate", "h1-missing",
                        "Add one main H1 heading to clearly identify the page content."));
                }
                else if (h1Count > 1)
                {
                    issues.Add(NewUiIssue("moderate", "multiple-h1",
                        "Use only one main H1 heading per page for clearer hierarchy."));
                }
            }

            if (settings.CheckUnlabeledInputs)
            {
                var inputs = driver.FindElements(By.CssSelector("input, select, textarea")).ToList();
                int unlabeled = 0;

                foreach (var el in inputs)
                {
                    try
                    {
                        var id = el.GetAttribute("id");
                        var ariaLabel = el.GetAttribute("aria-label");
                        var ariaLabelledBy = el.GetAttribute("aria-labelledby");

                        bool hasLabel = false;

                        if (!string.IsNullOrWhiteSpace(id))
                        {
                            hasLabel = driver.FindElements(By.CssSelector($"label[for='{CssEscape(id)}']")).Any();
                        }

                        if (!hasLabel && string.IsNullOrWhiteSpace(ariaLabel) && string.IsNullOrWhiteSpace(ariaLabelledBy))
                            unlabeled++;
                    }
                    catch { }
                }

                if (unlabeled > 0)
                {
                    issues.Add(NewUiIssue("serious", "form-unlabeled-input",
                        $"{unlabeled} form field(s) do not have a label or aria-label."));
                }
            }

            if (settings.CheckDuplicateIds)
            {
                try
                {
                    int duplicateIds = Convert.ToInt32(js.ExecuteScript(@"
                const ids = [...document.querySelectorAll('[id]')].map(x => x.id);
                const dupes = ids.filter((id, index) => ids.indexOf(id) !== index);
                return [...new Set(dupes)].length;
            "));
                    if (duplicateIds > 0)
                    {
                        issues.Add(NewUiIssue("serious", "duplicate-id",
                            $"Found {duplicateIds} duplicate id value(s). IDs must be unique."));
                    }
                }
                catch { }
            }

            if (settings.CheckHorizontalOverflow)
            {
                try
                {
                    bool overflowX = (bool)js.ExecuteScript(@"
                return document.documentElement.scrollWidth > document.documentElement.clientWidth + 5;
            ");
                    if (overflowX)
                    {
                        issues.Add(NewUiIssue("moderate", "layout-horizontal-overflow",
                            "Page has horizontal overflow. Check container widths, images, and fixed elements."));
                    }
                }
                catch { }
            }

            if (settings.CheckTinyClickTargets)
            {
                try
                {
                    int minSize = settings.MinClickTargetSize;

                    int tinyTargets = Convert.ToInt32(js.ExecuteScript(@"
                const minSize = arguments[0];
                const els = Array.from(document.querySelectorAll('button, a, input[type=button], input[type=submit]'));
                return els.filter(el => {
                    const r = el.getBoundingClientRect();
                    return r.width > 0 && r.height > 0 && (r.width < minSize || r.height < minSize);
                }).length;
            ", minSize));

                    if (tinyTargets > 0)
                    {
                        issues.Add(NewUiIssue("moderate", "tiny-click-target",
                            $"Found {tinyTargets} clickable element(s) smaller than {minSize}px."));
                    }
                }
                catch { }
            }

            // 1. Broken image detection
            try
            {
                int brokenImages = Convert.ToInt32(js.ExecuteScript(@"
            const imgs = Array.from(document.images || []);
            return imgs.filter(img => img.src && (!img.complete || img.naturalWidth === 0)).length;
        "));

                if (brokenImages > 0)
                {
                    issues.Add(NewUiIssue("serious", "broken-image",
                        $"Found {brokenImages} broken image(s) that failed to load properly."));
                }
            }
            catch { }

            // 2. Empty button detection
            try
            {
                int emptyButtons = Convert.ToInt32(js.ExecuteScript(@"
            const buttons = Array.from(document.querySelectorAll('button, input[type=button], input[type=submit], input[type=reset]'));
            return buttons.filter(el => {
                const text = (el.innerText || el.value || '').trim();
                const aria = (el.getAttribute('aria-label') || '').trim();
                const title = (el.getAttribute('title') || '').trim();
                return !text && !aria && !title;
            }).length;
        "));

                if (emptyButtons > 0)
                {
                    issues.Add(NewUiIssue("serious", "empty-button",
                        $"Found {emptyButtons} button(s) without visible text or accessible label."));
                }
            }
            catch { }

            // 2. Empty link detection
            try
            {
                int emptyLinks = Convert.ToInt32(js.ExecuteScript(@"
            const links = Array.from(document.querySelectorAll('a[href]'));
            return links.filter(el => {
                const text = (el.innerText || '').trim();
                const aria = (el.getAttribute('aria-label') || '').trim();
                const title = (el.getAttribute('title') || '').trim();
                return !text && !aria && !title;
            }).length;
        "));

                if (emptyLinks > 0)
                {
                    issues.Add(NewUiIssue("serious", "empty-link",
                        $"Found {emptyLinks} link(s) without visible text or accessible label."));
                }
            }
            catch { }

            // 3. Button size consistency
            try
            {
                int inconsistentButtons = Convert.ToInt32(js.ExecuteScript(@"
            const buttons = Array.from(document.querySelectorAll('button, a.btn, input[type=button], input[type=submit]'))
                .filter(el => {
                    const r = el.getBoundingClientRect();
                    return r.width > 0 && r.height > 0;
                });

            if (buttons.length < 2) return 0;

            const heights = buttons.map(el => Math.round(el.getBoundingClientRect().height));
            const minH = Math.min(...heights);
            const maxH = Math.max(...heights);

            return (maxH - minH) > 12 ? 1 : 0;
        "));

                if (inconsistentButtons > 0)
                {
                    issues.Add(NewUiIssue("moderate", "button-inconsistent-size",
                        "Buttons appear to have inconsistent heights. Use a more consistent button size/style across the page."));
                }
            }
            catch { }

            // 4. Input size consistency
            try
            {
                int inconsistentInputs = Convert.ToInt32(js.ExecuteScript(@"
            const inputs = Array.from(document.querySelectorAll('input[type=text], input[type=email], input[type=password], input[type=url], input[type=search], input[type=tel], input[type=number], select, textarea'))
                .filter(el => {
                    const r = el.getBoundingClientRect();
                    return r.width > 0 && r.height > 0;
                });

            if (inputs.length < 2) return 0;

            const heights = inputs.map(el => Math.round(el.getBoundingClientRect().height));
            const minH = Math.min(...heights);
            const maxH = Math.max(...heights);

            return (maxH - minH) > 12 ? 1 : 0;
        "));

                if (inconsistentInputs > 0)
                {
                    issues.Add(NewUiIssue("moderate", "input-inconsistent-size",
                        "Form inputs appear to have inconsistent heights. Use a more consistent input size/style across the page."));
                }
            }
            catch { }

            if (!issues.Any())
            {
                issues.Add(NewUiIssue("minor", "ui-ok",
                    "No UI validation issues detected by the current configured rule set."));
            }

            return issues;
        }

        private List<AxeIssue> RunAxeCoreScanWithEvidence(IWebDriver driver, string severity, string url)
        {
            var axePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "js", "axe.min.js");
            if (!System.IO.File.Exists(axePath))
                throw new Exception("axe.min.js not found. Put it in wwwroot/js/axe.min.js");

            var axeJs = System.IO.File.ReadAllText(axePath);
            var js = (IJavaScriptExecutor)driver;

            js.ExecuteScript(axeJs);

            var json = (string)js.ExecuteAsyncScript(@"
                const done = arguments[arguments.length - 1];
                axe.run().then(res => done(JSON.stringify(res)))
                        .catch(err => done(JSON.stringify({ error: err.toString() })));
            ");

            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("error", out var errEl))
                throw new Exception("axe.run() error: " + (errEl.GetString() ?? "unknown"));

            var results = new List<AxeIssue>();
            var violations = doc.RootElement.GetProperty("violations");

            foreach (var v in violations.EnumerateArray())
            {
                var ruleId = v.GetProperty("id").GetString() ?? "unknown";
                var impact = (v.TryGetProperty("impact", out var impEl) ? (impEl.GetString() ?? "unknown") : "unknown").ToLower();

                if (!PassSeverityFilter(impact, severity))
                    continue;

                var helpUrl = v.TryGetProperty("helpUrl", out var helpEl) ? (helpEl.GetString() ?? "") : "";
                var helpText = v.TryGetProperty("help", out var helpTextEl) ? (helpTextEl.GetString() ?? "") : "";

                var nodes = v.GetProperty("nodes");
                int count = nodes.GetArrayLength();

                string target = "";
                try
                {
                    var firstNode = nodes[0];
                    if (firstNode.TryGetProperty("target", out var targets)
                        && targets.ValueKind == JsonValueKind.Array
                        && targets.GetArrayLength() > 0)
                    {
                        target = targets[0].GetString() ?? "";
                    }
                }
                catch { }

                string evidenceUrl = "";
                string htmlSnippet = "";
                int? approxHtmlLine = null;

                if (!string.IsNullOrWhiteSpace(target))
                {
                    evidenceUrl = TryCaptureElementEvidence(driver, target, url);
                    htmlSnippet = TryGetOuterHtmlSnippet(driver, target);
                    approxHtmlLine = TryFindApproxHtmlLine(driver, target, htmlSnippet);
                }

                results.Add(new AxeIssue
                {
                    Impact = impact,
                    Rule = ruleId,
                    Count = count,
                    HelpUrl = helpUrl,
                    Help = helpText,
                    Target = string.IsNullOrWhiteSpace(target) ? "-" : target,
                    FixTip = GetFixTip(ruleId),
                    EvidenceUrl = evidenceUrl,
                    HtmlSnippet = htmlSnippet,
                    ScreenshotUrl = "",
                    ApproxHtmlLine = approxHtmlLine
                });
            }

            return results;
        }

        private bool PassSeverityFilter(string impact, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter) || filter == "all") return true;
            return impact == filter;
        }

        private string GetFixTip(string ruleId)
        {
            ruleId = (ruleId ?? "").Trim().ToLower();

            return ruleId switch
            {
                "color-contrast" => "Increase contrast between text and background.",
                "image-alt" => "Add meaningful alt text. If decorative, use alt=\"\".",
                "label" => "Add <label for> or aria-label / aria-labelledby for inputs.",
                "link-name" => "Make links have readable text or aria-label.",
                "button-name" => "Buttons need an accessible name.",
                "heading-order" => "Use headings in logical order.",
                "landmark-one-main" => "Ensure exactly one main landmark exists.",
                "region" => "Wrap sections with landmarks or add aria-labelledby.",
                _ => "Open the WCAG link and apply the recommended fix for this rule."
            };
        }

        private int? TryFindApproxHtmlLine(IWebDriver driver, string targetSelector, string htmlSnippet)
        {
            try
            {
                var pageSource = driver.PageSource;
                if (string.IsNullOrWhiteSpace(pageSource))
                    return null;

                string searchText = "";

                if (!string.IsNullOrWhiteSpace(htmlSnippet))
                {
                    searchText = htmlSnippet.Length > 120 ? htmlSnippet.Substring(0, 120) : htmlSnippet;
                }

                if (string.IsNullOrWhiteSpace(searchText) && !string.IsNullOrWhiteSpace(targetSelector))
                {
                    try
                    {
                        var el = driver.FindElement(By.CssSelector(targetSelector));
                        var outer = el.GetAttribute("outerHTML") ?? "";
                        searchText = outer.Length > 120 ? outer.Substring(0, 120) : outer;
                    }
                    catch { }
                }

                if (string.IsNullOrWhiteSpace(searchText))
                    return null;

                var index = pageSource.IndexOf(searchText, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                    return null;

                int line = 1;
                for (int i = 0; i < index; i++)
                {
                    if (pageSource[i] == '\n')
                        line++;
                }

                return line;
            }
            catch
            {
                return null;
            }
        }
        private string GetUiCategory(string rule)
        {
            rule = (rule ?? "").Trim().ToLower();

            return rule switch
            {
                "landmark-header-missing" => "Layout",
                "landmark-main-missing" => "Layout",
                "landmark-footer-missing" => "Layout",
                "landmark-nav-missing" => "Layout",
                "h1-missing" => "Content",
                "multiple-h1" => "Content",
                "page-title-missing" => "Content",
                "form-unlabeled-input" => "Forms",
                "duplicate-id" => "Forms",
                "broken-image" => "Content",
                "empty-button" => "Interaction",
                "empty-link" => "Interaction",
                "button-inconsistent-size" => "Visual Consistency",
                "input-inconsistent-size" => "Visual Consistency",
                "layout-horizontal-overflow" => "Layout",
                "tiny-click-target" => "Interaction",
                "ui-ok" => "General",
                _ => "General"
            };
        }
    }
}