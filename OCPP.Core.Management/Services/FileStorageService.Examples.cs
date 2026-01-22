//// File Storage Service - Quick Reference Examples
//// Copy these examples to use file storage throughout your codebase

//using OCPP.Core.Management.Services;
//using OCPP.Core.Database.EVCDTO;
//using Microsoft.AspNetCore.Http;

//namespace OCPP.Core.Management.Examples
//{
//    /// <summary>
//    /// Examples of using IFileStorageService programmatically
//    /// </summary>
//    public class FileStorageExamples
//    {
//        private readonly IFileStorageService _fileStorageService;

//        public FileStorageExamples(IFileStorageService fileStorageService)
//        {
//            _fileStorageService = fileStorageService;
//        }

//        // ========================================
//        // EXAMPLE 1: Upload a file from user
//        // ========================================
//        public async Task<string> UploadUserProfilePicture(IFormFile file, string userId)
//        {
//            var result = await _fileStorageService.UploadFileAsync(
//                file,
//                userId,
//                "User profile picture"
//            );

//            if (!result.Success)
//            {
//                throw new Exception($"Upload failed: {result.Message}");
//            }

//            return result.FileId; // Store this ID for later retrieval
//        }

//        // ========================================
//        // EXAMPLE 2: Upload verification documents
//        // ========================================
//        public async Task<List<string>> UploadVerificationDocuments(
//            List<IFormFile> documents,
//            string userId)
//        {
//            var uploadedFileIds = new List<string>();

//            foreach (var doc in documents)
//            {
//                var result = await _fileStorageService.UploadFileAsync(
//                    doc,
//                    userId,
//                    $"Verification document - {doc.FileName}"
//                );

//                if (result.Success)
//                {
//                    uploadedFileIds.Add(result.FileId);
//                }
//            }

//            return uploadedFileIds;
//        }

//        // ========================================
//        // EXAMPLE 3: Download file content
//        // ========================================
//        public async Task<byte[]> DownloadInvoice(string fileId)
//        {
//            var result = await _fileStorageService.DownloadFileAsync(fileId);

//            if (!result.Success)
//            {
//                throw new FileNotFoundException(result.Message);
//            }

//            return result.FileContent;
//        }

//        // ========================================
//        // EXAMPLE 4: Get file information only
//        // ========================================
//        public async Task<(string fileName, long fileSize, DateTime uploadDate)> GetFileMetadata(string fileId)
//        {
//            var result = await _fileStorageService.GetFileInfoAsync(fileId);

//            if (!result.Success)
//            {
//                return (null, 0, DateTime.MinValue);
//            }

//            return (
//                result.FileInfo.FileName,
//                result.FileInfo.FileSize,
//                result.FileInfo.CreatedOn
//            );
//        }

//        // ========================================
//        // EXAMPLE 5: Delete old files
//        // ========================================
//        public async Task<bool> DeleteExpiredDocuments(string fileId, string userId)
//        {
//            var result = await _fileStorageService.DeleteFileAsync(fileId, userId);
//            return result.Success;
//        }

//        // ========================================
//        // EXAMPLE 6: Update file description
//        // ========================================
//        public async Task<bool> UpdateDocumentRemarks(
//            string fileId,
//            string userId,
//            string newRemarks)
//        {
//            var result = await _fileStorageService.UpdateFileRemarksAsync(
//                fileId,
//                userId,
//                newRemarks
//            );
//            return result.Success;
//        }

//        // ========================================
//        // EXAMPLE 7: Check if file exists and is accessible
//        // ========================================
//        public async Task<bool> IsFileAccessible(string fileId)
//        {
//            var result = await _fileStorageService.GetFileInfoAsync(fileId);
//            return result.Success;
//        }

//        // ========================================
//        // EXAMPLE 8: Download and save to response stream (in a controller)
//        // ========================================
//        public async Task<IActionResult> DownloadUserDocument(string fileId)
//        {
//            var result = await _fileStorageService.DownloadFileAsync(fileId);

//            if (!result.Success)
//            {
//                return new NotFoundObjectResult(new { message = result.Message });
//            }

//            return new FileContentResult(result.FileContent, result.ContentType)
//            {
//                FileDownloadName = result.FileName
//            };
//        }

//        // ========================================
//        // EXAMPLE 9: Validate and upload with custom checks
//        // ========================================
//        public async Task<(bool success, string fileId, string message)> UploadWithValidation(
//            IFormFile file,
//            string userId,
//            string[] allowedExtensions)
//        {
//            // Custom validation
//            var fileExtension = Path.GetExtension(file.FileName).ToLower();
//            if (!allowedExtensions.Contains(fileExtension))
//            {
//                return (false, null, $"File type {fileExtension} is not allowed");
//            }

//            // Upload file
//            var result = await _fileStorageService.UploadFileAsync(
//                file,
//                userId,
//                $"Uploaded {fileExtension} file"
//            );

//            return result;
//        }

//        // ========================================
//        // EXAMPLE 10: Batch file operations
//        // ========================================
//        public async Task<Dictionary<string, bool>> DeleteMultipleFiles(
//            List<string> fileIds,
//            string userId)
//        {
//            var results = new Dictionary<string, bool>();

//            foreach (var fileId in fileIds)
//            {
//                var result = await _fileStorageService.DeleteFileAsync(fileId, userId);
//                results[fileId] = result.Success;
//            }

