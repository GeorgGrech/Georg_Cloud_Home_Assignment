using Google.Cloud.PubSub.V1;
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
    }
}
