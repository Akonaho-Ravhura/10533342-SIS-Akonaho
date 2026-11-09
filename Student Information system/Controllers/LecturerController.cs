using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Student_Information_system.Models;
using Student_Information_system.Services;

namespace Student_Information_system.Controllers
{
    [Authorize(Roles = Roles.Lecturer)]
    public class LecturerController : SisControllerBase
    {
        public LecturerController(DataStore store) : base(store) { }

        private List<Course> MyCourses => Store.Data.Courses.Where(c => c.LecturerId == CurrentUserId).OrderBy(c => c.Code).ToList();
        private Course? MyCourse(string? code) => MyCourses.FirstOrDefault(c => c.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

        public IActionResult Index()
        {
            var courses = MyCourses;
            var codes = courses.Select(c => c.Code).ToHashSet();
            ViewBag.Enrollments = Store.Data.Enrollments.Where(e => codes.Contains(e.CourseCode) && e.Status == EnrollmentStatus.Approved).ToList();
            ViewBag.Messages = Store.MessagesFor(Me!).Where(m => m.SenderId != CurrentUserId).Take(4).ToList();
            return View(courses);
        }

        // ---------- gradebook ----------
        public IActionResult Marks(string? id)
        {
            var courses = MyCourses;
            var course = MyCourse(id) ?? courses.FirstOrDefault();
            ViewBag.Courses = courses;
            ViewBag.Current = course;
            var list = course == null ? new List<Enrollment>() :
                Store.Data.Enrollments.Where(e => e.CourseCode == course.Code && e.Status == EnrollmentStatus.Approved)
                    .OrderBy(e => Store.Student(e.StudentId)?.LastName).ToList();
            return View(list);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Marks(string id, IFormCollection form)
        {
            var course = MyCourse(id);
            if (course == null) return Forbid();
            var errors = 0;
            Store.Write(d =>
            {
                foreach (var e in d.Enrollments.Where(e => e.CourseCode == course.Code && e.Status == EnrollmentStatus.Approved))
                {
                    var changed = false;
                    for (var i = 0; i < e.Assessments.Count; i++)
                    {
                        var raw = form[$"m_{e.Id}_{i}"].ToString().Trim().Replace(',', '.');
                        decimal? val = null;
                        if (raw.Length > 0)
                        {
                            if (!decimal.TryParse(raw, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var v) || v < 0 || v > 100) { errors++; continue; }
                            val = Math.Round(v, 1);
                        }
                        if (e.Assessments[i].Mark != val) { e.Assessments[i].Mark = val; changed = true; }
                    }
                    var comment = form[$"c_{e.Id}"].ToString().Trim();
                    if ((e.LecturerComment ?? "") != comment) { e.LecturerComment = comment.Length == 0 ? null : comment; changed = true; }
                    if (changed) e.MarksUpdatedAt = DateTime.Now;
                }
            });
            if (form["notify"] == "on")
                Store.Write(d => d.Messages.Add(new Message
                {
                    SenderId = CurrentUserId!, Audience = Audiences.CoursePrefix + course.Code, Priority = "Normal",
                    Subject = $"{course.Code} marks updated", Body = $"New marks for {course.Code} – {course.Name} have been captured. Log in to the student portal and open Results to view them."
                }));
            Flash(errors == 0 ? $"Marks for {course.Code} saved." : $"Marks saved, but {errors} value(s) were ignored because they were not between 0 and 100.", errors == 0 ? "success" : "warning");
            return RedirectToAction(nameof(Marks), new { id = course.Code });
        }

        // ---------- course content ----------
        public IActionResult Materials(string? id)
        {
            var courses = MyCourses;
            var course = MyCourse(id) ?? courses.FirstOrDefault();
            ViewBag.Courses = courses;
            ViewBag.Current = course;
            var list = course == null ? new List<CourseMaterial>() : Store.Data.Materials.Where(m => m.CourseCode == course.Code).OrderBy(m => m.Week).ThenBy(m => m.UploadedAt).ToList();
            return View(list);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMaterial(string id, int week, string title, string kind, string? description, string? url, DateTime? dueDate, IFormFile? file)
        {
            var course = MyCourse(id);
            if (course == null) return Forbid();
            if (string.IsNullOrWhiteSpace(title) || (file == null && string.IsNullOrWhiteSpace(url)))
            {
                Flash("Add a title and either upload a file or provide a link.", "danger");
                return RedirectToAction(nameof(Materials), new { id });
            }
            if (!string.IsNullOrWhiteSpace(url) && !(Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == "http" || u.Scheme == "https")))
            {
                Flash("Links must start with http:// or https://", "danger");
                return RedirectToAction(nameof(Materials), new { id });
            }
            var m = new CourseMaterial
            {
                CourseCode = course.Code, Week = Math.Clamp(week, 1, 20), Title = title.Trim(), Kind = kind, Description = description?.Trim() ?? "",
                Url = string.IsNullOrWhiteSpace(url) ? null : url.Trim(), DueDate = dueDate, UploadedBy = CurrentUserId!
            };
            if (file != null)
            {
                var (stored, err) = await Store.SaveUpload(file, "courses/" + course.Code);
                if (err != null) { Flash(err, "danger"); return RedirectToAction(nameof(Materials), new { id }); }
                m.StoredName = stored; m.FileName = Path.GetFileName(file.FileName); m.ContentType = file.ContentType;
            }
            Store.Write(d => d.Materials.Add(m));
            Flash($"\"{m.Title}\" published to {course.Code}.");
            return RedirectToAction(nameof(Materials), new { id = course.Code });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult DeleteMaterial(string materialId)
        {
            var m = Store.Data.Materials.FirstOrDefault(x => x.Id == materialId);
            if (m == null || MyCourse(m.CourseCode) == null) return Forbid();
            Store.Write(d => d.Materials.Remove(m));
            if (m.StoredName != null) System.IO.File.Delete(Store.PhysicalPath(m.StoredName));
            Flash($"\"{m.Title}\" removed.");
            return RedirectToAction(nameof(Materials), new { id = m.CourseCode });
        }
    }
}
