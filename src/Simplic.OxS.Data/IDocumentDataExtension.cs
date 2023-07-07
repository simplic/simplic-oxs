namespace Simplic.OxS.Data
{
    public interface IDocumentDataExtension
    {
        DateTime CreateDateTime { get; set; }
        Guid? CreateUserId { get; set; }
        string CreateUserName { get; set; }

        DateTime UpdateDateTime { get; set; }
        Guid? UpdateUserId { get; set; }
        string UpdateUserName { get; set; }
    }
}
