namespace IptvClient.ViewModels;

public sealed record ChannelGroup(string Name, int Count)
{
    public string Label => $"{Name} ({Count})";
}
