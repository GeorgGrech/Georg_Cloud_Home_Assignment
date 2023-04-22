using Google.Cloud.Functions.Framework;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Google.Cloud.Firestore;
using Google.Cloud.Firestore.V1;
using Microsoft.Extensions.Logging;
using Google.Cloud.Speech.V1;
using Google.Cloud.Storage.V1;
using System.IO;

namespace ToSrtFunction;

public class Function : IHttpFunction
{
    //Logging
     ILogger<Function> _logger;
  public Function(ILogger<Function> logger) =>
    _logger = logger;

    
    /// <summary>
    /// Logic for your function goes here.
    /// </summary>
    /// <param name="context">The HTTP context, containing the request and the response.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task HandleAsync(HttpContext context)
    {
        //await context.Response.WriteAsync("Hello, Functions Framework.");

        //System.Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", "georg-cloud-home-assignment-37ae86e05b4c.json");

        string projectId = "georg-cloud-home-assignment";
        FirestoreDb db = FirestoreDb.Create(projectId);

        HttpRequest request = context.Request;
        string id = request.Query["id"];
        Movie m = await GetMovie(id,db);


        var storage = StorageClient.Create();
        string bucketName = "georg_movie_app_bucket";


        Stream transcriptionStream = Transcribe(m.FlacFileName, bucketName, storage); 
        UploadTranscription(transcriptionStream, bucketName, storage, m, db);

        //await context.Response.WriteAsync($"Movie requested has these details: Name: {m.Name}, Audio File: {m.FlacFileName}");

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
