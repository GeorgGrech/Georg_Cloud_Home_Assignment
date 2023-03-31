using Google.Cloud.Firestore;
using Google.Cloud.Firestore.V1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Common.Models
{

    [FirestoreData] //So that we don't need to store this data formatted as json in Firestore
    public class Movie
    {
        [FirestoreProperty]
        public string Id { get; set; } //Id of movie (Set to Auto-generated)

        [FirestoreProperty]
        public string Name { get; set; } //Name of movie (No functional use)

        [FirestoreProperty]
        public string LinkToMovie { get; set; } //Link to bucket

        [FirestoreProperty]
        public string LinkToThumbnail { get; set; } //Link to bucket
    }

}
