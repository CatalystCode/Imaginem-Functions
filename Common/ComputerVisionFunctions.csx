#r "System.IO"
#r "System.Net.Http"
#r "System.Web"
#r "System.Runtime"
#r "System.Threading.Tasks"
#r "Newtonsoft.Json"

using System.Net.Http.Headers;
using System.Text;
using System.Net.Http;
using System.Web;
using System.Runtime;
using Newtonsoft.Json;
using System.Configuration;


private const string _features = "Adult, Categories, Tags, Description, Faces, Color, ImageType";

public class ComputerVisionFunctions
{
    public static async Task<string> AnalyzeImageAsync(string imageUrl, TraceWriter log)
    {
        return await PostImageToApiAsync(imageUrl, "analyze", log);
    }

    public static async Task<string> ImageOCRAsync(string imageUrl, TraceWriter log)
    {
        return await PostImageToApiAsync(imageUrl, "ocr", log);
    }

    private static async Task<string> PostImageToApiAsync(string imageUrl, string api, TraceWriter log)
    {
        try
        {
            string APIKey = ConfigurationManager.AppSettings["VISION_API_KEY"];

            var client = new HttpClient();
            var queryString = HttpUtility.ParseQueryString(string.Empty);

            // Request headers
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", APIKey);

            // Request parameters
            queryString["visualFeatures"] = _features;
            queryString["language"] = "en";
            var uri = "https://westus.api.cognitive.microsoft.com/vision/v1.0/" + api + "?" + queryString;

            var contentStr = "{\"url\":\"" + imageUrl + "\"}";

            var content = new StringContent(contentStr);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            var response = await client.PostAsync(uri, content);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync(); ;
            }
            else
            {
                return response.ReasonPhrase;
            }
        }
        catch (Exception e)
        {
            log.Info(e.Message);
            log.Info(e.StackTrace);
            return e.Message;
        }
    }
}

