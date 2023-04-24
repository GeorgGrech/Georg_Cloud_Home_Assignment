using CloudNative.CloudEvents;
using Google.Cloud.Functions.Framework;
using Google.Events.Protobuf.Cloud.PubSub.V1;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Google.Cloud.Firestore;
using Google.Cloud.Firestore.V1;
using Microsoft.Extensions.Logging;
using Google.Cloud.Speech.V1;
using Google.Cloud.Storage.V1;
using System.IO;

namespace tosrtfunction;

/// <summary>
/// A function that can be triggered in responses to changes in Google Cloud Storage.
/// The type argument (StorageObjectData in this case) determines how the event payload is deserialized.
/// The function must be deployed so that the trigger matches the expected payload type. (For example,
/// deploying a function expecting a StorageObject payload will not work for a trigger that provides
/// a FirestoreEvent.)
/// </summary>
public class Function : ICloudEventFunction<MessagePublishedData>
{
        //Logging
     ILogger<Function> _logger;
  public Function(ILogger<Function> logger) =>
    _logger = logger;
    public Task HandleAsync(CloudEvent cloudEvent, MessagePublishedData data, CancellationToken cancellationToken)
    {
        var id = data.Message?.TextData;
        _logger.LogInformation($"Retrieved id: {id}");

        string projectId = "georg-cloud-home-assignment";
        FirestoreDb db = FirestoreDb.Create(projectId);
        _logger.LogInformation($"Connected with firestore");

        var storage = StorageClient.Create();
        string bucketName = "georg_movie_app_bucket";

        Process(id,bucketName,storage,db).Wait();
        //t.Wait();
        
        return Task.CompletedTask;
    }

    public async Task Process(string id, string bucketName, StorageClient storage, FirestoreDb db)
    {
        Movie m = await GetMovie(id,db);

        Stream transcriptionStream = Transcribe(m.FlacFileName, bucketName, storage); 
        UploadTranscription(transcriptionStream, bucketName, storage, m, db);
    }

    public async Task<Movie> GetMovie(string id,FirestoreDb db)
    {
        List<Movie> movies = new List<Movie>();
        Query allMoviesQuery = db.Collection("movies").WhereEqualTo("Id", id);
        QuerySnapshot allMoviesQuerySnapshot = await allMoviesQuery.GetSnapshotAsync();

        foreach (DocumentSnapshot documentSnapshot in allMoviesQuerySnapshot.Documents)
        {
            Movie m = documentSnapshot.ConvertTo<Movie>();
            movies.Add(m);
        }

        return movies.FirstOrDefault();
    }


    public Stream Transcribe(string flacFileName, string bucketName, StorageClient storage)
    {

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

        _logger.LogInformation($"Attempting search in gs://{bucketName}/{flacFileName}");

        var audio = RecognitionAudio.FromStorageUri($"gs://{bucketName}/{flacFileName}");
        var response = speech.Recognize(config, audio);

        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);

        string transcription = "";

        int lineNumber = 0;

        foreach (var result in response.Results)
        {
            foreach (var alternative in result.Alternatives)
            {
                lineNumber++;

                string lineStartTime = alternative.Words[0].StartTime.ToString();
                string lineEndTime = alternative.Words[^1].EndTime.ToString();

                transcription += lineNumber.ToString()+"\n";
                transcription += ConvertToSrtTime(lineStartTime,false) + " --> " + ConvertToSrtTime(lineEndTime,true) + "\n";
                //Console.WriteLine(alternative.Transcript);
                transcription += alternative.Transcript + "\n";
                transcription += "\n"; //Skip extra line

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

        string transcriptFileName = m.Id + ".srt";

        await storage.UploadObjectAsync("georg_movie_app_bucket", transcriptFileName, null, transcriptionStream);

        m.LinkToTranscription = $"https://storage.googleapis.com/{bucketName}/{transcriptFileName}";
        m.FlacFileName = null; //Delete FlacFileName

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



    private string ConvertToSrtTime(string time, bool endTime) //Convert Google Speech StartTime/EndTime into srt format
    {
        int trailTime = 1; //Amount of seconds subtitle remains on screen after speech finished

        string srtTime = time.Replace("\"", string.Empty).Replace("s", string.Empty); //Removes quotation marks and s


        string[] times = srtTime.Split(".");
        int seconds = int.Parse(times[0]); //Current system only works with seconds, consider rework

        if (endTime)
            seconds += trailTime;

        if (seconds < 10)
        {
            times[0] = "0" + seconds;
        }
        else
        {
            times[0] = seconds.ToString();
        }

        if (times.Length > 1) //If milliseconds available
            srtTime = "00:00:" + times[0] + "," + times[1];
        else //if no milliseconds found, e.g time variable is "1s"
            srtTime = "00:00:" + times[0] + ",000"; 

        return srtTime;
    }
}