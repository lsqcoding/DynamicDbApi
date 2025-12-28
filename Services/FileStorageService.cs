using DynamicDbApi.Models;
using DynamicDbApi.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DynamicDbApi.Services
{
    public class FileStorageService : IFileStorageService
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<FileStorageService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _storagePath;
        private readonly long _maxFileSize;
        private readonly List<string> _allowedExtensions;
        private readonly List<string> _disallowedExtensions;

        public FileStorageService(AppDbContext context, IWebHostEnvironment environment, ILogger<FileStorageService> logger, IConfiguration configuration)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
            _configuration = configuration;
            
            // 从配置中获取文件存储设置
            var fileStorageConfig = _configuration.GetSection("FileStorage");
            _storagePath = fileStorageConfig.GetValue<string>("BasePath") ?? Path.Combine(_environment.ContentRootPath, "wwwroot/uploads");
            _maxFileSize = fileStorageConfig.GetValue<long>("MaxFileSizeMB", 50) * 1024 * 1024; // 默认50MB
            _allowedExtensions = fileStorageConfig.GetSection("AllowedExtensions").Get<List<string>>() ?? new List<string>();
            _disallowedExtensions = fileStorageConfig.GetSection("DisallowedExtensions").Get<List<string>>() ?? new List<string>();
            
            // 确保存储目录存在
            if (!Directory.Exists(_storagePath))
            {
                Directory.CreateDirectory(_storagePath);
                _logger.LogInformation($"创建文件存储目录: {_storagePath}");
            }
            
            _logger.LogInformation($"文件存储配置 - 路径: {_storagePath}, 最大文件大小: {_maxFileSize / (1024 * 1024)}MB");
        }

        public async Task<StoredFile> UploadFileAsync(Stream fileStream, string fileName, string contentType, long fileSize, string? uploaderId = null)
        {
            try
            {
                // 验证文件类型
                var fileExtension = Path.GetExtension(fileName).ToLowerInvariant();
                
                // 首先检查数据库中的文件类型策略
                var fileTypePolicy = _context.Db.Queryable<FileTypePolicy>()
                    .Where(p => p.FileType == fileExtension || p.FileType == contentType)
                    .First();
                
                if (fileTypePolicy != null && fileTypePolicy.IsBlacklisted)
                {
                    _logger.LogWarning($"尝试上传黑名单文件类型: {fileExtension}");
                    throw new InvalidOperationException($"文件类型 {fileExtension} 不被允许");
                }
                
                // 然后检查配置中的文件类型限制
                if (_disallowedExtensions.Any(ext => fileExtension.Equals(ext, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogWarning($"尝试上传配置中禁止的文件类型: {fileExtension}");
                    throw new InvalidOperationException($"文件类型 {fileExtension} 在配置中被禁止");
                }
                
                // 如果配置了允许的文件类型列表，则检查是否在允许列表中
                if (_allowedExtensions.Any() && !_allowedExtensions.Any(ext => fileExtension.Equals(ext, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogWarning($"尝试上传不在允许列表中的文件类型: {fileExtension}");
                    throw new InvalidOperationException($"文件类型 {fileExtension} 不在允许的文件类型列表中");
                }

                // 验证文件大小
                if (fileSize > _maxFileSize)
                {
                    _logger.LogWarning($"尝试上传过大的文件: {fileSize} 字节");
                    throw new InvalidOperationException($"文件大小超过最大限制 {_maxFileSize / (1024 * 1024)}MB");
                }

                // Generate unique storage name
                var storageName = $"{Guid.NewGuid()}{Path.GetExtension(fileName)}";
                var storageFullPath = Path.Combine(_storagePath, storageName);
                var fileId = Guid.NewGuid();

                // Save file to disk
                using (var stream = new FileStream(storageFullPath, FileMode.Create))
                {
                    await fileStream.CopyToAsync(stream);
                }

                // 使用Raw SQL插入数据
                await _context.Db.Ado.ExecuteCommandAsync(
                    "INSERT INTO StoredFiles (Id, OriginalName, StorageName, ContentType, Size, StoragePath, UploadTime, UploaderId) " +
                    "VALUES (@Id, @OriginalName, @StorageName, @ContentType, @Size, @StoragePath, @UploadTime, @UploaderId)",
                    new {
                        Id = fileId,
                        OriginalName = fileName,
                        StorageName = storageName,
                        ContentType = contentType,
                        Size = fileSize,
                        StoragePath = storageFullPath,
                        UploadTime = DateTime.UtcNow,
                        UploaderId = uploaderId
                    }
                );

                // 返回查询到的文件对象
                return await _context.Db.Queryable<StoredFile>().Where(f => f.Id == fileId).FirstAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file");
                throw;
            }
        }
        
        public async Task<string> SaveFileAsync(IFormFile file, string uploaderId = null)
        {
            var fileId = Guid.NewGuid();
            var storageName = $"{fileId}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(_storagePath, storageName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // 使用Raw SQL插入数据
            await _context.Db.Ado.ExecuteCommandAsync(
                "INSERT INTO StoredFiles (Id, OriginalName, DisplayName, StorageName, ContentType, Size, StoragePath, UploadTime, UploaderId) " +
                "VALUES (@Id, @OriginalName, @DisplayName, @StorageName, @ContentType, @Size, @StoragePath, @UploadTime, @UploaderId)",
                new {
                    Id = fileId,
                    OriginalName = file.FileName,
                    DisplayName = file.FileName,
                    StorageName = storageName,
                    ContentType = file.ContentType,
                    Size = file.Length,
                    StoragePath = filePath,
                    UploadTime = DateTime.Now,
                    UploaderId = uploaderId
                }
            );
            
            return fileId.ToString();
        }

        public async Task<FileDownloadResult?> DownloadFileAsync(Guid fileId)
        {
            try
            {
                // 使用Queryable查询数据
                var file = _context.Db.Queryable<StoredFile>().Where(f => f.Id == fileId).First();
                
                if (file == null)
                {
                    _logger.LogWarning($"File not found: {fileId}");
                    return null;
                }

                var stream = System.IO.File.OpenRead(file.StoragePath);
                return new FileDownloadResult
                {
                    FileStream = stream,
                    FileName = file.OriginalName,
                    ContentType = file.ContentType
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error downloading file: {fileId}");
                throw;
            }
        }

        public async Task<FileDownloadResult?> DownloadFileByShareLinkAsync(Guid linkId, string? password = null)
        {
            try
            {
                // 使用Queryable查询数据
                var link = _context.Db.Queryable<FileShareLink>().Where(l => l.Id == linkId && l.IsActive).First();

                if (link == null || link.ExpireTime < DateTime.UtcNow)
                {
                    _logger.LogWarning($"Invalid or expired share link: {linkId}");
                    return null;
                }

                if (!string.IsNullOrEmpty(link.PasswordHash))
                {
                    if (string.IsNullOrEmpty(password) || !VerifyPassword(password, link.PasswordHash))
                    {
                        _logger.LogWarning($"Invalid password for share link: {linkId}");
                        return null;
                    }
                }

                return await DownloadFileAsync(link.FileId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error downloading via share link: {linkId}");
                throw;
            }
        }

        public async Task DeleteFileAsync(Guid fileId)
        {
            try
            {
                // 使用Queryable查询数据
                var file = _context.Db.Queryable<StoredFile>().Where(f => f.Id == fileId).First();
                if (file != null)
                {
                    if (System.IO.File.Exists(file.StoragePath))
                    {
                        System.IO.File.Delete(file.StoragePath);
                    }
                    // 使用Raw SQL删除数据
                    await _context.Db.Ado.ExecuteCommandAsync("DELETE FROM StoredFiles WHERE Id = @Id", new { Id = fileId });
                    _logger.LogInformation($"File deleted: {fileId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting file: {fileId}");
                throw;
            }
        }

        public async Task UpdateFileInfoAsync(Guid fileId, string displayName)
        {
            try
            {
                // 使用Raw SQL更新数据
                await _context.Db.Ado.ExecuteCommandAsync(
                    "UPDATE StoredFiles SET DisplayName = @DisplayName WHERE Id = @Id",
                    new { DisplayName = displayName, Id = fileId }
                );
                _logger.LogInformation($"File info updated: {fileId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating file info: {fileId}");
                throw;
            }
        }

        public async Task<FileShareLink> CreateShareLinkAsync(Guid fileId, DateTime? expireTime, string? password, string? creatorId = null)
        {
            try
            {
                var linkId = Guid.NewGuid();
                
                // 使用Raw SQL插入数据
                await _context.Db.Ado.ExecuteCommandAsync(
                    "INSERT INTO FileShareLinks (Id, FileId, PasswordHash, ExpireTime, CreateTime, CreatorId, IsActive) " +
                    "VALUES (@Id, @FileId, @PasswordHash, @ExpireTime, @CreateTime, @CreatorId, @IsActive)",
                    new {
                        Id = linkId,
                        FileId = fileId,
                        PasswordHash = !string.IsNullOrEmpty(password) ? HashPassword(password) : null,
                        ExpireTime = expireTime ?? DateTime.UtcNow.AddDays(7),
                        CreateTime = DateTime.UtcNow,
                        CreatorId = creatorId,
                        IsActive = true
                    }
                );
                
                _logger.LogInformation($"Share link created for file: {fileId}");

                // 使用Queryable查询数据
                return _context.Db.Queryable<FileShareLink>().Where(l => l.Id == linkId).First();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating share link for file: {fileId}");
                throw;
            }
        }

        public async Task DisableShareLinkAsync(Guid linkId)
        {
            try
            {
                // 使用Raw SQL更新数据
                await _context.Db.Ado.ExecuteCommandAsync(
                    "UPDATE FileShareLinks SET IsActive = 0 WHERE Id = @Id",
                    new { Id = linkId }
                );
                _logger.LogInformation($"Share link disabled: {linkId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error disabling share link: {linkId}");
                throw;
            }
        }

        public async Task<IEnumerable<StoredFile>> GetUserFilesAsync(string userId)
        {
            try
            {
                // 使用Queryable查询数据
                return _context.Db.Queryable<StoredFile>().Where(f => f.UploaderId == userId).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting files for user: {userId}");
                throw;
            }
        }

        public async Task<IEnumerable<FileShareLink>> GetFileShareLinksAsync(Guid fileId)
        {
            try
            {
                // 使用Queryable查询数据
                return _context.Db.Queryable<FileShareLink>().Where(l => l.FileId == fileId).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting share links for file: {fileId}");
                throw;
            }
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        private bool VerifyPassword(string inputPassword, string storedHash)
        {
            return HashPassword(inputPassword) == storedHash;
        }
    }
}