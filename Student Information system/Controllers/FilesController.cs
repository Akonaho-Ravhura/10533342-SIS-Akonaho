using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Student_Information_system.Models;
using Student_Information_system.Services;

namespace Student_Information_system.Controllers
{
    /// <summary>Serves uploaded files only after checking the caller may see them.</summary>
    [Authorize]
    public class FilesController : SisControllerBase
    {
        public FilesController(DataStore store) : base(store) { }

        // Student documents (ID copy, certificates…): the student themselves or Management.
        public IActionResult Document(string id, bool download = false)
        {
            var doc = Store.Data.Documents.FirstOrDefault(d => d.Id == id);
            if (doc == null) return NotFound();
            var me = Me!;
            if (me.Role != Roles.Management && doc.StudentId != me.Id) return Forbid();
            return Serve(doc.StoredName, doc.ContentType, doc.FileName, download);
        }

        // Course materials: enrolled (approved) students, the course lecturer, or Management.
        public IActionResult Material(string id, bool download = false)
        {
            var m = Store.Data.Materials.FirstOrDefault(x => x.Id == id);
            if (m == null || m.StoredName == null) return NotFound();
            var me = Me!;
            var course = Store.Course(m.CourseCode);
            var allowed = me.Role == Roles.Management
                || (me.Role == Roles.Lecturer && course?.LecturerId == me.Id)
                || Store.Data.Enrollments.Any(e => e.StudentId == me.Id && e.CourseCode == m.CourseCode && e.Status == EnrollmentStatus.Approved);
            if (!allowed) return Forbid();
            return Serve(m.StoredName, m.ContentType ?? "application/octet-stream", m.FileName ?? "file", download);
        }

        private IActionResult Serve(string stored, string contentType, string fileName, bool download)
        {
            var path = Store.PhysicalPath(stored);
            if (!System.IO.File.Exists(path)) return NotFound();
            var stream = System.IO.File.OpenRead(path);
            return download ? File(stream, contentType, fileName) : File(stream, contentType);
        }
    }
}
