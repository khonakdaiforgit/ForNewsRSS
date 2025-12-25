using Microsoft.AspNetCore.Mvc;

namespace ForNewsRSS.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return Content("Running ...");
        }
    }
}
