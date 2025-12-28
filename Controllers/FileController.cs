using DynamicDbApi.Models;
using DynamicDbApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DynamicDbApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FileController : ControllerBase
    {
        private readonly IFileStorageService _fileService;

        public FileController(IFileStorageService fileService)
        {
            _fileService = fileService;
        }

        [HttpPost("upload")]
        [DisableRequestSizeLimit]
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            using (var stream = file.OpenReadStream())
            {
                var storedFile = await _fileService.UploadFileAsync(
                    stream,
                    file.FileName,
                    file.ContentType,
                    file.Length,
                    User?.Identity?.Name);

                return Ok(new { 
                    FileId = storedFile.Id,
                    OriginalName = storedFile.OriginalName,
                    Size = storedFile.Size
                });
            }
        }

        [HttpGet("download/{fileId}")]
        public async Task<IActionResult> DownloadFile(Guid fileId)
        {
            var result = await _fileService.DownloadFileAsync(fileId);
            if (result == null)
                return NotFound();

            return File(result.FileStream, result.ContentType, result.FileName);
        }

        [HttpGet("download/share/{linkId}")]
        public async Task<IActionResult> DownloadByShareLink(Guid linkId, [FromQuery] string? password = null)
        {
            var result = await _fileService.DownloadFileByShareLinkAsync(linkId, password);
            if (result == null)
                return NotFound();

            return File(result.FileStream, result.ContentType, result.FileName);
        }

        [HttpDelete("{fileId}")]
        [Authorize]
        public async Task<IActionResult> DeleteFile(Guid fileId)
        {
            await _fileService.DeleteFileAsync(fileId);
            return NoContent();
        }

        [HttpPut("{fileId}")]
        [Authorize]
        public async Task<IActionResult> UpdateFileInfo(Guid fileId, [FromBody] UpdateFileInfoRequest request)
        {
            await _fileService.UpdateFileInfoAsync(fileId, request.DisplayName);
            return Ok();
        }

        [HttpPost("{fileId}/share")]
        [Authorize]
        public async Task<IActionResult> CreateShareLink(Guid fileId, [FromBody] CreateShareLinkRequest request)
        {
            var link = await _fileService.CreateShareLinkAsync(
                fileId, 
                request.ExpireTime, 
                request.Password,
                User?.Identity?.Name);

            return Ok(new {
                LinkId = link.Id,
                ExpireTime = link.ExpireTime
            });
        }

        [HttpDelete("share/{linkId}")]
        [Authorize]
        public async Task<IActionResult> DisableShareLink(Guid linkId)
        {
            await _fileService.DisableShareLinkAsync(linkId);
            return NoContent();
        }

        [HttpGet("user")]
        [Authorize]
        public async Task<IActionResult> GetUserFiles()
        {
            var userId = User?.Identity?.Name;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }
            
            var files = await _fileService.GetUserFilesAsync(userId!);
            return Ok(files);
        }

        [HttpGet("{fileId}/share-links")]
        [Authorize]
        public async Task<IActionResult> GetFileShareLinks(Guid fileId)
        {
            var links = await _fileService.GetFileShareLinksAsync(fileId);
            return Ok(links);
        }
    }

    public class UpdateFileInfoRequest
    {
        public required string DisplayName { get; set; }
    }

    public class CreateShareLinkRequest
    {
        public DateTime? ExpireTime { get; set; }
        public string? Password { get; set; }
    }
}