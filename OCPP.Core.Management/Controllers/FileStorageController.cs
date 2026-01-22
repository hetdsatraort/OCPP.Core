using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OCPP.Core.Management.Services;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OCPP.Core.Management.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class FileStorageController : ControllerBase
    {
        private readonly IFileStorageService _fileStorageService;

        public FileStorageController(IFileStorageService fileStorageService)
        {
            _fileStorageService = fileStorageService;
        }

        /// <summary>
        /// Upload a file
        /// </summary>
        /// <param name="file">The file to upload</param>
        /// <param name="remarks">Optional remarks about the file</param>
        /// <returns>File ID if successful</returns>
        [HttpPost("upload")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> UploadFile(IFormFile file, [FromForm] string remarks = null)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Ok(new { success = false, message = "User not authenticated" });
                }

                var result = await _fileStorageService.UploadFileAsync(file, userId, remarks);

                if (result.Success)
                {
                    return Ok(new
                    {
                        success = true,
                        fileId = result.FileId,
                        message = result.Message
                    });
                }

                return Ok(new { success = false, message = result.Message });
            }
            catch (Exception ex)
            {
                return Ok( new { success = false, message = $"Internal server error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Download a file by ID
        /// </summary>
        /// <param name="fileId">The ID of the file to download</param>
        /// <returns>File content</returns>
        [HttpGet("download/{fileId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> DownloadFile(string fileId)
        {
            try
            {
                var result = await _fileStorageService.DownloadFileAsync(fileId);

                if (result.Success)
                {
                    return File(result.FileContent, result.ContentType, result.FileName);
                }

                return Ok(new { success = false, message = result.Message });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = $"Internal server error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Get file information without downloading the content
        /// </summary>
        /// <param name="fileId">The ID of the file</param>
        /// <returns>File metadata</returns>
        [HttpGet("info/{fileId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetFileInfo(string fileId)
        {
            try
            {
                var result = await _fileStorageService.GetFileInfoAsync(fileId);

                if (result.Success)
                {
                    return Ok(new
                    {
                        success = true,
                        fileInfo = new
                        {
                            fileId = result.FileInfo.RecId,
                            userId = result.FileInfo.UserId,
                            fileName = result.FileInfo.FileName,
                            fileType = result.FileInfo.FileType,
                            fileSize = result.FileInfo.FileSize,
                            remarks = result.FileInfo.Remarks,
                            createdOn = result.FileInfo.CreatedOn,
                            updatedOn = result.FileInfo.UpdatedOn
                        },
                        message = result.Message
                    });
                }

                return Ok(new { success = false, message = result.Message });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = $"Internal server error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Delete a file (soft delete)
        /// </summary>
        /// <param name="fileId">The ID of the file to delete</param>
        /// <returns>Success status</returns>
        [HttpDelete("{fileId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> DeleteFile(string fileId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Ok(new { success = false, message = "User not authenticated" });
                }

                var result = await _fileStorageService.DeleteFileAsync(fileId, userId);

                if (result.Success)
                {
                    return Ok(new { success = true, message = result.Message });
                }

                if (result.Message.Contains("Unauthorized"))
                {
                    return Ok(new { success = false, message = result.Message });
                }

                return Ok(new { success = false, message = result.Message });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = $"Internal server error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Update file remarks
        /// </summary>
        /// <param name="fileId">The ID of the file</param>
        /// <param name="request">New remarks</param>
        /// <returns>Success status</returns>
        [HttpPatch("{fileId}/remarks")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateFileRemarks(string fileId, [FromBody] UpdateFileRemarksRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Ok(new { success = false, message = "User not authenticated" });
                }

                var result = await _fileStorageService.UpdateFileRemarksAsync(fileId, userId, request.Remarks);

                if (result.Success)
                {
                    return Ok(new { success = true, message = result.Message });
                }

                if (result.Message.Contains("Unauthorized"))
                {
                    return Ok(new { success = false, message = result.Message });
                }

                return Ok(new { success = false, message = result.Message });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = $"Internal server error: {ex.Message}" });
            }
        }
    }

    public class UpdateFileRemarksRequest
    {
        public string Remarks { get; set; }
    }
}
