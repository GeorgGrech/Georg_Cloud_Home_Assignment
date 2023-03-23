using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Georg_Cloud_Home_Assignment.Controllers
{
    public class MoviesController : Controller
    {
        public IActionResult Create()
        {
            return View();
        }
    }
}
