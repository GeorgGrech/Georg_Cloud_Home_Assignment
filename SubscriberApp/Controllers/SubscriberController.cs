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
using Google.Cloud.Storage.V1;
using System.IO;
using Google.Cloud.Firestore;
using Grpc.Core;

namespace SubscriberApp.Controllers
{
    public class SubscriberController : Controller
    {
        IWebHostEnvironment Environment;
        public SubscriberController(IWebHostEnvironment environment)
        {
            Environment = environment;
        }

        public IActionResult Index()
        {
            bool acknowledge = true; //true - message will be pulled permanently from the queue
                                     //false - message will be restored back into the queue once the deadline of the acknowledgement exceeds
            string projectId = "georg-cloud-home-assignment";
            string subscriptionId = "movie-queue-sub";

            SubscriptionName subscriptionName = SubscriptionName.FromProjectSubscription(projectId, subscriptionId);
            SubscriberServiceApiClient subscriberClient = SubscriberServiceApiClient.Create();
            int messageCount = 0;

            //string messageOutput = "";

            try
            {
                // Pull messages from server,
                // allowing an immediate response if there are no messages.
                PullResponse response = subscriberClient.Pull(subscriptionName, maxMessages: 20);
                // Print out each received message.
                foreach (ReceivedMessage msg in response.ReceivedMessages)
                {
                    string text = System.Text.Encoding.UTF8.GetString(msg.Message.Data.ToArray());
                    Movie m = JsonConvert.DeserializeObject<Movie>(text);
                    Console.WriteLine($"Message {msg.Message.MessageId}: {text}");

                    TranscriptionProcess(m);

                    Interlocked.Increment(ref messageCount);
                }
                // If acknowledgement required, send to server.
                if (acknowledge && messageCount > 0)
                {
                    subscriberClient.Acknowledge(subscriptionName, response.ReceivedMessages.Select(msg => msg.AckId));
                }
            }
            catch (RpcException ex) /*when (ex.Status.StatusCode == StatusCode.Unavailable)*/
            {
                Console.WriteLine("Caught exception: " + ex);
                // UNAVAILABLE due to too many concurrent pull requests pending for the given subscription.
            }

            return Content(messageCount.ToString());

        }

        public async void TranscriptionProcess(Movie m)//method that encapsulates all transcription process. Will run seperately to not interfere with pub sub delay
        {
            //Preparation for pt 1 - Convert to Flac
            var storage = StorageClient.Create();
            string bucketName = "georg_movie_app_bucket";

            //Preparation for pt 2 - Transcription 
            var speech = SpeechClient.Create();
            var config = new RecognitionConfig
            {
                Encoding = RecognitionConfig.Types.AudioEncoding.Flac,
                //SampleRateHertz = 16000,
                AudioChannelCount = 2,
                LanguageCode = LanguageCodes.English.UnitedStates
            };

            //Preperation for pt 3 - Upload Transcription
            string projectId = "georg-cloud-home-assignment";
            FirestoreDb db = FirestoreDb.Create(projectId);

            try
            {
                string flacFileName = await ConvertAndUploadFlac(m.LinkToMovie, bucketName, storage); //Convert, upload Flac, and retrieve file name

                Stream transcriptionStream = Transcribe(flacFileName, bucketName, speech, config, storage);

                UploadTranscription(transcriptionStream, bucketName, storage, m, db);
            }
            catch (Exception e)
            {
                Console.WriteLine("Transcription unsuccseful. Object most likely deleted. Exception: "+e);
            }
        }



        public async Task<string> ConvertAndUploadFlac(string inputUrl, string bucketName, StorageClient storage)
        {
            var ffMpeg = new NReco.VideoConverter.FFMpegConverter();
            ffMpeg.ConvertMedia(inputUrl, Environment.WebRootPath + "\\export.wav", "wav"); //What's the point of this when I can just convert to flac immediately?


            Stream flacStream = new MemoryStream();
            ffMpeg.ConvertMedia(Environment.WebRootPath + "\\export.wav", flacStream, "flac");

            string newFileName = Guid.NewGuid().ToString() + ".flac";
            await storage.UploadObjectAsync(bucketName, newFileName, null, flacStream);

            System.IO.File.Delete(Environment.WebRootPath + "\\export.wav"); //Delete now uneccesary wav file

            return newFileName; //return in GCS URL format
        }

        public Stream Transcribe(string flacFileName, string bucketName, SpeechClient speech, RecognitionConfig config, StorageClient storage)
        {
            var audio = RecognitionAudio.FromStorageUri($"gs://{bucketName}/{flacFileName}");
            var response = speech.Recognize(config, audio);

            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);

            string transcription = "";

            foreach (var result in response.Results)
            {
                foreach (var alternative in result.Alternatives)
                {
                    //Console.WriteLine(alternative.Transcript);
                    transcription += alternative.Transcript + "\n";
                }
            }

            writer.Write(transcription);
            writer.Flush();
            stream.Position = 0;

            storage.DeleteObject(bucketName,flacFileName); //Delete now unnecessary flac file

            return stream;
        }


        public async void UploadTranscription(Stream transcriptionStream, string bucketName, StorageClient storage, Movie m, FirestoreDb db)
        {

            string transcriptFileName = m.Id + ".txt"; //Currently saving as txt, later save as .srt

            await storage.UploadObjectAsync("georg_movie_app_bucket", transcriptFileName, null, transcriptionStream);

            m.LinkToTranscription = $"https://storage.googleapis.com/{bucketName}/{transcriptFileName}";

            UpdateFirestore(m, db);
        }

        public async void UpdateFirestore(Movie m, FirestoreDb db)
        {
            Query allMoviesQuery = db.Collection("movies").WhereEqualTo("Id", m.Id);
            QuerySnapshot allMoviesQuerySnapshot = await allMoviesQuery.GetSnapshotAsync();

            string entryId = allMoviesQuerySnapshot.Documents[0].Id; //Id of entry in Firestore
            DocumentReference docRef = db.Collection("movies").Document(entryId);
            await docRef.SetAsync(m);
        }
    }
}
