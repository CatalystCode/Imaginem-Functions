#r "System.Drawing"
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
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using System.Drawing;
using System.Drawing.Imaging;


private const string ClassifierName = "facecrop";

public static void Run(string inputMsg, TraceWriter log)
{
    PipelineHelper.Process(FaceCropFunction, ClassifierName, inputMsg, log);
}

public static dynamic FaceCropFunction(dynamic inputJson, string imageUrl, TraceWriter log)
{
    dynamic faces = inputJson.job_output.facedetection.faces;

    var webClient = new WebClient();
    byte[] imageBytes = webClient.DownloadData(imageUrl);
    var ms = new MemoryStream(imageBytes);
    var image = Bitmap.FromStream(ms);
    var faceUrls = CreateCroppedFaceImages(faces, image);
    return new { facesUrls = faceUrls };
}

private static string[] CreateCroppedFaceImages(dynamic faces, Image image)
{
    var urls = new List<string>();
    var bitmap = new Bitmap(image);
    foreach (var o in faces)
    {
        var face = o.ToObject<Face>();
        var border = 0;
        var rect = new Rectangle(
            new Point(
                face.FaceRectangle.Left - border,
                face.FaceRectangle.Top - border
                ),
            new Size(
                face.FaceRectangle.Width + border,
                face.FaceRectangle.Height + border
                )
            );
        var crop = bitmap.Clone(rect, PixelFormat.DontCare);
        var url = SaveToAzure(crop, $"{face.FaceId.ToString()}.png");
        urls.Add(url);
    }
    return urls.ToArray();
}

private static string SaveToAzure(Bitmap crop, string name)
{
    CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["AzureWebJobsStorage"]);
    CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
    CloudBlobContainer container = blobClient.GetContainerReference(ConfigurationManager.AppSettings["FACES_CONTAINER"]);
    container.CreateIfNotExists();
    container.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Container });
    CloudBlockBlob blockBlob = container.GetBlockBlobReference(name);
    var imageBytes = ImageToByte(crop);
    blockBlob.UploadFromByteArray(imageBytes, 0, imageBytes.Length);
    return blockBlob.Uri.ToString();
}

public static byte[] ImageToByte(Image img)
{
    ImageConverter converter = new ImageConverter();
    return (byte[])converter.ConvertTo(img, typeof(byte[]));
}