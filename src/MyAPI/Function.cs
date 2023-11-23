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

public class Function
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

        var id = Guid.NewGuid();

        var putItemRequest = new PutItemRequest
        {
            TableName = _tableName,
            Item = new Dictionary<string, AttributeValue> {
                    {
                        "id",
                        new AttributeValue {
                        S = id.ToString(),
                    }
                    },
                    {
                        "description",
                        new AttributeValue {
                        S = request.Description
                        }
                    }
                }
        };

        await GetAnime();

        context.Logger.LogInformation("Hello world!!");

        await _dynamoDBClient.PutItemAsync(putItemRequest);

        var body = JsonSerializer.Serialize(new RegisterTaskResponse(id));

        var @event = new PublishRequest
        {
            TopicArn = _topicArn,
            Message = JsonSerializer.Serialize(new TaskRegistered(id, request.Description)),
        };

        await _snsClient.PublishAsync(@event);

        return new APIGatewayHttpApiV2ProxyResponse
        {
            Body = body,
            StatusCode = 200,
            Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
        };
    }

    private async Task GetAnime()
    {
        var response = await _httpClient.GetAsync(_url);

        var body = await response.Content.ReadAsStringAsync();
    }

    public record RegisterTaskRequest(string Description);

    public record RegisterTaskResponse(Guid Id);

    public record TaskRegistered(Guid Id, string Description);

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

        var body = JsonSerializer.Serialize(new GetTaskResponse(Guid.Parse(response.Item["id"].S), response.Item["description"].S));

        return new APIGatewayHttpApiV2ProxyResponse
        {
            Body = body,
            StatusCode = 200,
            Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
        };
    }

    public record GetTaskResponse(Guid Id, string Description);
}