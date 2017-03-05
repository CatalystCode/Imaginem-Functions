#r "System.IO"
#r "System.Runtime"
#r "System.Threading.Tasks"
#r "System.Configuration"
#r "Newtonsoft.Json"
#r "Microsoft.WindowsAzure.Storage"

using System;
using System.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Blob;

private class LinkedContent
{
    public string linked_content;
}
public class PipelineHelper
{
    private const int MAX_EMBEDDED_CONTENT_SIZE = 16 * 1024;
    private static System.Text.Encoding encoding = System.Text.Encoding.UTF8;
    const string LARGE_MESSAGE_CONTAINER = "largemessages";

    public delegate dynamic ProcessFunc(dynamic inputJson, string imageUrl, TraceWriter log);
    public static void Process(ProcessFunc func, string processorName, string inputMsg, TraceWriter log)
    {
        Exception exception = null;
        CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["AzureWebJobsStorage"]);

        dynamic inputJson = JsonConvert.DeserializeObject(inputMsg);
        dynamic expandedInputJson = JsonConvert.DeserializeObject(inputMsg);
        expandedInputJson = LoadInputJson(expandedInputJson, storageAccount);
        dynamic jobDefinition = expandedInputJson.job_definition;
        string imageUrl = jobDefinition.input.image_url;
        dynamic jobOutput = "";

        try
        {
            jobOutput = func(expandedInputJson, imageUrl, log);
        }
        catch (Exception ex)
        {
            exception = ex;
        }
        PipelineHelper.Commit(inputJson, processorName, imageUrl, jobOutput, exception, log);
    }
    private static void Commit(dynamic inputJson, string jobName, string imageUrl, dynamic jobOutput, Exception exception, TraceWriter log)
    {
        log.Info($"start commit {jobName}");

        CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["AzureWebJobsStorage"]);
        CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
        CloudTable table = tableClient.GetTableReference("pipelinelogs");
        table.CreateIfNotExists();

        DynamicTableEntity logEntity = new DynamicTableEntity(inputJson.job_definition.batch_id.ToString(), inputJson.job_definition.id.ToString());
        logEntity.Properties.Add("image_url", EntityProperty.GeneratePropertyForString(imageUrl));
        try
        {
            dynamic jobDefinition = inputJson.job_definition;
            int processingStep = (int)jobDefinition.processing_step;
            int nextProcessingStep = processingStep + 1;
            string inputQueue = jobDefinition.processing_pipeline[processingStep];

            jobOutput = GetContentToBeStored(jobOutput, string.Format("{0}/{1}", jobDefinition.id, jobName),storageAccount);

            if (exception == null)
            {
                // adding the job data to the output json
                ((JObject)inputJson.job_output).Add(jobName, JObject.FromObject(jobOutput));
                logEntity.Properties.Add(inputQueue, EntityProperty.GeneratePropertyForString(string.Format("step {0}: success", processingStep)));
                logEntity.Properties.Add(jobName + "_output", EntityProperty.GeneratePropertyForString(JsonConvert.SerializeObject(jobOutput)));
            }
            else
            {
                // writting exception details to table storage
                logEntity.Properties.Add(inputQueue, EntityProperty.GeneratePropertyForString(string.Format("step {0}: failed", processingStep)));
                logEntity.Properties.Add(jobName + "_exception", EntityProperty.GeneratePropertyForString(
                    string.Format("{0} - {1}", exception.Message, exception.InnerException)));
            }
            //there is a next step in the pipeline
            if (jobDefinition.processing_pipeline.Count > nextProcessingStep)
            {
                string outputQueue = jobDefinition.processing_pipeline[nextProcessingStep];
                logEntity.Properties.Add(outputQueue, EntityProperty.GeneratePropertyForString(string.Format("step {0}: processing", nextProcessingStep)));
                inputJson.job_definition.processing_step = nextProcessingStep;

                log.Info($"next processing step {outputQueue}");
                CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
                CloudQueue queue = queueClient.GetQueueReference(outputQueue);
                queue.CreateIfNotExists();
                string outputJson = JsonConvert.SerializeObject(inputJson);
                CloudQueueMessage message = new CloudQueueMessage(outputJson);
                queue.AddMessage(message);
                log.Info($"message added to queue {outputQueue}: {outputJson}");
            }
            else
            {
                logEntity.Properties.Add("job_output", EntityProperty.GeneratePropertyForString(JsonConvert.SerializeObject(inputJson)));
            }
            log.Info($"succesfully committed {jobName}");
        }
        catch (Exception ex)
        {
            log.Error($"Exception in {jobName}: {ex.Message} - {ex.InnerException}");
        }
        table.Execute(TableOperation.InsertOrMerge(logEntity));
    }

    private static dynamic GetContentToBeStored(dynamic json, string id, CloudStorageAccount storageAccount)
    {
        string jsonString = JsonConvert.SerializeObject(json);
        var bytesCount = System.Text.UnicodeEncoding.UTF32.GetByteCount(jsonString);

        if (bytesCount > MAX_EMBEDDED_CONTENT_SIZE)
        {
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(LARGE_MESSAGE_CONTAINER);
            container.CreateIfNotExists();
            CloudBlockBlob contentBlob = container.GetBlockBlobReference(id+".json");
            contentBlob.UploadText(jsonString, encoding);
            return  new LinkedContent {
                linked_content = contentBlob.Uri.ToString()
            };
        }
        else
        {
            return json;
        }
    }

    private static dynamic LoadInputJson(dynamic inputJson, CloudStorageAccount storageAccount)
    {
        CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
        CloudBlobContainer container = blobClient.GetContainerReference(LARGE_MESSAGE_CONTAINER);

        List<dynamic> outputs = new List<dynamic>();
        // load each node (in the case they contain large messages) 
        foreach (dynamic output in inputJson.job_output.Children())
        {
            try
            {
                dynamic largeOutput = LoadContent(output.First.ToObject<LinkedContent>(), container);
                output.First.Replace(JObject.FromObject(largeOutput));
            }
            catch (Exception ex)
            { }
        }
        return inputJson != null ? inputJson : new { };
    }

    private static dynamic LoadContent(LinkedContent linkedContent, CloudBlobContainer container)
    {
        var contentBlob = container.ServiceClient.GetBlobReferenceFromServer(new Uri(linkedContent.linked_content));
        using (var memoryStream = new MemoryStream())
        {
            contentBlob.DownloadToStream(memoryStream);
            return JsonConvert.DeserializeObject(encoding.GetString(memoryStream.ToArray()));
        }
    }
}

