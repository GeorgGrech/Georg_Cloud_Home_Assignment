using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SubscriberApp.Controllers
{
    public class SubscriberController : Controller
    {
        private IWebHostEnvironment Environment;

        public SubscriberController(IWebHostEnvironment environment)
        {
            Environment = environment;
        }

        public IActionResult Index()
        {
            try
            {
                TestConvert();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return Content("Successful.");
        }

        void TestConvert()
        {
            var ffMpeg = new NReco.VideoConverter.FFMpegConverter();
            ffMpeg.ConvertMedia(Environment.ContentRootPath + "\\irishmanCrop.mp4", Environment.ContentRootPath + "\\export.wav", "wav");
            ffMpeg.ConvertMedia(Environment.ContentRootPath + "\\export.wav", Environment.ContentRootPath + "\\export.flac", "flac");
        }
    }
}
