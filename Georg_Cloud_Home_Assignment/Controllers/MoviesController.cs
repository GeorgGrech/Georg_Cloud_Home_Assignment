using Georg_Cloud_Home_Assignment.DataAccess;
using Georg_Cloud_Home_Assignment.Models;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
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

        float uploadMax;

        public MoviesController(FirestoreMovieRepository _fmr)
        {
            fmr = _fmr;
        }

        [Authorize]
        public IActionResult Create()
        {
            TempData["progress"] = null;
            return View();
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateAsync(Movie m, IFormFile file)
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


                    uploadMax = fileStream.Length; //Set max to calculate progress bar percentage
                    var progressReporter = new Progress<Google.Apis.Upload.IUploadProgress>(OnUploadProgress);
                    await storage.UploadObjectAsync("georg_movie_app_bucket", newFilename, null, fileStream, progress: progressReporter);

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


        void OnUploadProgress(Google.Apis.Upload.IUploadProgress progress)
        {
            switch (progress.Status)
            {
                case Google.Apis.Upload.UploadStatus.Starting:
                    //ProgressBar.Minimum = 0;
                    TempData["progressinfo"] = "Starting upload...";
                    TempData["progress"] = 0;

                    break;
                case Google.Apis.Upload.UploadStatus.Completed:
                    TempData["progressinfo"] = "Upload complete!";
                    TempData["progress"] = 100;
                    //System.Windows.MessageBox.Show("Upload completed"); //Set as TempData later

                    break;
                case Google.Apis.Upload.UploadStatus.Uploading:
                    TempData["progressinfo"] = "Uploading file...";
                    UpdateProgressBar(progress.BytesSent);

                    break;
                case Google.Apis.Upload.UploadStatus.Failed:
                    TempData["progressinfo"] = "Upload failed.";
                    /*System.Windows.MessageBox.Show("Upload failed"
                                                   + Environment.NewLine
                                                   + progress.Exception);*/
                    break;
            }
        }

        void UpdateProgressBar(long value)
        {
            TempData["progress"] = (value/uploadMax)*100;
        }


    }
}
