#load "..\Common\FunctionHelper.csx"

using System;
using Newtonsoft.Json;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using System.Configuration;

private const string ClassifierName = "faceprint";

public static void Run(string inputMsg, TraceWriter log)
{
    PipelineHelper.Process(FaceMatchFunction, ClassifierName, inputMsg, log);
}
public static dynamic FaceMatchFunction(dynamic inputJson, string imageUrl, TraceWriter log)
{
    //get the tags present in the message
    var tags = inputJson.job_output.general_classification.tags;
    List<String> tagNames = new List<string>();
    foreach (var tag in tags)
    {
        tagNames.Add(tag.name?.ToString());
    }

    FaceServiceClient faceClient = new FaceServiceClient(ConfigurationManager.AppSettings["FACE_API_KEY"]);
    List<string> groupIds = getGroups(faceClient, tagNames).Result;
    // get the faces in the pictures
    Face[] faces = faceClient.DetectAsync(imageUrl).Result;
    // list of the people that match all the candidates in the picture
    List<Person> matches = new List<Person>();
    // iterate on the faces and check if there is a match. 
    foreach (var face in faces)
    {
        foreach (string groupId in groupIds)
        {
            //Select the person that best match the picture
            Candidate[] candidates = null;
            try
            {
                IdentifyResult[] result = faceClient.IdentifyAsync(groupId, new Guid[] { face.FaceId }).Result;
                candidates = result?.First()?.Candidates;
            }
            catch
            {
                // do nothing (candidates is already set to null as we want it too in that case)
            }

            if (null != candidates && candidates.Count() > 0)
            {
                matches.Add(faceClient.GetPersonAsync(groupId, candidates.First().PersonId).Result);
                faceClient.AddPersonFaceAsync(groupId, candidates.First().PersonId, imageUrl, imageUrl, face.FaceRectangle).Wait();
            }
            else //the person does not exist
            {
                var newPerson = faceClient.CreatePersonAsync(groupId, imageUrl).Result;
                faceClient.AddPersonFaceAsync(groupId, newPerson.PersonId, imageUrl, imageUrl, face.FaceRectangle).Wait();
                //train the group
                faceClient.TrainPersonGroupAsync(groupId);
            }
        }
    }
    return new
    {
        matches = matches
    };
}


/// <summary>
/// 
/// </summary>
/// <param name="faceClient"></param>
/// <param name="tags"></param>
/// <returns></returns>
static async Task<List<string>> getGroups(FaceServiceClient faceClient, List<String> tags=null)
{
    PersonGroup[] groups = await faceClient.ListPersonGroupsAsync();

    if (tags.Count() == 0)
    {
        tags.Add("__default__");
    }

    List<string> groupIds = new List<string>();

    //iterate on each tags present in the picture to add it to the different groups
    foreach (String tag in tags)
    {
        string groupNamePrefix = tag + "__";
        List<string> tagGroupIds = new List<string>();
        bool hasRoom = false;

        //search for all the groups that could contain our tag
        foreach (PersonGroup group in groups)
        {
            if (group.PersonGroupId.StartsWith(groupNamePrefix))
            {
                var personsInGroup = await faceClient.GetPersonsAsync(group.PersonGroupId);
                //groups have a limit of 1000, if we are 1000 or more, we need to create a second group
                hasRoom |= personsInGroup.Length < 1000;
                tagGroupIds.Add(group.PersonGroupId);
                //group must be trained when created
                await faceClient.TrainPersonGroupAsync(group.PersonGroupId);
            }
        }
        // if all the groups are full we need to create a new one to store the picture here (will be done in the main func)
        if (!hasRoom) 
        {
            string newGroupId = groupNamePrefix + (tagGroupIds.Count() + 1);
            await faceClient.CreatePersonGroupAsync(newGroupId, tag);
            tagGroupIds.Add(newGroupId);
            //group must be trained when created
            await faceClient.TrainPersonGroupAsync(newGroupId);
        }
        

        groupIds.AddRange(tagGroupIds);
    }

    return groupIds;
}
