﻿using Google.Cloud.Speech.V1;
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
using Google.Protobuf;

namespace SubscriberApp.Controllers
{
    public class SubscriberController : Controller
    {
        IWebHostEnvironment Environment;
        string projectId = "georg-cloud-home-assignment";
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
                    Interlocked.Increment(ref messageCount);

                    TranscriptionProcess(m);

                }
                // If acknowledgement required, send to server.
                if (acknowledge && messageCount > 0)
                {
                    Console.WriteLine("Attempting Acknowledge");
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
                LanguageCode = LanguageCodes.English.UnitedStates,
                EnableWordTimeOffsets = true //Enable timing
            };

            //Preperation for pt 3 - Upload Transcription
            FirestoreDb db = FirestoreDb.Create(projectId);

            try
            {
                string flacFileName = await ConvertAndUploadFlac(m.LinkToMovie, bucketName, storage); //Convert, upload Flac, and retrieve file name
                m.FlacFileName = flacFileName;
                UpdateFirestore(m, db);
                PushMessage(m.Id);
                
            }
            catch (Exception e)
            {
                Console.WriteLine("Transcription unsuccseful. Object most likely deleted. Exception: "+e);
            }
        }


        public async Task<string> ConvertAndUploadFlac(string inputUrl, string bucketName, StorageClient storage)
        {
            var ffMpeg = new NReco.VideoConverter.FFMpegConverter();

            string audioGuid = Guid.NewGuid().ToString();

            Stream flacStream = new MemoryStream();
            ffMpeg.ConvertMedia(inputUrl, flacStream, "flac"); //Skip wav, convert to flac immediately

            string flacFileName = audioGuid + ".flac";
            await storage.UploadObjectAsync(bucketName, flacFileName, null, flacStream);


            return flacFileName;
        }


        public async void UpdateFirestore(Movie m, FirestoreDb db)
        {
            Query allMoviesQuery = db.Collection("movies").WhereEqualTo("Id", m.Id);
            QuerySnapshot allMoviesQuerySnapshot = await allMoviesQuery.GetSnapshotAsync();

            string entryId = allMoviesQuerySnapshot.Documents[0].Id; //Id of entry in Firestore
            DocumentReference docRef = db.Collection("movies").Document(entryId);
            await docRef.SetAsync(m);
        }

        public async void PushMessage(string id)
        {
            TopicName topicName = TopicName.FromProjectTopic(projectId, "to-srt-queue");
            PublisherClient publisher = await PublisherClient.CreateAsync(topicName);
            
            await publisher.PublishAsync(id); //the message (reservation as json string) will be published onto the queue
        }

    }
}
