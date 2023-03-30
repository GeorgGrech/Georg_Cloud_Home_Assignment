using Google.Cloud.Speech.V1;
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
            
            TestConvert(Environment.ContentRootPath + "\\TheIrishmanClip.mp4");
            string transcription = TestTranscribe(Environment.ContentRootPath + "\\export.flac");

            return Content(transcription);
        }

        void TestConvert(string path)
        {
            var ffMpeg = new NReco.VideoConverter.FFMpegConverter();
            ffMpeg.ConvertMedia(path, Environment.ContentRootPath + "\\export.wav", "wav"); //What's the point of this when I can just convert to flac immediately?
            ffMpeg.ConvertMedia(Environment.ContentRootPath + "\\export.wav", Environment.ContentRootPath + "\\export.flac", "flac");
        }

        string TestTranscribe(string path)
        {
            var speech = SpeechClient.Create();
            var config = new RecognitionConfig
            {
                Encoding = RecognitionConfig.Types.AudioEncoding.Flac,
                //SampleRateHertz = 16000,
                AudioChannelCount = 2,
                LanguageCode = LanguageCodes.English.UnitedStates
            };
            var audio = RecognitionAudio.FromFile(path); //Change to FromStorageUri later

            var response = speech.Recognize(config, audio);

            string transcription = "";

            foreach (var result in response.Results)
            {
                foreach (var alternative in result.Alternatives)
                {
                    //Console.WriteLine(alternative.Transcript);
                    transcription += alternative.Transcript + "\n";
                }
            }
            return transcription;
        }
    }
}
