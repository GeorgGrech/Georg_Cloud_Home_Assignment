using Georg_Cloud_Home_Assignment.DataAccess;
using Georg_Cloud_Home_Assignment.Models;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Georg_Cloud_Home_Assignment.Controllers
{
    public class MoviesController : Controller
    {

        FirestoreMovieRepository fmr;
        public MoviesController(FirestoreMovieRepository _fmr)
        {
            fmr = _fmr;
        }

        [Authorize]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [Authorize]
        public IActionResult Create(Movie m, IFormFile file)
        {
            try
            {
                string newFilename;
                //1) ebook is going to be stored in the cloud storage i.e. in the bucket with name msd63a2023ra
                if (file != null)
                {
                    var storage = StorageClient.Create();
                    using var fileStream = file.OpenReadStream(); //reads the uploaded file from the server's memory
                    newFilename = Guid.NewGuid().ToString() + System.IO.Path.GetExtension(file.FileName);

                    storage.UploadObject("georg_movie_app_bucket", newFilename, null, fileStream);
                    m.Link = $"https://storage.googleapis.com/{"georg_movie_app_bucket"}/{newFilename}";
                }

                //2) will store the book details/info in the NoSql database (Firestore)

                fmr.AddMovie(m);
                TempData["success"] = "Movie added successfully";
            }
            catch (Exception e)
            {
                //log the exceptions
                Console.WriteLine(e);
                TempData["error"] = "Movie was not added";
            }
            return View();
        }


        [Authorize]
        public IActionResult Index()
        {
            Task<List<Movie>> t = fmr.GetMovies();

            var list = t.Result;
            return View(list);
        }
    }
}
