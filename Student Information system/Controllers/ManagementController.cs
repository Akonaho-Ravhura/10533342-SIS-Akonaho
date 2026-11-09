using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Student_Information_system.Models;
using Student_Information_system.Services;

namespace Student_Information_system.Controllers
{
    [Authorize(Roles = Roles.Management)]
    public class ManagementController : SisControllerBase
    {
        public ManagementController(DataStore store) : base(store) { }

        public IActionResult Index()
        {
            var d = Store.Data;
            ViewBag.Applicants = d.Students.Where(s => s.Status == StudentStatus.Applicant).OrderBy(s => s.RegisteredAt).ToList();
            ViewBag.PendingEnrollments = d.Enrollments.Where(e => e.Status == EnrollmentStatus.Pending).OrderBy(e => e.RequestedAt).ToList();
            ViewBag.UnverifiedDocs = d.Documents.Count(x => !x.Verified);
            ViewBag.RecentMessages = d.Messages.OrderByDescending(m => m.SentAt).Take(5).ToList();
            return View();
        }

        // ---------- student records ----------
        public IActionResult Students(string? q, string? status, string? programme)
        {
            IEnumerable<StudentProfile> list = Store.Data.Students;
            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                list = list.Where(s => s.FullName.Contains(q, StringComparison.OrdinalIgnoreCase) || s.StudentNumber.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || s.IdNumber.Contains(q) || s.Email.Contains(q, StringComparison.OrdinalIgnoreCase));
            }
            if (Enum.TryParse<StudentStatus>(status, out var st)) list = list.Where(s => s.Status == st);
            if (!string.IsNullOrWhiteSpace(programme)) list = list.Where(s => s.Programme == programme);
            ViewBag.Q = q; ViewBag.Status = status; ViewBag.Programme = programme;
            return View(list.OrderBy(s => s.LastName).ToList());
        }