//            return results;
//        }

//        // ========================================
//        // EXAMPLE 11: Generate download URL (for future external storage)
//        // ========================================
//        public async Task<string> GetDownloadUrl(string fileId)
//        {
//            var fileInfo = await _fileStorageService.GetFileInfoAsync(fileId);

//            if (!fileInfo.Success)
//            {
//                return null;
//            }

//            // For now, return API endpoint. Later can return external storage URL
//            return $"/api/FileStorage/download/{fileId}";
//        }

//        // ========================================
//        // EXAMPLE 12: Store transaction receipt
//        // ========================================
//        public async Task<string> StoreTransactionReceipt(
//            string transactionId,
//            string userId,
//            byte[] pdfContent)
//        {
//            // Create a temporary IFormFile from byte array
//            var stream = new MemoryStream(pdfContent);
//            var formFile = new FormFile(stream, 0, pdfContent.Length, "receipt", $"receipt_{transactionId}.pdf")
//            {
//                Headers = new HeaderDictionary(),
//                ContentType = "application/pdf"
//            };

//            var result = await _fileStorageService.UploadFileAsync(
//                formFile,
//                userId,
//                $"Transaction receipt for {transactionId}"
//            );

//            return result.FileId;
//        }

//        // ========================================
//        // EXAMPLE 13: Copy file for another user
//        // ========================================
//        public async Task<string> CopyFileToUser(string sourceFileId, string targetUserId)
//        {
//            // Download original
//            var downloadResult = await _fileStorageService.DownloadFileAsync(sourceFileId);
//            if (!downloadResult.Success)
//            {
//                return null;
//            }

//            // Create new file for target user
//            var stream = new MemoryStream(downloadResult.FileContent);
//            var formFile = new FormFile(
//                stream,
//                0,
//                downloadResult.FileContent.Length,
//                "file",
//                downloadResult.FileName)
//            {
//                Headers = new HeaderDictionary(),
//                ContentType = downloadResult.ContentType
//            };

//            var uploadResult = await _fileStorageService.UploadFileAsync(
//                formFile,
//                targetUserId,
//                "Copied file"
//            );

//            return uploadResult.FileId;
//        }

//        // ========================================
//        // EXAMPLE 14: Get file size before download
//        // ========================================
//        public async Task<long> GetFileSize(string fileId)
//        {
//            var result = await _fileStorageService.GetFileInfoAsync(fileId);
//            return result.Success ? result.FileInfo.FileSize : 0;
//        }

//        // ========================================
//        // EXAMPLE 15: Validate file ownership
//        // ========================================
//        public async Task<bool> IsFileOwnedByUser(string fileId, string userId)
//        {
//            var result = await _fileStorageService.GetFileInfoAsync(fileId);
//            return result.Success && result.FileInfo.UserId == userId;
//        }
//    }

//    // ========================================
//    // INTEGRATION PATTERNS
//    // ========================================

//    /// <summary>
//    /// Pattern 1: User registration with profile picture
//    /// </summary>
//    public class UserRegistrationWithFile
//    {
//        private readonly IFileStorageService _fileStorageService;

//        public async Task<string> RegisterUserWithProfilePicture(
//            string userId,
//            IFormFile profilePicture)
//        {
//            if (profilePicture != null)
//            {
//                var result = await _fileStorageService.UploadFileAsync(
//                    profilePicture,
//                    userId,
//                    "Profile picture"
//                );

//                if (result.Success)
//                {
//                    // Store the fileId in Users table or separate ProfilePictures table
//                    return result.FileId;
//                }
//            }

//            return null;
//        }
//    }

//    /// <summary>
//    /// Pattern 2: Charging session with invoice
//    /// </summary>
//    public class ChargingSessionWithInvoice
//    {
//        private readonly IFileStorageService _fileStorageService;

//        public async Task<string> StoreSessionInvoice(
//            string sessionId,
//            string userId,
//            byte[] invoicePdf)
//        {
//            var stream = new MemoryStream(invoicePdf);
//            var file = new FormFile(stream, 0, invoicePdf.Length, "invoice", $"invoice_{sessionId}.pdf")
//            {
//                ContentType = "application/pdf"
//            };

//            var result = await _fileStorageService.UploadFileAsync(
//                file,
//                userId,
//                $"Invoice for charging session {sessionId}"
//            );

//            // Store result.FileId in ChargingSession.InvoiceFileId column
//            return result.FileId;
//        }
//    }

//    /// <summary>
//    /// Pattern 3: Charging hub with images
//    /// </summary>
//    public class ChargingHubWithImages
//    {
//        private readonly IFileStorageService _fileStorageService;

//        public async Task<List<string>> UploadHubImages(
//            string hubId,
//            string userId,
//            List<IFormFile> images)
//        {
//            var imageIds = new List<string>();

//            for (int i = 0; i < images.Count; i++)
//            {
//                var result = await _fileStorageService.UploadFileAsync(
//                    images[i],
//                    userId,
//                    $"Charging hub {hubId} - Image {i + 1}"
//                );

//                if (result.Success)
//                {
//                    imageIds.Add(result.FileId);
//                }
//            }

//            // Store imageIds in ChargingHubImages junction table
//            return imageIds;
//        }
//    }
//}
