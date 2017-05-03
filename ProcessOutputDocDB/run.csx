#load "..\Common\FunctionHelper.csx"

using System;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Newtonsoft.Json;
using System.Configuration;

private const string ClassifierName = "output";

public static void Run(string inputMsg, TraceWriter log)
{
    log.Info(inputMsg);
    PipelineHelper.Process(ProcessOutputFunction, ClassifierName, inputMsg, log);
}

public static async Task<dynamic> ProcessOutputFunction(dynamic inputJson, string imageUrl, TraceWriter log)
{
    string endpoint = ConfigurationManager.AppSettings["DOCDB_ENDPOINT_STRING"];
    string key = ConfigurationManager.AppSettings["DOCDB_AUTH_KEY"];
    string link = "dbs/imaginem/colls/process_output";

    try
    {
        using (var docClient = new DocumentClient(new Uri(endpoint), key))
        {
            var doc = await docClient.CreateDocumentAsync(link, inputJson);
            log.Info($"Document created. Id: { doc.Id }");
        }
    }
    catch(Exception e)
    {
        log.Error(e.Message);
        log.Error(e.StackTrace);
        return e.Message;
    }

    return new { done = true };
}