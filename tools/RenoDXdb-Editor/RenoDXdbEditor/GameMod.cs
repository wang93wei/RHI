using System.ComponentModel;
using System.Text.Json.Serialization;

namespace RenoDXdbEditor;

public class GameMod : INotifyPropertyChanged
{
    private string _name = "";
    private string _status = "Done";
    private string _author = "";
    private string? _snapshotUrl;
    private string? _snapshotUrl32;
    private string? _nexusUrl;
    private string? _discordUrl;
    private string? _discussionUrl;
    private string? _notes;

    [JsonPropertyName("name")]
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(nameof(Name)); OnPropertyChanged(nameof(DisplayName)); }
    }

    [JsonPropertyName("status")]
    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(nameof(Status)); OnPropertyChanged(nameof(DisplayName)); }
    }

    [JsonPropertyName("author")]
    public string Author
    {
        get => _author;
        set { _author = value; OnPropertyChanged(nameof(Author)); }
    }

    [JsonPropertyName("snapshotUrl")]
    public string? SnapshotUrl
    {
        get => _snapshotUrl;
        set { _snapshotUrl = value; OnPropertyChanged(nameof(SnapshotUrl)); }
    }

    [JsonPropertyName("snapshotUrl32")]
    public string? SnapshotUrl32
    {
        get => _snapshotUrl32;
        set { _snapshotUrl32 = value; OnPropertyChanged(nameof(SnapshotUrl32)); }
    }

    [JsonPropertyName("nexusUrl")]
    public string? NexusUrl
    {
        get => _nexusUrl;
        set { _nexusUrl = value; OnPropertyChanged(nameof(NexusUrl)); }
    }

    [JsonPropertyName("discordUrl")]
    public string? DiscordUrl
    {
        get => _discordUrl;
        set { _discordUrl = value; OnPropertyChanged(nameof(DiscordUrl)); }
    }

    [JsonPropertyName("discussionUrl")]
    public string? DiscussionUrl
    {
        get => _discussionUrl;
        set { _discussionUrl = value; OnPropertyChanged(nameof(DiscussionUrl)); }
    }

    [JsonPropertyName("notes")]
    public string? Notes
    {
        get => _notes;
        set { _notes = value; OnPropertyChanged(nameof(Notes)); }
    }

    [JsonIgnore]
    public string DisplayName => $"{Name}  [{Status}]";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
