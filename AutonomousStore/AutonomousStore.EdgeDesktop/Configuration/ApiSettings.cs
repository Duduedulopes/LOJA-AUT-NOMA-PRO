namespace AutonomousStore.EdgeDesktop.Configuration;

public class ApiSettings
{
    public const string SectionName = "Api";

    public string BaseUrl { get; set; } = "http://localhost:5071";
}
