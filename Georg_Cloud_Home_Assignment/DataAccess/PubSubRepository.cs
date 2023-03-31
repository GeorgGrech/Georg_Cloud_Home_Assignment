using Common.Models;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Georg_Cloud_Home_Assignment.DataAccess
{
    public class PubSubRepository
    {
        TopicName topicName;

        public PubSubRepository(string project)
        {
            //get the queue (so we can add messages to it)
            topicName = TopicName.FromProjectTopic(project, "movie-queue");

            if (topicName == null) //if it is not created...
            {
                var p = PublisherServiceApiClient.Create();
                var t = p.CreateTopic("movie-queue");
                topicName = t.TopicName;
            }
        }

        public async void PushMessage(Movie m)
        {
            PublisherClient publisher = await PublisherClient.CreateAsync(topicName);
            var movie = JsonConvert.SerializeObject(m); //converts from object >>>> a json string
            var pubsubMessage = new PubsubMessage
            {
                Data = ByteString.CopyFromUtf8(movie),

                Attributes = //not compulsory
                {
                    { "priorty", "low" }
                }
            };
            string message = await publisher.PublishAsync(pubsubMessage); //the message (reservation as json string) will be published onto the queue

        }
    }
}
