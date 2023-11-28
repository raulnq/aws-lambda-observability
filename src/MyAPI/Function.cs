using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using System.Text.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace MyAPI;

public partial class Function
{
    private readonly AmazonDynamoDBClient _dynamoDBClient;
    private readonly string _tableName;
    private readonly AmazonSimpleNotificationServiceClient _snsClient;
    private readonly string _topicArn;
    private readonly HttpClient _httpClient;
    private readonly string _url;
    public Function()
    {
        _dynamoDBClient = new AmazonDynamoDBClient();
        _tableName = Environment.GetEnvironmentVariable("TABLE_NAME")!;
        _snsClient = new AmazonSimpleNotificationServiceClient();
        _topicArn = Environment.GetEnvironmentVariable("TOPIC_ARN")!;
        _httpClient = new HttpClient();
        _url = Environment.GetEnvironmentVariable("URL")!;
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> Register(APIGatewayHttpApiV2ProxyRequest input, ILambdaContext context)
    {
        var request = JsonSerializer.Deserialize<RegisterTaskRequest>(input.Body)!;

        var taskId = Guid.NewGuid();

        var worker = await GetWorker();

        var putItemRequest = new PutItemRequest
        {
            TableName = _tableName,
            Item = new Dictionary<string, AttributeValue> {
                    {
                        "id",
                        new AttributeValue {
                        S = taskId.ToString(),
                    }
                    },
                    {
                        "description",
                        new AttributeValue {
                        S = request.Description
                        }
                    },
                    {
                        "worker",
                        new AttributeValue {
                        S = worker
                        }
                    }
                }
        };

        context.Logger.LogInformation($"Task {taskId} assigned to {worker}");

        await _dynamoDBClient.PutItemAsync(putItemRequest);

        var body = JsonSerializer.Serialize(new RegisterTaskResponse(taskId));

        var @event = new PublishRequest
        {
            TopicArn = _topicArn,
            Message = JsonSerializer.Serialize(new TaskRegistered(taskId, request.Description, worker)),
        };

        await _snsClient.PublishAsync(@event);

        return new APIGatewayHttpApiV2ProxyResponse
        {
            Body = body,
            StatusCode = 200,
            Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
        };
    }

    private async Task<string> GetWorker()
    {
        var response = await _httpClient.GetAsync(_url);

        if (response.StatusCode != System.Net.HttpStatusCode.OK)
        {
            return "Missing worker";
        }

        var content = await response.Content.ReadAsStringAsync();

        var payload = JsonSerializer.Deserialize<Payload>(content);

        return payload?.Name ?? "Missing worker";
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> Get(APIGatewayHttpApiV2ProxyRequest input, ILambdaContext context)
    {
        var id = input.PathParameters["id"];

        var request = new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>() { { "id", new AttributeValue { S = id.ToString() } } },
        };

        var response = await _dynamoDBClient.GetItemAsync(request);

        if (response.HttpStatusCode != System.Net.HttpStatusCode.OK)
        {
            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 404
            };
        }

        var body = JsonSerializer.Serialize(new GetTaskResponse(Guid.Parse(response.Item["id"].S), response.Item["description"].S, response.Item["worker"].S));

        return new APIGatewayHttpApiV2ProxyResponse
        {
            Body = body,
            StatusCode = 200,
            Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
        };
    }

}