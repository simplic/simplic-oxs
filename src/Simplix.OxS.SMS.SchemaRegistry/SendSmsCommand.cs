namespace Simplix.OxS.Sms.SchemaRegistry
{
    public interface SendSmsCommand
    {
        string PhoneNumber { get; }
        string TemplateId { get; }
        IDictionary<string, object> Parameter { get; }
    }
}