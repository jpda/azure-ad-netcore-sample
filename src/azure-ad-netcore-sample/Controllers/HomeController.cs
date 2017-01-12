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

        [Authorize(Roles = "82c95acc-9010-4822-9a0c-9769e5381cdb")]
        public IActionResult About()
        {
            ViewData["Message"] = "This is an authenticated page to a specific group GUID. You made it!";
            ViewBag.GroupName = "";
            var s = new System.Net.Http.HttpClient();
            s.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer ");
            var groupData = s.GetStringAsync("https://graph.microsoft.com/v1.0/groups/82c95acc-9010-4822-9a0c-9769e5381cdb").Result;
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
