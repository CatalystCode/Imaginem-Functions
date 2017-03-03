#load "..\Common\FunctionHelper.csx"

using System;
using System.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;

// your classifier's name
private const string ClassifierName = "samplefunction";

public static void Run(string inputMsg, TraceWriter log)
{
    log.Verbose($"Process {inputMsg}");
    PipelineHelper.Process(SampleFunction, ClassifierName, inputMsg, log);
}
public static dynamic SampleFunction(dynamic inputJson, string imageUrl, TraceWriter log)
{
    // TODO: do your processing here and return the results

    return new {
        stringOutput = "your output string value",
        intOutput = 10 
    };
}




