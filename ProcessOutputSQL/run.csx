#load "..\Common\FunctionHelper.csx"

using System;
using System.Data;
using System.Data.SqlClient;
using Newtonsoft.Json;
using System.Configuration;

private const string ClassifierName = "output";

private const string INSERT_QUERY = @"INSERT INTO pipeline_output
        (id,
        processing_pipeline,
        image_url,
        image_parameters,
        job_output,
        batch_id,
        timestamp
        )
        VALUES
            (@id,
            @processing_pipeline,
            @image_url,
            @image_parameters,
            @job_output,
            @batch_id,
            CURRENT_TIMESTAMP
            ); ";

public static void Run(string inputMsg, TraceWriter log)
{
    log.Info(inputMsg);
    PipelineHelper.Process(ProcessOutputFunction, ClassifierName, inputMsg, log);
}

public static dynamic ProcessOutputFunction(dynamic inputJson, string imageUrl, TraceWriter log)
{
    string sqlConnectionString = ConfigurationManager.AppSettings["SQL_CONNECTION_STRING"];
    using (var connection = new SqlConnection(sqlConnectionString))
    {
        connection.Open();

        using (var command = new SqlCommand())
        {
            command.Connection = connection;
            command.CommandType = CommandType.Text;
            command.CommandText = INSERT_QUERY;

            var parameter = new SqlParameter("@id", SqlDbType.NVarChar);
            parameter.Value = inputJson.job_definition.id.ToString();
            command.Parameters.Add(parameter);

            parameter = new SqlParameter("@processing_pipeline", SqlDbType.NText);
            parameter.Value = inputJson.job_definition.processing_pipeline.ToString();
            command.Parameters.Add(parameter);

            parameter = new SqlParameter("@image_url", SqlDbType.NVarChar);
            parameter.Value = inputJson.job_definition.input.image_url.ToString();
            command.Parameters.Add(parameter);

            parameter = new SqlParameter("@image_parameters", SqlDbType.NText);
            parameter.Value = inputJson.job_definition.input.image_parameters.ToString();
            command.Parameters.Add(parameter);

            parameter = new SqlParameter("@job_output", SqlDbType.NText);
            parameter.Value = inputJson.job_output.ToString();
            command.Parameters.Add(parameter);
                
            parameter = new SqlParameter("@batch_id", SqlDbType.NVarChar);
            parameter.Value = inputJson.job_definition.batch_id.ToString();
            command.Parameters.Add(parameter);

            command.ExecuteScalar();
        }
    }
    return new { done = true };
}
