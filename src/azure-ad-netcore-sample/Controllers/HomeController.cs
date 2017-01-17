using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AzureAdNetCoreSample.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            ViewBag.Claims = HttpContext.User.Claims;
            return View();
        }

        [Authorize(Roles = "Strategic Strategy Group")]
        public IActionResult About()
        {
            ViewData["Message"] = "This is an authenticated page to a specific group. You made it!";
            return View();
        }

        public IActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";

            return View();
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}
