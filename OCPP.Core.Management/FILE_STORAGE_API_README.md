# File Storage API Documentation

## Overview
The File Storage API provides functionality to upload, download, and manage files in the OCPP.Core system. Files are stored in the database with metadata and can be retrieved by authorized users.

## Features
- ✅ File upload with size validation (max 10 MB)
- ✅ File download by ID
- ✅ File metadata retrieval without downloading content
- ✅ Soft delete functionality
- ✅ Update file remarks
- ✅ User-based authorization
- ✅ Support for any file type

## Database Schema

### FileMaster Table
| Column | Type | Description |
|--------|------|-------------|
| RecId | string(50) | Primary key - Unique file identifier (GUID) |
| UserId | string(50) | User who uploaded the file |
| FileName | string(200) | Original file name |
| FileType | string(50) | MIME type (e.g., image/jpeg, application/pdf) |
| FileContent | byte[] | Binary file content |
| FileSize | long | File size in bytes |
| FileURL | string(500) | Reserved for future external storage URL |
| Remarks | string(500) | Optional user notes about the file |
| Active | int | 1 = active, 0 = deleted (soft delete) |
| CreatedOn | DateTime | Upload timestamp (UTC) |
| UpdatedOn | DateTime | Last modification timestamp (UTC) |

## API Endpoints

### 1. Upload File
Upload a file to the system.

**Endpoint:** `POST /api/FileStorage/upload`

**Authorization:** Required (Bearer token)

**Content-Type:** `multipart/form-data`

**Request Parameters:**
- `file` (FormFile, required): The file to upload
- `remarks` (string, optional): Notes about the file

**Example Request (cURL):**
```bash
curl -X POST "https://localhost:5001/api/FileStorage/upload" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -F "file=@/path/to/your/file.pdf" \
  -F "remarks=Important document"
```

**Response (Success - 200 OK):**
```json
{
  "success": true,
  "fileId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "message": "File uploaded successfully"
}
```

**Response (Error - 400 Bad Request):**
```json
{
  "success": false,
  "message": "File size exceeds maximum allowed size of 10 MB"
}
```

---

### 2. Download File
Download a file by its ID.

**Endpoint:** `GET /api/FileStorage/download/{fileId}`

**Authorization:** Required (Bearer token)

**Path Parameters:**
- `fileId` (string, required): The unique file identifier

**Example Request (cURL):**
```bash
curl -X GET "https://localhost:5001/api/FileStorage/download/a1b2c3d4-e5f6-7890-abcd-ef1234567890" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  --output downloaded_file.pdf
```

**Response (Success - 200 OK):**
- Returns the file as a binary stream with appropriate Content-Type header
- Content-Disposition header includes the original filename

**Response (Error - 404 Not Found):**
```json
{
  "success": false,
  "message": "File not found or has been deleted"
}
```

---

### 3. Get File Info
Retrieve file metadata without downloading the content.

**Endpoint:** `GET /api/FileStorage/info/{fileId}`

**Authorization:** Required (Bearer token)

**Path Parameters:**
- `fileId` (string, required): The unique file identifier

**Example Request (cURL):**
```bash
curl -X GET "https://localhost:5001/api/FileStorage/info/a1b2c3d4-e5f6-7890-abcd-ef1234567890" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

**Response (Success - 200 OK):**
```json
{
  "success": true,
  "fileInfo": {
    "fileId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "userId": "user123",
    "fileName": "document.pdf",
    "fileType": "application/pdf",
    "fileSize": 1024567,
    "remarks": "Important document",
    "createdOn": "2024-01-18T10:30:00Z",
    "updatedOn": "2024-01-18T10:30:00Z"
  },
  "message": "File info retrieved successfully"
}
```

---

### 4. Delete File
Soft delete a file (only the owner can delete).

**Endpoint:** `DELETE /api/FileStorage/{fileId}`

**Authorization:** Required (Bearer token)

**Path Parameters:**
- `fileId` (string, required): The unique file identifier

**Example Request (cURL):**
```bash
curl -X DELETE "https://localhost:5001/api/FileStorage/a1b2c3d4-e5f6-7890-abcd-ef1234567890" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

**Response (Success - 200 OK):**
```json
{
  "success": true,
  "message": "File deleted successfully"
}
```

**Response (Error - 403 Forbidden):**
```json
{
  "success": false,
  "message": "Unauthorized: You can only delete your own files"
}
```

---

### 5. Update File Remarks
Update the remarks/notes for a file (only the owner can update).

**Endpoint:** `PATCH /api/FileStorage/{fileId}/remarks`

**Authorization:** Required (Bearer token)

**Path Parameters:**
- `fileId` (string, required): The unique file identifier

