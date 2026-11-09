using System.ComponentModel.DataAnnotations;

namespace Student_Information_system.Models
{
    public class LoginViewModel
    {
        [Required] public string Username { get; set; } = "";
        [Required, DataType(DataType.Password)] public string Password { get; set; } = "";
        public string? ReturnUrl { get; set; }
    }

    public class ApplyViewModel
    {
        [Required, Display(Name = "First name")] public string FirstName { get; set; } = "";
        [Required, Display(Name = "Last name")] public string LastName { get; set; } = "";
        [Required, Display(Name = "ID / Passport number"), StringLength(20, MinimumLength = 6)] public string IdNumber { get; set; } = "";
        [Required, DataType(DataType.Date), Display(Name = "Date of birth")] public DateTime? DateOfBirth { get; set; }
        [Required] public string Gender { get; set; } = "";
        [Required, Phone] public string Phone { get; set; } = "";
        [Required, EmailAddress] public string Email { get; set; } = "";
        [Required] public string Address { get; set; } = "";
        [Required] public string Programme { get; set; } = "";
        [Required, Display(Name = "High school attended")] public string HighSchool { get; set; } = "";
        [Required, Range(1980, 2030), Display(Name = "Year of matric")] public int? MatricYear { get; set; }
        [Required, Display(Name = "Next of kin name")] public string NextOfKinName { get; set; } = "";
        [Required, Phone, Display(Name = "Next of kin phone")] public string NextOfKinPhone { get; set; } = "";
        [Required, StringLength(30, MinimumLength = 4)] public string Username { get; set; } = "";
        [Required, DataType(DataType.Password), StringLength(64, MinimumLength = 8)] public string Password { get; set; } = "";
        [Required, DataType(DataType.Password), Compare(nameof(Password)), Display(Name = "Confirm password")] public string ConfirmPassword { get; set; } = "";
        [Display(Name = "Copy of ID / Passport")] public IFormFile? IdCopy { get; set; }
        [Display(Name = "High school certificate")] public IFormFile? MatricCertificate { get; set; }
        [Range(typeof(bool), "true", "true", ErrorMessage = "You must accept the declaration.")] public bool AcceptTerms { get; set; }
    }

    public static class Programmes
    {
        public static readonly string[] All =
        {
            "Diploma in Information Technology",
            "Higher Certificate in Business Management",
            "General",
        };
    }
}
