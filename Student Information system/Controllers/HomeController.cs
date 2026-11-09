using Microsoft.AspNetCore.Mvc;
using Student_Information_system.Models;
using Student_Information_system.Services;
using System.Diagnostics;

namespace Student_Information_system.Controllers
{
    public class HomeController : SisControllerBase
    {
        public HomeController(DataStore store) : base(store) { }

        public IActionResult Index() => View(Store.Data.Courses.Where(c => c.OpenForRegistration).OrderBy(c => c.Programme).ThenBy(c => c.Code).ToList());

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
