using Google.Cloud.Firestore;
using Google.Cloud.Firestore.V1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Georg_Cloud_Home_Assignment.Models
{

    [FirestoreData] //So that we don't need to store this data formatted as json in Firestore
    public class Movie
    {
        [FirestoreProperty]
        public string Id { get; set; } //Id of movie (Auto-generated)

        [FirestoreProperty]
        public string Name { get; set; } //Name of movie (No functional use)

        [FirestoreProperty]
        public string Link { get; set; } //Link to bucket
    }

}
