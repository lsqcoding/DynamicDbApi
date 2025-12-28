using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DynamicDbApi.Models
{
    public class StoredFile
    {
        [Key]
        public Guid Id { get; set; }
        
        [Required]
        [MaxLength(255)]
        public required string OriginalName { get; set; }
        
        [MaxLength(255)]
        public string? DisplayName { get; set; }
        
        [Required]
        [MaxLength(255)]
        public required string StorageName { get; set; }
        
        [Required]
        [MaxLength(50)]
        public required string ContentType { get; set; }
        
        public long Size { get; set; }
        
        [Required]
        [MaxLength(255)]
        public required string StoragePath { get; set; }
        
        public DateTime UploadTime { get; set; }
        
        [MaxLength(255)]
        public string? UploaderId { get; set; }
    }

    public class FileShareLink
    {
        [Key]
        public Guid Id { get; set; }
        
        [Required]
        public Guid FileId { get; set; }
        
        [ForeignKey("FileId")]
        public StoredFile? File { get; set; }
        
        [MaxLength(255)]
        public string? PasswordHash { get; set; }
        
        public DateTime ExpireTime { get; set; }
        
        public DateTime CreateTime { get; set; }
        
        [MaxLength(255)]
        public string? CreatorId { get; set; }
        
        public bool IsActive { get; set; }
    }

    public class FileTypePolicy
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [MaxLength(50)]
        public required string FileType { get; set; }
        
        [Required]
        public bool IsBlacklisted { get; set; }
        
        [MaxLength(255)]
        public string? Description { get; set; }
    }
}