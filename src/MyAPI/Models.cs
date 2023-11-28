using System.Text.Json.Serialization;

namespace MyAPI;

public partial class Function
{
    public class Payload
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
    }

    public record GetTaskResponse(Guid Id, string Description, string Worker);

    public record RegisterTaskRequest(string Description);

    public record RegisterTaskResponse(Guid Id);

    public record TaskRegistered(Guid Id, string Description, string Worker);
}