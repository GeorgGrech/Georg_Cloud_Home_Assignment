using Google.Cloud.Speech.V1;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Models;
using Google.Cloud.PubSub.V1;
using System.Threading;
using Newtonsoft.Json;

namespace SubscriberApp.Controllers
{
    public class SubscriberController : Controller
    {
        private IWebHostEnvironment Environment;

        public SubscriberController(IWebHostEnvironment environment)
        {
            Environment = environment;
        }

        public async Task<IActionResult> Index()
        {
            /*
            TestConvert(Environment.ContentRootPath + "\\TheIrishmanClip.mp4");
            string transcription = TestTranscribe(Environment.ContentRootPath + "\\export.flac");

            return Content(transcription);*/

            bool acknowledge = true; //true - message will be pulled permanently from the queue
                                     //false - message will be restored back into the queue once the deadline of the acknowledgement exceeds
            string projectId = "georg-cloud-home-assignment";
            string subscriptionId = "movie-queue-sub";

            SubscriptionName subscriptionName = SubscriptionName.FromProjectSubscription(projectId, subscriptionId);
            SubscriberClient subscriber = await SubscriberClient.CreateAsync(subscriptionName);
            // SubscriberClient runs your message handle function on multiple
            // threads to maximize throughput.
            int messageCount = 0;
            string messageOutput = "";
            Task startTask = subscriber.StartAsync((PubsubMessage message, CancellationToken cancel) =>
            {
                string text = System.Text.Encoding.UTF8.GetString(message.Data.ToArray());

                //code that sends out the email
                Movie m = JsonConvert.DeserializeObject<Movie>(text);

                messageOutput += $"Message {message.MessageId}: {text}";
                Console.WriteLine($"Message {message.MessageId}: {text}");
                Interlocked.Increment(ref messageCount);
                return Task.FromResult(acknowledge ? SubscriberClient.Reply.Ack : SubscriberClient.Reply.Nack);
            });

            // Run for 5 seconds.
            await Task.Delay(5000);
            await subscriber.StopAsync(CancellationToken.None);
            // Lets make sure that the start task finished successfully after the call to stop.
            await startTask;
            return Content(messageCount.ToString());

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
