#load "..\Common\FunctionHelper.csx"
#load "..\Common\ComputerVisionFunctions.csx"

using System.Net.Http.Headers;
using System.Text;
using System.Net.Http;
using System.Web;
using System.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

private const string ClassifierName = "ocr";

public static void Run(string inputMsg, TraceWriter log)
{
    PipelineHelper.Process(OcrFunction, ClassifierName, inputMsg, log);
}
public static dynamic OcrFunction(dynamic inputJson, string imageUrl, TraceWriter log)
{
    var response = ComputerVisionFunctions.ImageOCRAsync(imageUrl, log).Result;

    // Retrieve all identified words and concat them into a string (allTextConcat)
    JObject obj = JObject.Parse(response);
    var texts = obj.SelectTokens("..text");
    var allTextConcat = String.Join(" ", texts.Values<string>());

    // Add allTextConcat to the OCR initial response
    return new {
        ocr = JsonConvert.DeserializeObject(response),
        OCR_fullText = allTextConcat
    };
}
