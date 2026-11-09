using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Student_Information_system.Models;

namespace Student_Information_system.Services
{
    /// <summary>
    /// Prototype persistence: the whole dataset lives in memory and is saved to App_Data/sis-data.json.
    /// Uploaded files are stored under App_Data/uploads (outside wwwroot so they are only served through authorised controllers).
    /// Swap this for EF Core + SQL Server when moving beyond the prototype.
    /// </summary>
    public class DataStore
    {
        private readonly object _lock = new();
        private readonly string _dataFile;
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public string UploadRoot { get; }
        public SisData Data { get; private set; }

        public DataStore(IWebHostEnvironment env)
        {
            var appData = Path.Combine(env.ContentRootPath, "App_Data");
            UploadRoot = Path.Combine(appData, "uploads");
            Directory.CreateDirectory(UploadRoot);
            _dataFile = Path.Combine(appData, "sis-data.json");

            if (File.Exists(_dataFile))
            {
                Data = JsonSerializer.Deserialize<SisData>(File.ReadAllText(_dataFile), JsonOpts) ?? new SisData();
            }
            else
            {
                Data = Seed();
                Save();
            }
        }

        public void Save()
        {
            lock (_lock)
            {
                var tmp = _dataFile + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(Data, JsonOpts));
                File.Move(tmp, _dataFile, true);
            }
        }

        public T Write<T>(Func<SisData, T> change)
        {
            lock (_lock)
            {
                var r = change(Data);
                Save();
                return r;
            }
        }

        public void Write(Action<SisData> change) => Write(d => { change(d); return true; });

        // ---------- lookups ----------
        public AppUser? User(string? id) => Data.Users.FirstOrDefault(u => u.Id == id);
        public StudentProfile? Student(string? userId) => Data.Students.FirstOrDefault(s => s.UserId == userId);
        public Course? Course(string? code) => Data.Courses.FirstOrDefault(c => c.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        public string UserName(string? id) => User(id)?.FullName ?? "—";

        public IEnumerable<Message> MessagesFor(AppUser user)
        {
            var courses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (user.Role == Roles.Student)
                foreach (var e in Data.Enrollments.Where(e => e.StudentId == user.Id && e.Status == EnrollmentStatus.Approved))
                    courses.Add(e.CourseCode);
            if (user.Role == Roles.Lecturer)
                foreach (var c in Data.Courses.Where(c => c.LecturerId == user.Id))
                    courses.Add(c.Code);

            return Data.Messages.Where(m =>
                    m.SenderId == user.Id ||
                    m.Audience == Audiences.Everyone ||
                    (m.Audience == Audiences.Students && user.Role == Roles.Student) ||
                    (m.Audience == Audiences.Lecturers && user.Role == Roles.Lecturer) ||
                    (m.Audience.StartsWith(Audiences.CoursePrefix) && courses.Contains(m.Audience[Audiences.CoursePrefix.Length..])) ||
                    m.Audience == Audiences.UserPrefix + user.Id ||
                    user.Role == Roles.Management)
                .OrderByDescending(m => m.SentAt);
        }

        public string AudienceLabel(string audience)
        {
            if (audience == Audiences.Everyone) return "Everyone";
            if (audience == Audiences.Students) return "All students";
            if (audience == Audiences.Lecturers) return "All lecturers";
            if (audience.StartsWith(Audiences.CoursePrefix)) return "Course " + audience[Audiences.CoursePrefix.Length..];
            if (audience.StartsWith(Audiences.UserPrefix)) return UserName(audience[Audiences.UserPrefix.Length..]);
            return audience;
        }

        public string NextStudentNumber(SisData d) => $"HPTC{DateTime.Now:yy}{d.NextStudentSeq++:D4}";

        // ---------- files ----------
        public static readonly string[] AllowedExtensions = { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx", ".ppt", ".pptx", ".xlsx", ".zip", ".txt", ".mp4" };
        public const long MaxUploadBytes = 20 * 1024 * 1024;

        public async Task<(string stored, string? error)> SaveUpload(IFormFile file, string folder, string[]? allowed = null)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!(allowed ?? AllowedExtensions).Contains(ext)) return ("", $"File type {ext} is not allowed.");
            if (file.Length == 0) return ("", "The file is empty.");
            if (file.Length > MaxUploadBytes) return ("", "Files must be 20 MB or smaller.");
            var dir = Path.Combine(UploadRoot, folder);
            Directory.CreateDirectory(dir);
            var stored = Path.Combine(folder, Guid.NewGuid().ToString("N") + ext);
            await using var fs = File.Create(Path.Combine(UploadRoot, stored));
            await file.CopyToAsync(fs);
            return (stored, null);
        }

