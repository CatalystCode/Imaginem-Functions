#load "..\Common\FunctionHelper.csx"

using System;
using System.Configuration;
using Newtonsoft.Json;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;


private const string ClassifierName = "facematch";

class MatchedFace
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("image_url")]
    public string ImageUrl { get; set; }

    [JsonProperty("face_rectangle")]
    public FaceRectangle FaceRectangle { get;set;}

    [JsonProperty("confidence")]
    public double Confidence { get; set; }
}

public static void Run(string inputMsg, TraceWriter log)
{
    PipelineHelper.Process(FaceListMatchFunction, ClassifierName, inputMsg, log);
}

public static dynamic FaceListMatchFunction(dynamic inputJson, string imageUrl, TraceWriter log)
{
    string APIKey = ConfigurationManager.AppSettings["FACE_API_KEY"];
    CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["AzureWebJobsStorage"]);
    CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
    CloudTable table = tableClient.GetTableReference("persistedfaces");
    table.CreateIfNotExists();

    var similarFacesOutput = new List<SimilarPersistedFace>();

    var faces = inputJson.job_output.facedetection.faces.ToObject<Face[]>();

    var faceClient = new FaceServiceClient(APIKey);

    // get the face lists
    var faceLists = faceClient.ListFaceListsAsync().Result;

    foreach (var face in faces)
    {
        foreach (var faceList in faceLists)
        {
            var similarFaces = faceClient.FindSimilarAsync(face.FaceId, faceList.FaceListId).Result;

            if (similarFaces.Length > 0)
            {
                similarFacesOutput.AddRange(similarFaces);
            }
        }

        PersistFaceAsync(faceClient, faceLists, table, imageUrl, face.FaceRectangle).Wait();
    }

    var matches = new List<MatchedFace>();

    // retrieve the similar faces from table storage
    foreach (var output in similarFacesOutput)
    {
        dynamic result = table.Execute(TableOperation.Retrieve("faces", output.PersistedFaceId.ToString())).Result;

        if (result != null)
        {
            var matchedFace = new MatchedFace()
            {
                Id = result.RowKey,
                FaceRectangle = JsonConvert.DeserializeObject<FaceRectangle>(result.Properties["face_rectangle"].StringValue),
                ImageUrl = result.Properties["image_url"].StringValue,
                Confidence = output.Confidence
            };

            matches.Add(matchedFace);
        }
    }

    return new
    {
        matches = matches
    };
}

static async Task PersistFaceAsync(FaceServiceClient faceClient, FaceListMetadata[] faceLists, CloudTable table, string imageUrl, FaceRectangle faceRectangle)
{
    var faceListId = await GetFaceListIdAsync(faceClient, faceLists);
    var persistedFaceResult = await faceClient.AddFaceToFaceListAsync(faceListId.ToString(), imageUrl, imageUrl, faceRectangle);

    // store the persisted face to the persistedface table
    DynamicTableEntity logEntity = new DynamicTableEntity("faces", persistedFaceResult.PersistedFaceId.ToString());
    logEntity.Properties.Add("image_url", EntityProperty.GeneratePropertyForString(imageUrl));
    logEntity.Properties.Add("face_rectangle", EntityProperty.GeneratePropertyForString(JsonConvert.SerializeObject(faceRectangle)));
    table.Execute(TableOperation.InsertOrMerge(logEntity));
}

static async Task<string> GetFaceListIdAsync(FaceServiceClient faceClient, FaceListMetadata[] faceLists)
{
    string result = string.Empty;

    if(faceLists.Count() == 0)
    {
        result = await CreateFaceListAsync(faceClient);
    }
    else
    {
        foreach(var faceList in faceLists)
        {
            if (await FaceListHasRoom(faceClient, faceList.FaceListId))
            {
                result = (await faceClient.GetFaceListAsync(faceList.FaceListId)).FaceListId;
                break;
            }
        }
    }

    if (!string.IsNullOrEmpty(result))
    {
        return result;
    }
    else
    {
        return await CreateFaceListAsync(faceClient);
    }
}

static async Task<string> CreateFaceListAsync(FaceServiceClient faceClient)
{
    var id = Guid.NewGuid().ToString();
    await faceClient.CreateFaceListAsync(id, id, null);
    return id;
}

static async Task<bool> FaceListHasRoom(FaceServiceClient faceClient, string faceListId)
{
    var faceList = await faceClient.GetFaceListAsync(faceListId);

    return faceList.PersistedFaces.Length < 1000;
}