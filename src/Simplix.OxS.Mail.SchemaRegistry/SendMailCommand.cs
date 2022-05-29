namespace Simplix.OxS.Mail.SchemaRegistry
{
    public interface SendMailCommand
    {
        string MailAddress { get; }
        long TemplateId { get; }
        IDictionary<string, object> Parameter { get; }
    }
}