        public string PhysicalPath(string stored) => Path.Combine(UploadRoot, stored);

        // ---------- passwords ----------
        public static string HashPassword(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(16);
            var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
            return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }

        public static bool VerifyPassword(string password, string stored)
        {
            var parts = stored.Split('.');
            if (parts.Length != 2) return false;
            var salt = Convert.FromBase64String(parts[0]);
            var expected = Convert.FromBase64String(parts[1]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }

        // ---------- seed ----------
        private SisData Seed()
        {
            var d = new SisData();
            AppUser U(string username, string role, string name, string email, string? dept = null)
            {
                var u = new AppUser { Username = username, Role = role, FullName = name, Email = email, Department = dept, PasswordHash = HashPassword("Password123!") };
                d.Users.Add(u);
                return u;
            }

            var admin = U("admin", Roles.Management, "Nomsa Dlamini", "registrar@horizoncollege.ac.za", "Registrar's Office");
            U("principal", Roles.Management, "Dr. Peter van Wyk", "principal@horizoncollege.ac.za", "Executive");
            var l1 = U("lecturer", Roles.Lecturer, "Mr. Thabo Mokoena", "t.mokoena@horizoncollege.ac.za", "Information Technology");
            var l2 = U("lecturer2", Roles.Lecturer, "Ms. Sarah Naidoo", "s.naidoo@horizoncollege.ac.za", "Business Studies");

            d.Courses.AddRange(new[]
            {
                new Course { Code = "ITF101", Name = "Introduction to Computing", Programme = "Diploma in Information Technology", Credits = 12, Fee = 4500, LecturerId = l1.Id, Description = "Computer hardware, software, operating systems and digital literacy fundamentals." },
                new Course { Code = "PRG102", Name = "Programming Fundamentals (C#)", Programme = "Diploma in Information Technology", Credits = 16, Fee = 5200, LecturerId = l1.Id, Description = "Variables, control flow, methods, OOP basics and problem solving with C#." },
                new Course { Code = "WEB103", Name = "Web Development I", Programme = "Diploma in Information Technology", Credits = 12, Fee = 4800, LecturerId = l1.Id, Description = "HTML, CSS and JavaScript for building responsive websites." },
                new Course { Code = "BUS101", Name = "Business Management I", Programme = "Higher Certificate in Business Management", Credits = 12, Fee = 4200, LecturerId = l2.Id, Description = "Management functions, business environment and organisational structures." },
                new Course { Code = "ACC102", Name = "Financial Accounting I", Programme = "Higher Certificate in Business Management", Credits = 16, Fee = 4900, LecturerId = l2.Id, Description = "Accounting equation, journals, ledgers and basic financial statements." },
                new Course { Code = "COM100", Name = "Business Communication", Programme = "General", Credits = 8, Fee = 3000, LecturerId = l2.Id, Description = "Professional writing, presentations and workplace communication skills." },
            });

            StudentProfile S(string username, string first, string last, string idNo, string programme, StudentStatus status)
            {
                var u = U(username, Roles.Student, $"{first} {last}", $"{username}@student.horizoncollege.ac.za");
                var p = new StudentProfile
                {
                    UserId = u.Id, StudentNumber = NextStudentNumber(d), FirstName = first, LastName = last, IdNumber = idNo,
                    DateOfBirth = new DateTime(2004, 3, 14), Gender = "Prefer not to say", Phone = "071 000 0000", Email = u.Email,
                    Address = "12 Example Street, Johannesburg, 2001", Programme = programme, HighSchool = "Example High School",
                    MatricYear = 2022, NextOfKinName = "Parent / Guardian", NextOfKinPhone = "082 000 0000", Status = status,
                    RegisteredAt = DateTime.Now.AddDays(-40)
                };
                d.Students.Add(p);
                return p;
            }

            var s1 = S("student", "Lerato", "Khumalo", "0403140000081", "Diploma in Information Technology", StudentStatus.Active);
            var s2 = S("student2", "Sipho", "Ndlovu", "0311250000087", "Diploma in Information Technology", StudentStatus.Active);
            var s3 = S("student3", "Aisha", "Patel", "0507020000086", "Higher Certificate in Business Management", StudentStatus.Active);
            S("applicant", "Johan", "Botha", "0601010000084", "Diploma in Information Technology", StudentStatus.Applicant);

            void E(StudentProfile s, string code, EnrollmentStatus st, params decimal?[] marks)
            {
                var e = new Enrollment { StudentId = s.UserId, CourseCode = code, Status = st, RequestedAt = DateTime.Now.AddDays(-35), Assessments = Enrollment.DefaultAssessments() };
                for (var i = 0; i < marks.Length && i < e.Assessments.Count; i++) e.Assessments[i].Mark = marks[i];
                if (marks.Length > 0) e.MarksUpdatedAt = DateTime.Now.AddDays(-3);
                d.Enrollments.Add(e);
            }
            E(s1, "ITF101", EnrollmentStatus.Approved, 78, 82, 71);
            E(s1, "PRG102", EnrollmentStatus.Approved, 65, 58);
            E(s1, "COM100", EnrollmentStatus.Pending);
            E(s2, "ITF101", EnrollmentStatus.Approved, 55, 61, 48);
            E(s2, "WEB103", EnrollmentStatus.Approved, 88);
            E(s3, "BUS101", EnrollmentStatus.Approved, 72, 69, 80);
            E(s3, "ACC102", EnrollmentStatus.Approved, 49, 57);

            void M(string code, int week, string title, string kind, string desc, string? url = null, int? dueInDays = null, string? by = null)
                => d.Materials.Add(new CourseMaterial { CourseCode = code, Week = week, Title = title, Kind = kind, Description = desc, Url = url, DueDate = dueInDays.HasValue ? DateTime.Today.AddDays(dueInDays.Value) : null, UploadedBy = by ?? l1.Id, UploadedAt = DateTime.Now.AddDays(-30 + week * 7) });
            M("ITF101", 1, "Course outline & study guide", "Notes", "Module outcomes, assessment schedule and recommended reading.");
            M("ITF101", 1, "How computers work", "Video", "Short introductory video on the components of a computer.", "https://www.youtube.com/results?search_query=how+computers+work");
            M("ITF101", 2, "Operating systems", "Slides", "Lecture slides on processes, memory and file systems.");
            M("ITF101", 3, "Assignment 2: Build a PC spec", "Assignment", "Research and cost a PC build for a small office. Submit a 2-page PDF.", null, 10);
            M("PRG102", 1, "Installing Visual Studio", "Link", "Setup guide for the development environment.", "https://visualstudio.microsoft.com/vs/community/");
            M("PRG102", 2, "Loops and decisions", "Notes", "if/else, switch, for, while and foreach with worked examples.");
            M("WEB103", 1, "HTML basics", "Link", "MDN reference for HTML elements.", "https://developer.mozilla.org/en-US/docs/Web/HTML");
            M("BUS101", 1, "Introduction to management", "Notes", "The four functions of management.", by: l2.Id);
            M("ACC102", 1, "The accounting equation", "Slides", "Assets = Liabilities + Equity with examples.", by: l2.Id);

            d.Messages.AddRange(new[]
            {
                new Message { SenderId = admin.Id, Audience = Audiences.Everyone, Priority = "Important", Subject = "Welcome to the 2026 academic year", Body = "Welcome to Horizon Private Training College. Please make sure your ID copy and high school certificate are uploaded on the Documents page before the end of the month.", SentAt = DateTime.Now.AddDays(-20) },
                new Message { SenderId = admin.Id, Audience = Audiences.Students, Priority = "Urgent", Subject = "Exam timetable released", Body = "The final examination timetable is now available. Exams start on 24 November. Bring your student card and ID to every exam.", SentAt = DateTime.Now.AddDays(-2) },
                new Message { SenderId = l1.Id, Audience = Audiences.CoursePrefix + "ITF101", Priority = "Normal", Subject = "Test marks released", Body = "Your ITF101 test marks are now available on the portal. Consultation hours are Thursday 14:00–16:00.", SentAt = DateTime.Now.AddDays(-3) },
                new Message { SenderId = admin.Id, Audience = Audiences.Lecturers, Priority = "Important", Subject = "Mark submission deadline", Body = "All semester marks must be captured on the system by 30 November.", SentAt = DateTime.Now.AddDays(-5) },
            });

            return d;
        }
    }
}
