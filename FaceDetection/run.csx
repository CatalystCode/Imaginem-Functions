#load "..\Common\FunctionHelper.csx"

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web;
using System.Collections.Generic;
using System.Configuration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;

private const string ClassifierName = "facedetection";

public static void Run(string inputMsg, TraceWriter log)
{
    PipelineHelper.Process(FaceDetectionFunction, ClassifierName, inputMsg, log);
}

public static dynamic FaceDetectionFunction(dynamic inputJson, string imageUrl, TraceWriter log)
{
    FaceServiceClient faceClient = new FaceServiceClient(ConfigurationManager.AppSettings["FACE_API_KEY"]);

    var attributes = new List<FaceAttributeType> { FaceAttributeType.Age, FaceAttributeType.Gender, FaceAttributeType.FacialHair, FaceAttributeType.Smile, FaceAttributeType.HeadPose, FaceAttributeType.Glasses };
    Face[] faces = faceClient.DetectAsync(imageUrl, true, true, attributes).Result;

    return new { faces = faces };
}