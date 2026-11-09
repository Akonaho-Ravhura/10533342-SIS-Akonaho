using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Student_Information_system.Models;
using Student_Information_system.Services;

namespace Student_Information_system.Controllers
{
    public class AccountController : SisControllerBase
    {
        public AccountController(DataStore store) : base(store) { }

        [HttpGet]
        public IActionResult Login(string? returnUrl)
        {
            if (User.Identity?.IsAuthenticated == true) return RedirectToPortal(User.FindFirstValue(ClaimTypes.Role));
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);
            var user = Store.Data.Users.FirstOrDefault(u => u.Username.Equals(vm.Username.Trim(), StringComparison.OrdinalIgnoreCase));
            if (user == null || !DataStore.VerifyPassword(vm.Password, user.PasswordHash))
            {
                ModelState.AddModelError("", "Incorrect username or password.");
                return View(vm);
            }
            if (!user.Active)
            {
                ModelState.AddModelError("", "This account has been deactivated. Please contact the Registrar's Office.");
                return View(vm);
            }
            await SignIn(user);
            if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl)) return Redirect(vm.ReturnUrl);
            return RedirectToPortal(user.Role);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        public IActionResult AccessDenied() => View();

        [HttpGet]
        public IActionResult Apply() => View(new ApplyViewModel());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Apply(ApplyViewModel vm)
        {
            var docs = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
            if (vm.IdCopy == null) ModelState.AddModelError(nameof(vm.IdCopy), "Please upload a copy of your ID or passport.");
            if (vm.MatricCertificate == null) ModelState.AddModelError(nameof(vm.MatricCertificate), "Please upload your high school certificate.");
            if (Store.Data.Users.Any(u => u.Username.Equals(vm.Username.Trim(), StringComparison.OrdinalIgnoreCase)))
                ModelState.AddModelError(nameof(vm.Username), "That username is already taken.");
            if (Store.Data.Students.Any(s => s.IdNumber == vm.IdNumber.Trim()))
                ModelState.AddModelError(nameof(vm.IdNumber), "An application with this ID number already exists.");
            if (!ModelState.IsValid) return View(vm);

            var user = new AppUser
            {
                Username = vm.Username.Trim(), Role = Roles.Student, FullName = $"{vm.FirstName.Trim()} {vm.LastName.Trim()}",
                Email = vm.Email.Trim(), Phone = vm.Phone, PasswordHash = DataStore.HashPassword(vm.Password)
            };

            var uploads = new List<StudentDocument>();
            foreach (var (file, type) in new[] { (vm.IdCopy!, DocumentTypes.IdCopy), (vm.MatricCertificate!, DocumentTypes.MatricCertificate) })
            {
                var (stored, err) = await Store.SaveUpload(file, "students/" + user.Id, docs);
                if (err != null)
                {
                    ModelState.AddModelError(type == DocumentTypes.IdCopy ? nameof(vm.IdCopy) : nameof(vm.MatricCertificate), err + " Use PDF, JPG or PNG.");
                    return View(vm);
                }
                uploads.Add(new StudentDocument { StudentId = user.Id, Type = type, FileName = Path.GetFileName(file.FileName), StoredName = stored, ContentType = file.ContentType, Size = file.Length });
            }

            var number = Store.Write(d =>
            {
                d.Users.Add(user);
                var p = new StudentProfile
                {
                    UserId = user.Id, StudentNumber = Store.NextStudentNumber(d), FirstName = vm.FirstName.Trim(), LastName = vm.LastName.Trim(),
                    IdNumber = vm.IdNumber.Trim(), DateOfBirth = vm.DateOfBirth, Gender = vm.Gender, Phone = vm.Phone, Email = vm.Email.Trim(),
                    Address = vm.Address, Programme = vm.Programme, HighSchool = vm.HighSchool, MatricYear = vm.MatricYear,
                    NextOfKinName = vm.NextOfKinName, NextOfKinPhone = vm.NextOfKinPhone, Status = StudentStatus.Applicant
                };
                d.Students.Add(p);
                d.Documents.AddRange(uploads);
                d.Messages.Add(new Message
                {
                    SenderId = d.Users.First(u => u.Role == Roles.Management).Id, Audience = Audiences.UserPrefix + user.Id, Priority = "Important",
                    Subject = "Application received", Body = $"Dear {p.FirstName}, thank you for applying to Horizon Private Training College. Your reference number is {p.StudentNumber}. The Registrar's Office will review your documents and notify you once your application is approved."
                });
                return p.StudentNumber;
            });

            await SignIn(user);
            Flash($"Application submitted! Your student number is {number}. You can now track your application status here.");
            return RedirectToAction("Index", "Student");
        }

        private async Task SignIn(AppUser user)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Name, user.FullName),
                new(ClaimTypes.Role, user.Role),
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        }

        private IActionResult RedirectToPortal(string? role) => role switch
        {
            Roles.Management => RedirectToAction("Index", "Management"),
            Roles.Lecturer => RedirectToAction("Index", "Lecturer"),
            _ => RedirectToAction("Index", "Student"),
        };

        [Authorize, HttpGet]
        public IActionResult Password() => View();

        [Authorize, HttpPost, ValidateAntiForgeryToken]
        public IActionResult Password(string current, string next, string confirm)
        {
            var me = Me!;
            if (!DataStore.VerifyPassword(current ?? "", me.PasswordHash)) ModelState.AddModelError("", "Your current password is incorrect.");
            else if ((next ?? "").Length < 8) ModelState.AddModelError("", "The new password must be at least 8 characters.");
            else if (next != confirm) ModelState.AddModelError("", "The new passwords do not match.");
            if (!ModelState.IsValid) return View();
            Store.Write(_ => me.PasswordHash = DataStore.HashPassword(next!));
            Flash("Your password has been changed.");
            return RedirectToAction(nameof(Password));
        }
    }
}
