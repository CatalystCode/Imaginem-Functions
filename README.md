# Imaginem-Functions
Azure functions for the configurable image recognition and classification pipeline

Details on how to [develop for Azure function](https://docs.microsoft.com/en-us/azure/azure-functions/functions-reference-csharp) and the [Azure Function tools for Visual Studio](https://blogs.msdn.microsoft.com/webdev/2016/12/01/visual-studio-tools-for-azure-functions/)


# Adding a function 
Each function is added to its own folder and contains the following artifacts:

```
function.json   // your function binding
project.json    // your project dependencies
run.csx         // your code
test.json       // your test message
```
## Implementing the function.json

Here an example implementation for `samplefunction`:

```csharp
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
``` 
## Declare the dependencies
As the last step, you need to declare the functions dependencies in `dependencies.json`. In the example below, `facematch` depends on `faceprint` and `faceprint` depends on `facedetection`. 

```json
{
  "facecrop": ["facedetection"],
  "facedetection": [],
  "facematch": ["faceprint"],
  "faceprint": ["facedetection"],
  "generalclassification": [],
  "ocr": []
}
```

Therefore the pipeline definition for `facematch` would look like `"facedetection,faceprint,facematch"`

## Create the test message
Each function should contain a simple test message stored in `test.json`:

```json
{
  "job_definition": {
    "batch_id": "mybatchid",
    "id": "myjobid",
    "input": {
      "image_url": "your image url",
      "image_classifiers": [ "classifier1", "classifier2" ]
    },
    "processing_pipeline": [ "your_input_queue_name", "your_output_queue_name" ],
    "processing_step": 0

  },
  "job_output" : {
    "sample_job1" : {
      "output": "sample_job1 output"
    }
  }
}
```
Ensure you add all needed input data (your dependencies) as part of the `job_output` and set `your_input_queue_name` and `your_input_queue_name` for the `processing_pipeline` property.


## Test the function locally
To test your functions locally, you either run them in Visual Studio or you use `azure-functions-cli` directly:

```
npm i -g azure-functions-cli
```

To run your function, just execute 

```
func run YourFunctionName
```

to run the tests, run the Imaginem-Cli. The following triggers the `SampleFunction` by adding its `test.json` to the `sample` queue. 
```
.\Imaginem-Cli.exe test SampleFunction sample
```

