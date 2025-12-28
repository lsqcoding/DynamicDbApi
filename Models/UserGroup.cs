namespace DynamicDbApi.Models
{
    public class UserGroup
    {
        public int Id { get; set; }
        public string UserId { get; set; } = null!;
        public string GroupId { get; set; } = null!;
    }
}