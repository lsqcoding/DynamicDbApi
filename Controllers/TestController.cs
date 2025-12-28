using DynamicDbApi.Models;
using DynamicDbApi.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace DynamicDbApi.Controllers
{
    [ApiController]
    [Route("api/test")]
    public class TestController : ControllerBase
    {
        private readonly IFileStorageService _fileService;
        private readonly IMailService _mailService;
        private readonly IRealTimeMessageService _messageService;
        private readonly ISchedulerService _schedulerService;

        public TestController(
            IFileStorageService fileService,
            IMailService mailService,
            IRealTimeMessageService messageService,
            ISchedulerService schedulerService)
        {
            _fileService = fileService;
            _mailService = mailService;
            _messageService = messageService;
            _schedulerService = schedulerService;
        }

        [HttpPost("file/upload")]
        public async Task<IActionResult> TestFileUpload([FromForm] TestFileUploadRequest request)
        {
            try
            {
                using var stream = request.File.OpenReadStream();
                var result = await _fileService.UploadFileAsync(
                    stream,
                    request.File.FileName,
                    request.File.ContentType,
                    request.File.Length);
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // Other test methods...
    }

    public class TestFileUploadRequest
    {
        public required IFormFile File { get; set; }
    }
}