        public IActionResult Student(string id)
        {
            var s = Store.Student(id);
            if (s == null) return NotFound();
            ViewBag.Enrollments = Store.Data.Enrollments.Where(e => e.StudentId == id).OrderBy(e => e.CourseCode).ToList();
            ViewBag.Documents = Store.Data.Documents.Where(x => x.StudentId == id).OrderByDescending(x => x.UploadedAt).ToList();
            ViewBag.Account = Store.User(id);
            return View(s);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult SetStatus(string id, StudentStatus status)
        {
            var s = Store.Student(id);
            if (s == null) return NotFound();
            var old = s.Status;
            Store.Write(d =>
            {
                s.Status = status;
                Store.User(id)!.Active = status != StudentStatus.Suspended && status != StudentStatus.Rejected;
                if (old == StudentStatus.Applicant && status == StudentStatus.Active)
                    d.Messages.Add(new Message
                    {
                        SenderId = CurrentUserId!, Audience = Audiences.UserPrefix + id, Priority = "Important", Subject = "Application approved – welcome!",
                        Body = $"Congratulations {s.FirstName}! Your application to Horizon Private Training College has been approved. Your student number is {s.StudentNumber}. You can now register for courses under Course Registration."
                    });
            });
            Flash($"{s.FullName} is now {status}.");
            return RedirectToAction(nameof(Student), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult VerifyDocument(string docId, bool verified)
        {
            var doc = Store.Data.Documents.FirstOrDefault(x => x.Id == docId);
            if (doc == null) return NotFound();
            Store.Write(_ => { doc.Verified = verified; doc.VerifiedBy = verified ? Me!.FullName : null; });
            Flash(verified ? $"{doc.Type} marked as verified." : $"{doc.Type} verification removed.");
            return RedirectToAction(nameof(Student), new { id = doc.StudentId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadDocument(string id, string type, IFormFile? file)
        {
            if (Store.Student(id) == null) return NotFound();
            if (file == null || !DocumentTypes.All.Contains(type)) { Flash("Choose a type and file.", "danger"); return RedirectToAction(nameof(Student), new { id }); }
            var (stored, err) = await Store.SaveUpload(file, "students/" + id, new[] { ".pdf", ".jpg", ".jpeg", ".png" });
            if (err != null) { Flash(err, "danger"); return RedirectToAction(nameof(Student), new { id }); }
            Store.Write(d => d.Documents.Add(new StudentDocument
            {
                StudentId = id, Type = type, FileName = Path.GetFileName(file.FileName), StoredName = stored, ContentType = file.ContentType, Size = file.Length,
                Verified = true, VerifiedBy = Me!.FullName
            }));
            Flash($"{type} added to the student record.");
            return RedirectToAction(nameof(Student), new { id });
        }

        public IActionResult Transcript(string id)
        {
            var s = Store.Student(id);
            if (s == null) return NotFound();
            ViewBag.Profile = s;
            return View("~/Views/Shared/Transcript.cshtml", Store.Data.Enrollments.Where(e => e.StudentId == id && e.Status == EnrollmentStatus.Approved).ToList());
        }

        public IActionResult Documents(string? type, string? q)
        {
            IEnumerable<StudentDocument> docs = Store.Data.Documents;
            if (!string.IsNullOrEmpty(type)) docs = docs.Where(x => x.Type == type);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var ids = Store.Data.Students.Where(s => s.FullName.Contains(q, StringComparison.OrdinalIgnoreCase) || s.StudentNumber.Contains(q, StringComparison.OrdinalIgnoreCase) || s.IdNumber.Contains(q)).Select(s => s.UserId).ToHashSet();
                docs = docs.Where(x => ids.Contains(x.StudentId));
            }
            ViewBag.Type = type; ViewBag.Q = q;
            return View(docs.OrderByDescending(x => x.UploadedAt).ToList());
        }

        // ---------- registrations ----------
        public IActionResult Registrations(string? status)
        {
            var st = Enum.TryParse<EnrollmentStatus>(status, out var s) ? s : EnrollmentStatus.Pending;
            ViewBag.Status = st;
            return View(Store.Data.Enrollments.Where(e => e.Status == st).OrderByDescending(e => e.RequestedAt).ToList());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult DecideRegistration(string[] ids, bool approve, string? returnTo)
        {
            var n = Store.Write(d =>
            {
                var count = 0;
                foreach (var e in d.Enrollments.Where(e => ids.Contains(e.Id) && e.Status == EnrollmentStatus.Pending))
                {
                    e.Status = approve ? EnrollmentStatus.Approved : EnrollmentStatus.Rejected;
                    if (e.Assessments.Count == 0) e.Assessments = Enrollment.DefaultAssessments();
                    count++;
                }
                return count;
            });
            Flash(n == 0 ? "Nothing selected." : $"{n} registration(s) {(approve ? "approved" : "rejected")}.", n == 0 ? "warning" : "success");
            return returnTo != null && Url.IsLocalUrl(returnTo) ? Redirect(returnTo) : RedirectToAction(nameof(Registrations));
        }

        // ---------- courses ----------
        public IActionResult Courses()
        {
            ViewBag.Lecturers = Store.Data.Users.Where(u => u.Role == Roles.Lecturer && u.Active).OrderBy(u => u.FullName).ToList();
            return View(Store.Data.Courses.OrderBy(c => c.Programme).ThenBy(c => c.Code).ToList());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult SaveCourse(Course input, string? originalCode)
        {
            if (string.IsNullOrWhiteSpace(input.Code) || string.IsNullOrWhiteSpace(input.Name))
            {
                Flash("Course code and name are required.", "danger");
                return RedirectToAction(nameof(Courses));
            }
            input.Code = input.Code.Trim().ToUpperInvariant();
            var existing = Store.Course(originalCode ?? input.Code);
            if (originalCode == null && existing != null)
            {
                Flash($"A course with code {input.Code} already exists.", "danger");
                return RedirectToAction(nameof(Courses));
            }
            Store.Write(d =>
            {
                if (existing == null) d.Courses.Add(input);
                else
                {
                    existing.Name = input.Name; existing.Description = input.Description ?? ""; existing.Programme = input.Programme ?? "";
                    existing.Credits = input.Credits; existing.Capacity = input.Capacity; existing.Semester = input.Semester ?? "";
                    existing.LecturerId = string.IsNullOrEmpty(input.LecturerId) ? null : input.LecturerId; existing.Fee = input.Fee;
                    existing.OpenForRegistration = input.OpenForRegistration;
                }
            });
            Flash($"Course {input.Code} saved.");
            return RedirectToAction(nameof(Courses));
        }

        // ---------- staff ----------
        public IActionResult Staff() => View(Store.Data.Users.Where(u => u.Role != Roles.Student).OrderBy(u => u.Role).ThenBy(u => u.FullName).ToList());

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult AddStaff(string fullName, string username, string email, string role, string? department, string password)
        {
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(username) || (password ?? "").Length < 8 || (role != Roles.Lecturer && role != Roles.Management))
            {
                Flash("Name, username, role and a password of at least 8 characters are required.", "danger");
                return RedirectToAction(nameof(Staff));
            }
            if (Store.Data.Users.Any(u => u.Username.Equals(username.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                Flash("That username is already taken.", "danger");
                return RedirectToAction(nameof(Staff));
            }
            Store.Write(d => d.Users.Add(new AppUser
            {
                FullName = fullName.Trim(), Username = username.Trim(), Email = email?.Trim() ?? "", Role = role, Department = department?.Trim(), PasswordHash = DataStore.HashPassword(password!)
            }));
            Flash($"{role} account created for {fullName}.");
            return RedirectToAction(nameof(Staff));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult ToggleStaff(string id)
        {
            var u = Store.User(id);
            if (u == null || u.Role == Roles.Student) return NotFound();
            if (u.Id == CurrentUserId) { Flash("You cannot deactivate your own account.", "warning"); return RedirectToAction(nameof(Staff)); }
            Store.Write(_ => u.Active = !u.Active);
            Flash($"{u.FullName} is now {(u.Active ? "active" : "deactivated")}.");
            return RedirectToAction(nameof(Staff));
        }

        // ---------- reports ----------
        public IActionResult Reports()
        {
            return View(Store.Data.Courses.OrderBy(c => c.Code).ToList());
        }
    }
}