**Request Body:**
```json
{
  "remarks": "Updated remarks text"
}
```

**Example Request (cURL):**
```bash
curl -X PATCH "https://localhost:5001/api/FileStorage/a1b2c3d4-e5f6-7890-abcd-ef1234567890/remarks" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"remarks": "Updated remarks text"}'
```

**Response (Success - 200 OK):**
```json
{
  "success": true,
  "message": "File remarks updated successfully"
}
```

---

## Service Layer Usage

You can also use the `IFileStorageService` directly in your code for programmatic file operations.

### Example: Upload a file from code

```csharp
public class MyService
{
    private readonly IFileStorageService _fileStorageService;
    
    public MyService(IFileStorageService fileStorageService)
    {
        _fileStorageService = fileStorageService;
    }
    
    public async Task<string> UploadUserDocument(IFormFile file, string userId)
    {
        var result = await _fileStorageService.UploadFileAsync(
            file, 
            userId, 
            "User verification document"
        );
        
        if (result.Success)
        {
            return result.FileId;
        }
        
        throw new Exception(result.Message);
    }
}
```

### Example: Download a file from code

```csharp
public async Task<byte[]> GetFileContent(string fileId)
{
    var result = await _fileStorageService.DownloadFileAsync(fileId);
    
    if (result.Success)
    {
        return result.FileContent;
    }
    
    throw new FileNotFoundException(result.Message);
}
```

### Example: Get file information

```csharp
public async Task<FileMaster> GetFileDetails(string fileId)
{
    var result = await _fileStorageService.GetFileInfoAsync(fileId);
    
    if (result.Success)
    {
        return result.FileInfo;
    }
    
    return null;
}
```

### Example: Delete a file

```csharp
public async Task<bool> RemoveFile(string fileId, string userId)
{
    var result = await _fileStorageService.DeleteFileAsync(fileId, userId);
    return result.Success;
}
```

### Example: Update remarks

```csharp
public async Task<bool> UpdateFileNotes(string fileId, string userId, string newRemarks)
{
    var result = await _fileStorageService.UpdateFileRemarksAsync(fileId, userId, newRemarks);
    return result.Success;
}
```

## Implementation Details

### File Size Limit
- Current limit: **10 MB**
- Can be configured in `FileStorageService.cs` by changing the `MaxFileSize` constant

### Security Features
1. **Authentication Required**: All endpoints require a valid JWT token
2. **User Authorization**: Users can only delete/update their own files
3. **Soft Delete**: Files are not physically deleted, just marked as inactive
4. **Content Type Validation**: Proper MIME type handling

### Database Storage
- Files are currently stored as binary data (`byte[]`) in the `FileContent` column
- This approach is suitable for files up to 10 MB
- For larger files or better performance, consider migrating to external storage (Azure Blob, AWS S3) in the future
- The `FileURL` field is reserved for future external storage integration

## Future Optimizations

When scaling up, consider these optimizations:

1. **External Storage**: Move file content to Azure Blob Storage or AWS S3
   - Update `FileURL` to point to external location
   - Keep metadata in database
   - Reduce database size significantly

2. **Streaming**: Implement streaming for large file downloads
   - Reduce memory usage
   - Better performance for large files

3. **Chunked Upload**: Support chunked/resumable uploads for large files
   - Better user experience
   - Handle network interruptions

4. **File Compression**: Compress files before storage
   - Reduce storage costs
   - Faster transfers

5. **CDN Integration**: Use CDN for frequently accessed files
   - Faster downloads
   - Reduced server load

## Migration Required

After implementing these changes, run the following command to create and apply the database migration:

```bash
cd OCPP.Core.Database
dotnet ef migrations add AddFileContentToFileMaster --startup-project ..\OCPP.Core.Management\OCPP.Core.Management.csproj
dotnet ef database update --startup-project ..\OCPP.Core.Management\OCPP.Core.Management.csproj
```

## Error Codes Summary

| Status Code | Description |
|-------------|-------------|
| 200 | Success - Operation completed |
| 400 | Bad Request - Invalid input or file too large |
| 401 | Unauthorized - Missing or invalid token |
| 403 | Forbidden - User not authorized for this operation |
| 404 | Not Found - File doesn't exist or was deleted |
| 500 | Internal Server Error - Server-side error |

## Testing with Postman

1. **Set Authorization**: Add Bearer token in Authorization tab
2. **Upload Test**: Use form-data with file field for upload endpoint
3. **Download Test**: Use Send and Download to save file locally
4. **Info Test**: Check metadata without downloading content

## Support

For issues or questions:
- Check the logs in `OCPP.Core.Management`
- Verify JWT token is valid and not expired
- Ensure database migration was applied successfully
- Check file size limits
