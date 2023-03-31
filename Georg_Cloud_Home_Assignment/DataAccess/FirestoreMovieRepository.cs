using Common.Models;
using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Georg_Cloud_Home_Assignment.DataAccess
{
    public class FirestoreMovieRepository
    {
        FirestoreDb db;
        public FirestoreMovieRepository(string project)
        {
            db = FirestoreDb.Create(project);
        }

        public void AddMovie(Movie m)
        {
            DocumentReference docRef = db.Collection("movies").Document();
            docRef.SetAsync(m);
        }

        public async Task<List<Movie>> GetMovies()
        {
            List<Movie> movies = new List<Movie>();
            Query allMoviesQuery = db.Collection("movies");
            QuerySnapshot allMoviesQuerySnapshot = await allMoviesQuery.GetSnapshotAsync();
            foreach (DocumentSnapshot documentSnapshot in allMoviesQuerySnapshot.Documents)
            {
                Movie m = documentSnapshot.ConvertTo<Movie>();
                movies.Add(m);
            }
            return movies;
        }

        public async Task<Movie> GetMovie(string id)
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

        public async Task DeleteMovie(string movieId)
        {
            //List<Movie> movies = new List<Movie>();
            Query allMoviesQuery = db.Collection("movies").WhereEqualTo("Id", movieId);
            QuerySnapshot allMoviesQuerySnapshot = await allMoviesQuery.GetSnapshotAsync();

            string id = allMoviesQuerySnapshot.Documents[0].Id;
            DocumentReference moviesRef = db.Collection("movies").Document(id);
            await moviesRef.DeleteAsync();
        }

    }
}
