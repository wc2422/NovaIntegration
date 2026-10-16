using Microsoft.AspNetCore.Mvc;


namespace NovaIntegration.Web.Controllers
{
    public class AccountController : Controller
    {
        [HttpGet]
        public IActionResult Login()
        {
            return View("Index");
        }


    }
}