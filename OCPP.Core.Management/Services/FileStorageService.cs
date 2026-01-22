using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OCPP.Core.Database;
using OCPP.Core.Database.EVCDTO;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OCPP.Core.Management.Services
{
    public interface IFileStorageService
    {
        Task<(bool Success, string FileId, string Message)> UploadFileAsync(IFormFile file, string userId, string remarks = null);
        Task<(bool Success, byte[] FileContent, string FileName, string ContentType, string Message)> DownloadFileAsync(string fileId);
        Task<(bool Success, FileMaster FileInfo, string Message)> GetFileInfoAsync(string fileId);
        Task<(bool Success, string Message)> DeleteFileAsync(string fileId, string userId);
        Task<(bool Success, string Message)> UpdateFileRemarksAsync(string fileId, string userId, string remarks);
    }

    public class FileStorageService : IFileStorageService
    {
        private readonly OCPPCoreContext _dbContext;
        private const long MaxFileSize = 10 * 1024 * 1024; // 10 MB

        public FileStorageService(OCPPCoreContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<(bool Success, string FileId, string Message)> UploadFileAsync(
            IFormFile file, 
            string userId, 
            string remarks = null)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return (false, null, "No file provided or file is empty");
                }

                if (file.Length > MaxFileSize)
                {
                    return (false, null, $"File size exceeds maximum allowed size of {MaxFileSize / (1024 * 1024)} MB");
                }

                byte[] fileContent;
                using (var memoryStream = new MemoryStream())
                {
                    await file.CopyToAsync(memoryStream);
                    fileContent = memoryStream.ToArray();
                }

                var fileRecord = new FileMaster
                {
                    RecId = Guid.NewGuid().ToString(),
                    UserId = userId,
                    FileName = file.FileName,
                    FileType = file.ContentType ?? "application/octet-stream",
                    FileContent = fileContent,
                    FileSize = file.Length,
                    FileURL = string.Empty,
                    Remarks = remarks ?? string.Empty,
                    Active = 1,
                    CreatedOn = DateTime.UtcNow,
                    UpdatedOn = DateTime.UtcNow
                };

                _dbContext.FileMasters.Add(fileRecord);
                await _dbContext.SaveChangesAsync();

                return (true, fileRecord.RecId, "File uploaded successfully");
            }
            catch (Exception ex)
            {
                return (false, null, $"Error uploading file: {ex.Message}");
            }
        }

        public async Task<(bool Success, byte[] FileContent, string FileName, string ContentType, string Message)> DownloadFileAsync(string fileId)
        {
            try
            {
                if (string.IsNullOrEmpty(fileId))
                {
                    return (false, null, null, null, "File ID is required");
                }

                var fileRecord = await _dbContext.FileMasters
                    .FirstOrDefaultAsync(f => f.RecId == fileId && f.Active == 1);

                if (fileRecord == null)
                {
                    return (false, null, null, null, "File not found or has been deleted");
                }

                if (fileRecord.FileContent == null || fileRecord.FileContent.Length == 0)
                {
                    return (false, null, null, null, "File content is empty");
                }

                return (true, fileRecord.FileContent, fileRecord.FileName, fileRecord.FileType, "File retrieved successfully");
            }
            catch (Exception ex)
            {
                return (false, null, null, null, $"Error downloading file: {ex.Message}");
            }
        }

        public async Task<(bool Success, FileMaster FileInfo, string Message)> GetFileInfoAsync(string fileId)
        {
            try
            {
                if (string.IsNullOrEmpty(fileId))
                {
                    return (false, null, "File ID is required");
                }

                var fileRecord = await _dbContext.FileMasters
                    .AsNoTracking()
                    .Where(f => f.RecId == fileId && f.Active == 1)
                    .Select(f => new FileMaster
                    {
                        RecId = f.RecId,
                        UserId = f.UserId,
                        FileName = f.FileName,
                        FileType = f.FileType,
                        FileSize = f.FileSize,
                        FileURL = f.FileURL,
                        Remarks = f.Remarks,
                        Active = f.Active,
                        CreatedOn = f.CreatedOn,
                        UpdatedOn = f.UpdatedOn
                    })
                    .FirstOrDefaultAsync();

                if (fileRecord == null)
                {
                    return (false, null, "File not found or has been deleted");
                }

                return (true, fileRecord, "File info retrieved successfully");
            }
            catch (Exception ex)
            {
                return (false, null, $"Error retrieving file info: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> DeleteFileAsync(string fileId, string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(fileId))
                {
                    return (false, "File ID is required");
                }

                var fileRecord = await _dbContext.FileMasters
                    .FirstOrDefaultAsync(f => f.RecId == fileId && f.Active == 1);

                if (fileRecord == null)
                {
                    return (false, "File not found or already deleted");
                }

                if (fileRecord.UserId != userId)
                {
                    return (false, "Unauthorized: You can only delete your own files");
                }

                fileRecord.Active = 0;
                fileRecord.UpdatedOn = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                return (true, "File deleted successfully");
            }
            catch (Exception ex)
            {
                return (false, $"Error deleting file: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> UpdateFileRemarksAsync(string fileId, string userId, string remarks)
        {
            try
            {
                if (string.IsNullOrEmpty(fileId))
                {
                    return (false, "File ID is required");
                }

                var fileRecord = await _dbContext.FileMasters
                    .FirstOrDefaultAsync(f => f.RecId == fileId && f.Active == 1);

                if (fileRecord == null)
                {
                    return (false, "File not found or has been deleted");
                }

                if (fileRecord.UserId != userId)
                {
                    return (false, "Unauthorized: You can only update your own files");
                }

                fileRecord.Remarks = remarks ?? string.Empty;
                fileRecord.UpdatedOn = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                return (true, "File remarks updated successfully");
            }
            catch (Exception ex)
            {
                return (false, $"Error updating file remarks: {ex.Message}");
            }
        }
    }
}
