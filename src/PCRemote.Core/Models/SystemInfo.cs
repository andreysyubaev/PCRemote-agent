namespace PCRemote.Core.Models;

public class SystemInfo
{
    public string OperatingSystem { get; set; } = string.Empty;

    public string OSVersion { get; set; } = string.Empty;

    public string Processor { get; set; } = string.Empty;
    
    public string GraphicCard { get; set; } = string.Empty;

    public long TotalMemoryBytes { get; set; }

    public long FreeDiskSpaceBytes { get; set; }
}