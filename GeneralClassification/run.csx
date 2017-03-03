#load "..\Common\FunctionHelper.csx"
#load "..\Common\ComputerVisionFunctions.csx"

using System.Net.Http.Headers;
using System.Text;
using System.Net.Http;
using System.Web;
using System.Runtime;
using Newtonsoft.Json;

private const string ClassifierName = "generalclassification";

public static void Run(string inputMsg, TraceWriter log)
{
    log.Info(inputMsg);
    PipelineHelper.Process(GeneralClassificationFunction, ClassifierName, inputMsg, log);
}

public static dynamic GeneralClassificationFunction(dynamic inputJson, string imageUrl, TraceWriter log)
{
    var parameters = inputJson.job_definition.image_parameters;
    var response = ComputerVisionFunctions.AnalyzeImageAsync(imageUrl, log).Result;
    return JsonConvert.DeserializeObject(response);
}
