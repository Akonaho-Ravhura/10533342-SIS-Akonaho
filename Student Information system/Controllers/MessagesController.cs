using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Student_Information_system.Models;
using Student_Information_system.Services;

namespace Student_Information_system.Controllers
{
    [Authorize]
    public class MessagesController : SisControllerBase
    {
        public MessagesController(DataStore store) : base(store) { }

        public IActionResult Index(string? box)
        {
            var me = Me!;
            var all = Store.MessagesFor(me).ToList();
            ViewBag.Box = box == "sent" ? "sent" : "inbox";
            var list = box == "sent" ? all.Where(m => m.SenderId == me.Id) : all.Where(m => m.SenderId != me.Id);
            return View(list.ToList());
        }

        public IActionResult Read(string id)
        {
            var me = Me!;
            var msg = Store.MessagesFor(me).FirstOrDefault(m => m.Id == id);
            if (msg == null) return NotFound();
            if (!msg.ReadBy.Contains(me.Id)) Store.Write(_ => msg.ReadBy.Add(me.Id));
            return View(msg);
        }

        [Authorize(Roles = Roles.Lecturer + "," + Roles.Management), HttpGet]
        public IActionResult Compose(string? to)
        {
            ViewBag.Audiences = AudienceOptions(Me!);
            ViewBag.To = to;
            return View();
        }

        [Authorize(Roles = Roles.Lecturer + "," + Roles.Management), HttpPost, ValidateAntiForgeryToken]
        public IActionResult Compose(string audience, string subject, string body, string priority)
        {
            var me = Me!;
            var options = AudienceOptions(me);
            if (!options.Any(o => o.value == audience)) ModelState.AddModelError("", "Please choose who should receive this message.");
            if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(body)) ModelState.AddModelError("", "Subject and message are required.");
            if (!ModelState.IsValid)
            {
                ViewBag.Audiences = options;
                ViewBag.To = audience;
                return View();
            }
            Store.Write(d => d.Messages.Add(new Message
            {
                SenderId = me.Id, Audience = audience, Subject = subject.Trim(), Body = body.Trim(),
                Priority = priority is "Important" or "Urgent" ? priority : "Normal"
            }));
            Flash($"Message sent to {Store.AudienceLabel(audience)}.");
            return RedirectToAction(nameof(Index), new { box = "sent" });
        }

        private List<(string value, string label)> AudienceOptions(AppUser me)
        {
            var list = new List<(string, string)>();
            IEnumerable<Course> courses;
            if (me.Role == Roles.Management)
            {
                list.Add((Audiences.Everyone, "Everyone (students & staff)"));
                list.Add((Audiences.Students, "All students"));
                list.Add((Audiences.Lecturers, "All lecturers"));
                courses = Store.Data.Courses;
            }
            else courses = Store.Data.Courses.Where(c => c.LecturerId == me.Id);

            foreach (var c in courses.OrderBy(c => c.Code))
                list.Add((Audiences.CoursePrefix + c.Code, $"Course: {c.Code} – {c.Name}"));

            var codes = courses.Select(c => c.Code).ToHashSet();
            var students = me.Role == Roles.Management
                ? Store.Data.Students
                : Store.Data.Students.Where(s => Store.Data.Enrollments.Any(e => e.StudentId == s.UserId && codes.Contains(e.CourseCode) && e.Status == EnrollmentStatus.Approved));
            foreach (var s in students.OrderBy(s => s.LastName))
                list.Add((Audiences.UserPrefix + s.UserId, $"Student: {s.FullName} ({s.StudentNumber})"));
            if (me.Role == Roles.Management)
                foreach (var u in Store.Data.Users.Where(u => u.Role == Roles.Lecturer).OrderBy(u => u.FullName))
                    list.Add((Audiences.UserPrefix + u.Id, $"Lecturer: {u.FullName}"));
            return list;
        }
    }
}
