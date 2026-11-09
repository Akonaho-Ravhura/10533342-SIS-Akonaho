using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Student_Information_system.Models;
using Student_Information_system.Services;

namespace Student_Information_system.Controllers
{
    [Authorize(Roles = Roles.Student)]
    public class StudentController : SisControllerBase
    {
        public StudentController(DataStore store) : base(store) { }

        private StudentProfile Profile => Store.Student(CurrentUserId)!;
        private List<Enrollment> MyEnrollments => Store.Data.Enrollments.Where(e => e.StudentId == CurrentUserId).ToList();

        public IActionResult Index()
        {
            ViewBag.Profile = Profile;
            ViewBag.Enrollments = MyEnrollments;
            ViewBag.Messages = Store.MessagesFor(Me!).Where(m => m.SenderId != CurrentUserId).Take(4).ToList();
            ViewBag.Documents = Store.Data.Documents.Where(d => d.StudentId == CurrentUserId).ToList();
            var approved = MyEnrollments.Where(e => e.Status == EnrollmentStatus.Approved).Select(e => e.CourseCode).ToHashSet();
            ViewBag.Due = Store.Data.Materials.Where(m => approved.Contains(m.CourseCode) && m.DueDate >= DateTime.Today).OrderBy(m => m.DueDate).Take(5).ToList();
            return View();
        }

        // ---------- course registration ----------
        public IActionResult Courses()
        {
            ViewBag.Profile = Profile;
            ViewBag.Enrollments = MyEnrollments;
            return View(Store.Data.Courses.OrderBy(c => c.Programme != Profile.Programme).ThenBy(c => c.Code).ToList());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Register(string[] codes)
        {
            var p = Profile;
            if (p.Status != StudentStatus.Active)
            {
                Flash("Course registration opens once the Registrar has approved your application.", "warning");
                return RedirectToAction(nameof(Courses));
            }
            var added = Store.Write(d =>
            {
                var n = new List<string>();
                foreach (var code in codes.Distinct())
                {
                    var c = Store.Course(code);
                    if (c == null || !c.OpenForRegistration) continue;
                    if (d.Enrollments.Any(e => e.StudentId == p.UserId && e.CourseCode == c.Code && e.Status is EnrollmentStatus.Pending or EnrollmentStatus.Approved)) continue;
                    var taken = d.Enrollments.Count(e => e.CourseCode == c.Code && e.Status == EnrollmentStatus.Approved);
                    if (taken >= c.Capacity) continue;
                    d.Enrollments.Add(new Enrollment { StudentId = p.UserId, CourseCode = c.Code, Assessments = Enrollment.DefaultAssessments() });
                    n.Add(c.Code);
                }
                return n;
            });
            Flash(added.Count > 0
                ? $"Registration submitted for {string.Join(", ", added)}. The Registrar's Office will confirm your registration."
                : "No courses were added. Select at least one open course you are not already registered for.", added.Count > 0 ? "success" : "warning");
            return RedirectToAction(nameof(Courses));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Drop(string id)
        {
            var e = MyEnrollments.FirstOrDefault(x => x.Id == id && x.Status is EnrollmentStatus.Pending or EnrollmentStatus.Approved);
            if (e == null) return NotFound();
            if (e.Assessments.Any(a => a.Mark.HasValue))
            {
                Flash("You cannot drop a course once marks have been captured. Please contact the Registrar.", "warning");
                return RedirectToAction(nameof(Courses));
            }
            Store.Write(_ => e.Status = EnrollmentStatus.Dropped);
            Flash($"{e.CourseCode} has been dropped.");
            return RedirectToAction(nameof(Courses));
        }

        // ---------- results ----------
        public IActionResult Results()
        {
            ViewBag.Profile = Profile;
            return View(MyEnrollments.Where(e => e.Status == EnrollmentStatus.Approved).ToList());
        }

        public IActionResult Transcript()
        {
            ViewBag.Profile = Profile;
            return View("~/Views/Shared/Transcript.cshtml", MyEnrollments.Where(e => e.Status == EnrollmentStatus.Approved).ToList());
        }

        // ---------- course content ----------
        public IActionResult Materials(string? id)
        {
            var codes = MyEnrollments.Where(e => e.Status == EnrollmentStatus.Approved).Select(e => e.CourseCode).ToList();
            ViewBag.Courses = codes.Select(c => Store.Course(c)!).Where(c => c != null).ToList();
            var current = id != null && codes.Contains(id, StringComparer.OrdinalIgnoreCase) ? Store.Course(id) : (codes.Count > 0 ? Store.Course(codes[0]) : null);
            ViewBag.Current = current;
            var mats = current == null ? new List<CourseMaterial>() : Store.Data.Materials.Where(m => m.CourseCode == current.Code).OrderBy(m => m.Week).ThenBy(m => m.UploadedAt).ToList();
            return View(mats);
        }

        // ---------- documents ----------
        public IActionResult Documents()
        {
            ViewBag.Profile = Profile;
            return View(Store.Data.Documents.Where(d => d.StudentId == CurrentUserId).OrderByDescending(d => d.UploadedAt).ToList());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Documents(string type, IFormFile? file)
        {
            if (file == null || !DocumentTypes.All.Contains(type))
            {
                Flash("Choose a document type and a file to upload.", "danger");
                return RedirectToAction(nameof(Documents));
            }
            var (stored, err) = await Store.SaveUpload(file, "students/" + CurrentUserId, new[] { ".pdf", ".jpg", ".jpeg", ".png" });
            if (err != null)
            {
                Flash(err + " Use PDF, JPG or PNG.", "danger");
                return RedirectToAction(nameof(Documents));
            }
            Store.Write(d => d.Documents.Add(new StudentDocument
            {
                StudentId = CurrentUserId!, Type = type, FileName = Path.GetFileName(file.FileName), StoredName = stored, ContentType = file.ContentType, Size = file.Length
            }));
            Flash($"{type} uploaded successfully.");
            return RedirectToAction(nameof(Documents));
        }

        // ---------- profile ----------

        public IActionResult MyProfile() => View("Profile", Profile);

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult MyProfile(string phone, string email, string address, string nextOfKinName, string nextOfKinPhone)
        {
            var p = Profile;
            if (string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(address))
            {
                Flash("Phone, email and address are required.", "danger");
                return RedirectToAction(nameof(MyProfile));
            }
            Store.Write(d =>
            {
                p.Phone = phone.Trim(); p.Email = email.Trim(); p.Address = address.Trim();
                p.NextOfKinName = nextOfKinName?.Trim() ?? ""; p.NextOfKinPhone = nextOfKinPhone?.Trim() ?? "";
                var u = Store.User(p.UserId)!; u.Email = p.Email; u.Phone = p.Phone;
            });
            Flash("Your contact details have been updated.");
            return RedirectToAction(nameof(MyProfile));
        }
    }
}
