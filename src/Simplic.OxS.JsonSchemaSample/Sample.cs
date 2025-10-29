// Example target method
public class Sample
{
    public record Address(string Street, string City, string? Zip);
    public enum Status { New, Active, Suspended }

    public (int id, string name) CreateUser(
        string email,
        string? displayName,
        int age,
        Address address,
        List<string> tags,
        Dictionary<string, double> scores,
        Status status = Status.New)
    {
        return (1, "ok");
    }
}