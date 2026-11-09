namespace Student_Information_system.Models
{
    public static class Roles
    {
        public const string Student = "Student";
        public const string Lecturer = "Lecturer";
        public const string Management = "Management";
    }

    public class AppUser
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Username { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public string Role { get; set; } = Roles.Student;
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string? Phone { get; set; }
        public string? Department { get; set; }
        public bool Active { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public enum StudentStatus { Applicant, Active, Suspended, Graduated, Rejected }

    public class StudentProfile
    {
        public string UserId { get; set; } = "";
        public string StudentNumber { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string IdNumber { get; set; } = "";
        public DateTime? DateOfBirth { get; set; }
        public string Gender { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Email { get; set; } = "";
        public string Address { get; set; } = "";
        public string Programme { get; set; } = "";
        public string HighSchool { get; set; } = "";
        public int? MatricYear { get; set; }
        public string NextOfKinName { get; set; } = "";
        public string NextOfKinPhone { get; set; } = "";
        public StudentStatus Status { get; set; } = StudentStatus.Applicant;
        public DateTime RegisteredAt { get; set; } = DateTime.Now;
        public string FullName => $"{FirstName} {LastName}";
    }

    public class Course
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Programme { get; set; } = "";
        public int Credits { get; set; }
        public int Capacity { get; set; } = 40;
        public string Semester { get; set; } = "Semester 1 2026";
        public string? LecturerId { get; set; }
        public decimal Fee { get; set; }
        public bool OpenForRegistration { get; set; } = true;
    }

    public enum EnrollmentStatus { Pending, Approved, Rejected, Dropped }

    public class Assessment
    {
        public string Name { get; set; } = "";
        public int Weight { get; set; }
        public decimal? Mark { get; set; }
    }

    public class Enrollment
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string StudentId { get; set; } = "";
        public string CourseCode { get; set; } = "";
        public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Pending;
        public DateTime RequestedAt { get; set; } = DateTime.Now;
        public List<Assessment> Assessments { get; set; } = new();
        public string? LecturerComment { get; set; }
        public DateTime? MarksUpdatedAt { get; set; }

        public decimal? FinalMark
        {
            get
            {
                var marked = Assessments.Where(a => a.Mark.HasValue).ToList();
                if (marked.Count == 0) return null;
                var w = marked.Sum(a => a.Weight);
                if (w == 0) return null;
                return Math.Round(marked.Sum(a => a.Mark!.Value * a.Weight) / w, 1);
            }
        }

        public string Result => FinalMark switch
        {
            null => "In progress",
            >= 75 => "Distinction",
            >= 50 => "Pass",
            _ => "Fail"
        };

        public static List<Assessment> DefaultAssessments() => new()
        {
            new() { Name = "Assignment 1", Weight = 15 },
            new() { Name = "Assignment 2", Weight = 15 },
            new() { Name = "Test", Weight = 20 },
            new() { Name = "Exam", Weight = 50 },
        };
    }

    public static class DocumentTypes
    {
        public const string IdCopy = "Copy of ID / Passport";
        public const string MatricCertificate = "High School Certificate";
        public const string ProofOfAddress = "Proof of Address";
        public const string ProofOfPayment = "Proof of Payment";
        public const string Other = "Other";
        public static readonly string[] All = { IdCopy, MatricCertificate, ProofOfAddress, ProofOfPayment, Other };
    }

    public class StudentDocument
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string StudentId { get; set; } = "";
        public string Type { get; set; } = DocumentTypes.Other;
        public string FileName { get; set; } = "";
        public string StoredName { get; set; } = "";
        public string ContentType { get; set; } = "application/octet-stream";
        public long Size { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.Now;
        public bool Verified { get; set; }
        public string? VerifiedBy { get; set; }
    }

    public class CourseMaterial
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string CourseCode { get; set; } = "";
        public int Week { get; set; } = 1;
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Kind { get; set; } = "Notes"; // Notes, Slides, Assignment, Video, Link
        public string? FileName { get; set; }
        public string? StoredName { get; set; }
        public string? ContentType { get; set; }
        public string? Url { get; set; }
        public DateTime? DueDate { get; set; }
        public string UploadedBy { get; set; } = "";
        public DateTime UploadedAt { get; set; } = DateTime.Now;
    }

    public static class Audiences
    {
        public const string Everyone = "everyone";
        public const string Students = "students";
        public const string Lecturers = "lecturers";
        public const string CoursePrefix = "course:";
        public const string UserPrefix = "user:";
    }

    public class Message
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string SenderId { get; set; } = "";
        public string Audience { get; set; } = Audiences.Everyone;
        public string Subject { get; set; } = "";
        public string Body { get; set; } = "";
        public string Priority { get; set; } = "Normal"; // Normal, Important, Urgent
        public DateTime SentAt { get; set; } = DateTime.Now;
        public HashSet<string> ReadBy { get; set; } = new();
    }

    public class SisData
    {
        public List<AppUser> Users { get; set; } = new();
        public List<StudentProfile> Students { get; set; } = new();
        public List<Course> Courses { get; set; } = new();
        public List<Enrollment> Enrollments { get; set; } = new();
        public List<StudentDocument> Documents { get; set; } = new();
        public List<CourseMaterial> Materials { get; set; } = new();
        public List<Message> Messages { get; set; } = new();
        public int NextStudentSeq { get; set; } = 1;
    }
}
