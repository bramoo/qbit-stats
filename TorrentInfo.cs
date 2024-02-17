using System.Text.Json.Serialization;

public class TorrentInfo {
    [JsonPropertyName("hash")]
    public required string Hash {get; set;}

    [JsonPropertyName("name")]
    public required string Name {get; set;}

    [JsonPropertyName("tracker")]
    public required string Tracker {get; set;}

    [JsonPropertyName("uploaded")]
    public long Uploaded {get; set;}
}