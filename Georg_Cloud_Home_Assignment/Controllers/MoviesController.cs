using Georg_Cloud_Home_Assignment.DataAccess;
using Common.Models;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Google.Cloud.Firestore;
using Google.Cloud.Firestore.V1;

namespace Georg_Cloud_Home_Assignment.Controllers
{
    public class MoviesController : Controller
    {
        FirestoreMovieRepository fmr;
        PubSubRepository psr;

        float uploadMax;


        private IWebHostEnvironment Environment;
        public MoviesController(FirestoreMovieRepository _fmr, PubSubRepository _psr, IWebHostEnvironment _environment)
        {
            fmr = _fmr;
            psr = _psr;
            Environment = _environment;
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
                if (file != null)
                {
                    var storage = StorageClient.Create();
                    using var fileStream = file.OpenReadStream(); //reads the uploaded file from the server's memory

                    string GuidName = Guid.NewGuid().ToString();

                    m.Id = GuidName; //Auto Generated ID

                    newFilename = GuidName + Path.GetExtension(file.FileName);


                    uploadMax = fileStream.Length; //Set max to calculate progress bar percentage
                    var progressReporter = new Progress<Google.Apis.Upload.IUploadProgress>(OnUploadProgress);

                    //Upload movie
                    await storage.UploadObjectAsync("georg_movie_app_bucket", newFilename, null, fileStream, progress: progressReporter);

                    m.LinkToMovie = $"https://storage.googleapis.com/{"georg_movie_app_bucket"}/{newFilename}";

                    //Generate thumbnail
                    Stream tnStream = GenerateThumbnailStream(m.LinkToMovie);

                    //Upload thumbnail
                    string tnFileName = GuidName + "_tn.png";
                    await storage.UploadObjectAsync("georg_movie_app_bucket", tnFileName, null, tnStream);
                    m.LinkToThumbnail = $"https://storage.googleapis.com/{"georg_movie_app_bucket"}/{tnFileName}";

                    m.DownloadTimes = new List<Timestamp>(); //Create an empty list
                }


                fmr.AddMovie(m);
                TempData["success"] = "Movie added successfully";
                psr.PushMessage(m); //Push movie to pubsub
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

        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                var movie = await fmr.GetMovie(id);

                string[] urlsToCheck = {
                    movie.LinkToMovie,
                    movie.LinkToThumbnail,
                    movie.LinkToTranscription
                };

                var storage = StorageClient.Create();

                foreach(string url in urlsToCheck) //Check every url in movie
                {
                    if (url != null) //attempt delete only if not empty
                    {
                        string fileName = System.IO.Path.GetFileName(url);
                        storage.DeleteObject("georg_movie_app_bucket", fileName);
                    }
                }

                await fmr.DeleteMovie(id);
                TempData["success"] = "Movie was deleted successfully";
            }
            catch (Exception ex)
            {
                TempData["error"] = "Movie was not deleted";
            }

            return RedirectToAction("Index");

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

        Stream GenerateThumbnailStream(string filePath)
        {
            Stream tnStream = new MemoryStream();
            var ffMpeg = new NReco.VideoConverter.FFMpegConverter();
            ffMpeg.GetVideoThumbnail(filePath, tnStream);
            return tnStream;
            //System.IO.File.Delete(filePath); //Delete now unneeded file from path
        }

        public async Task<IActionResult> DownloadMovie(string id)
        {
            try
            {
                Movie m = await fmr.GetMovie(id); //Get movie
                
                m.DownloadTimes.Add(Timestamp.FromDateTime(DateTime.UtcNow));  //Add current utc dateTime (converted into google timestamp) to list of download times

                await fmr.AddDownloadTime(m); //Update movie to firestore

                return Redirect(m.LinkToMovie); //redirect to Movie Link to download movie
            }
            catch (Exception ex)
            {
                TempData["error"] = "Download time was not added";
            }

            return RedirectToAction("Index");
        }

    }
}
