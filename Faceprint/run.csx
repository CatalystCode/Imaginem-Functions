#load "..\Common\FunctionHelper.csx"

using System;
using Newtonsoft.Json;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using System.Configuration;

private const string ClassifierName = "faceprint";

public static void Run(string inputMsg, TraceWriter log)
{
    PipelineHelper.Process(FacePrintFunction, ClassifierName, inputMsg, log);
}
public static dynamic FacePrintFunction(dynamic inputJson, string imageUrl, TraceWriter log)
{
    FaceServiceClient faceClient = new FaceServiceClient(ConfigurationManager.AppSettings["FACE_API_KEY"]);
    Face[] faces = faceClient.DetectAsync(imageUrl, true, true, null).Result;

    return new
    {
        faces = faces
    };
}
