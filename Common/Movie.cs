﻿using Google.Cloud.Firestore;
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

        [FirestoreProperty]
        public string FlacFileName { get; set; } //name of flac file to be found in bucket

        [FirestoreProperty]
        public string LinkToTranscription { get; set; } //Link to bucket

        [FirestoreProperty]
        public string UploaderName { get; set; } //Link to bucket

        [FirestoreProperty]
        public Timestamp UploadTime { get; set; }

        [FirestoreProperty]
        public List<Timestamp> DownloadTimes { get; set; }

        [FirestoreProperty]
        public string Status { get; set; }

    }

}
