using Azure.Storage.Blobs;

namespace TegritTriviaFullStack
{
    public class BlobStorage
    {
        public async Task UploadFileAsync()
        {
            var connectionString = new Uri("DefaultEndpointsProtocol=https;AccountName=tegrittrivia82c2;AccountKey=n7wF6jcPMk/Cc0slz12jKulFjnqiDx1SnlaK9JQsGnQVRUX2eu8tfXkdOL/ybj3ixaNA7+3b6DZh+AStz8+oIQ==;EndpointSuffix=core.windows.net");
            var path = @"C:\Users\gokul.sankar\source\repos\TegritTrivia\TegritTriviaFullStack\TegritTriviaFullStack\valid-wordle-words.txt";

            BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
        }
    }
}
