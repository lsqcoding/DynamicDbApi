using DynamicDbApi.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DynamicDbApi.Services
{
    public interface IFileStorageService
    {
        Task<StoredFile> UploadFileAsync(Stream fileStream, string fileName, string contentType, long fileSize, string? uploaderId = null);
        Task<FileDownloadResult?> DownloadFileAsync(Guid fileId);
        Task<FileDownloadResult?> DownloadFileByShareLinkAsync(Guid linkId, string? password = null);
        Task DeleteFileAsync(Guid fileId);
        Task UpdateFileInfoAsync(Guid fileId, string displayName);
        Task<FileShareLink> CreateShareLinkAsync(Guid fileId, DateTime? expireTime, string? password, string? creatorId = null);
        Task DisableShareLinkAsync(Guid linkId);
        Task<IEnumerable<StoredFile>> GetUserFilesAsync(string userId);
        Task<IEnumerable<FileShareLink>> GetFileShareLinksAsync(Guid fileId);
    }

    public class FileDownloadResult
    {
        public required Stream FileStream { get; set; }
        public required string FileName { get; set; }
        public required string ContentType { get; set; }
    }
}