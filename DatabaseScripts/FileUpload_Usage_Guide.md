# Attendance Count Approval - File Upload Guide

## Overview
The system now supports **physical file uploads** for attendance count approval requests. Files are saved in a structured folder hierarchy.

## File Storage Structure

```
wwwroot/
└── AttendanceApproval/
    └── {Year}/
        └── {Month}/
            └── {Day}/
                └── {timestamp}_{guid}.{extension}
```

### Example:
```
wwwroot/AttendanceApproval/2025/01/15/15012025143052789_a1b2c3d4.pdf
```

### File Naming Format:
`{ddMMyyyyHHmmssfff}_{GUID}.{extension}`
- **dd**: Day (01-31)
- **MM**: Month (01-12)
- **yyyy**: Year (2025)
- **HH**: Hour (00-23)
- **mm**: Minute (00-59)
- **ss**: Second (00-59)
- **fff**: Millisecond (000-999)
- **GUID**: Unique identifier
- **extension**: Original file extension

## Allowed File Types

- **Documents**: `.pdf`, `.doc`, `.docx`
- **Spreadsheets**: `.xls`, `.xlsx`
- **Images**: `.jpg`, `.jpeg`, `.png`

## File Size Limit
- **Maximum**: 50 MB per request (combined all files)

## API Endpoint

### POST: Create Attendance Count Approval with Files

**Endpoint:**
```
POST /api/EmpAttendance/attendance-count-approval
```

**Content-Type:** `multipart/form-data`

**Authorization:** Bearer Token Required

### Request Format

Use `FormData` with the following fields:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `ECode` | string | Yes | Employee code |
| `MonthYear` | string | Yes | Format: MMM-YY (e.g., Jan-25) |
| `AttendanceCount` | integer | Yes | 0-31 |
| `EmployeeRemarks` | string | No | Max 1000 characters |
| `Files` | file[] | No | Multiple files can be uploaded |

### Example Requests

#### 1. Using Postman

1. Select `POST` method
2. URL: `http://localhost:5000/api/EmpAttendance/attendance-count-approval`
3. Headers:
   ```
   Authorization: Bearer {your_token}
   ```
4. Body → form-data:
   ```
   ECode: EMP001
   MonthYear: Jan-25
   AttendanceCount: 25
   EmployeeRemarks: Please approve my attendance
   Files: [Select File 1]
   Files: [Select File 2]
   ```

#### 2. Using JavaScript (Fetch API)

```javascript
const formData = new FormData();
formData.append('ECode', 'EMP001');
formData.append('MonthYear', 'Jan-25');
formData.append('AttendanceCount', 25);
formData.append('EmployeeRemarks', 'Please approve my attendance');

// Add files
const fileInput = document.getElementById('fileInput');
for (let i = 0; i < fileInput.files.length; i++) {
    formData.append('Files', fileInput.files[i]);
}

const response = await fetch('/api/EmpAttendance/attendance-count-approval', {
    method: 'POST',
    headers: {
        'Authorization': `Bearer ${token}`
    },
    body: formData
});

const result = await response.json();
console.log(result);
```

#### 3. Using Axios (JavaScript/TypeScript)

```javascript
import axios from 'axios';

const formData = new FormData();
formData.append('ECode', 'EMP001');
formData.append('MonthYear', 'Jan-25');
formData.append('AttendanceCount', 25);
formData.append('EmployeeRemarks', 'Please approve my attendance');

// Add files
files.forEach(file => {
    formData.append('Files', file);
});

try {
    const response = await axios.post(
        '/api/EmpAttendance/attendance-count-approval',
        formData,
        {
            headers: {
                'Authorization': `Bearer ${token}`,
                'Content-Type': 'multipart/form-data'
            }
        }
    );
    console.log(response.data);
} catch (error) {
    console.error('Upload failed:', error.response.data);
}
```

#### 4. Using C# HttpClient

```csharp
using var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.Authorization = 
    new AuthenticationHeaderValue("Bearer", token);

using var form = new MultipartFormDataContent();
form.Add(new StringContent("EMP001"), "ECode");
form.Add(new StringContent("Jan-25"), "MonthYear");
form.Add(new StringContent("25"), "AttendanceCount");
form.Add(new StringContent("Please approve my attendance"), "EmployeeRemarks");

// Add files
foreach (var filePath in filePaths)
{
    var fileStream = File.OpenRead(filePath);
    var fileName = Path.GetFileName(filePath);
    form.Add(new StreamContent(fileStream), "Files", fileName);
}

var response = await httpClient.PostAsync(
    "http://localhost:5000/api/EmpAttendance/attendance-count-approval", 
    form
);

var result = await response.Content.ReadAsStringAsync();
```

#### 5. Using cURL

```bash
curl -X POST \
  http://localhost:5000/api/EmpAttendance/attendance-count-approval \
  -H 'Authorization: Bearer YOUR_TOKEN' \
  -F 'ECode=EMP001' \
  -F 'MonthYear=Jan-25' \
  -F 'AttendanceCount=25' \
  -F 'EmployeeRemarks=Please approve my attendance' \
  -F 'Files=@/path/to/file1.pdf' \
  -F 'Files=@/path/to/file2.jpg'
```

### Response Format

#### Success Response (200 OK)
```json
{
  "success": true,
  "message": "Attendance count approval request created successfully",
  "approvalId": 123
}
```

#### Error Response - Invalid File Type (400 Bad Request)
```json
{
  "success": false,
  "message": "File type .exe is not allowed. Allowed types: .pdf, .jpg, .jpeg, .png, .doc, .docx, .xls, .xlsx"
}
```

#### Error Response - Duplicate Request (400 Bad Request)
```json
{
  "success": false,
  "message": "An attendance count approval request for EMP001 for Jan-25 already exists."
}
```

#### Error Response - Validation (400 Bad Request)
```json
{
  "success": false,
  "message": "Month-Year must be in format MMM-YY (e.g., Jan-25)"
}
```

## File Information Saved in Database

For each uploaded file, the following information is stored:

| Field | Description | Example |
|-------|-------------|---------|
| `FileUrl` | Relative path to file | `/AttendanceApproval/2025/01/15/15012025143052789_a1b2c3d4.pdf` |
| `FileName` | Original file name | `attendance_proof.pdf` |
| `FileSize` | File size in bytes | `1024567` |
| `CreatedOn` | Upload timestamp | `2025-01-15T14:30:52Z` |
| `CreatedBy` | Uploader ECode | `EMP001` |

## Retrieving Files

### Get Approval with Attachments

```
GET /api/EmpAttendance/attendance-count-approval/{approvalId}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "attendanceCountApprovalId": 123,
    "eCode": "EMP001",
    "monthYear": "Jan-25",
    "attachments": [
      {
        "attachmentId": 1,
        "fileUrl": "/AttendanceApproval/2025/01/15/15012025143052789_a1b2c3d4.pdf",
        "fileName": "attendance_proof.pdf",
        "fileSize": 1024567,
        "createdOn": "2025-01-15T14:30:52Z"
      }
    ]
  }
}
```

### Access File Directly

Files can be accessed via URL:
```
http://localhost:5000/AttendanceApproval/2025/01/15/15012025143052789_a1b2c3d4.pdf
```

## Frontend Example (React)

```jsx
import React, { useState } from 'react';
import axios from 'axios';

function AttendanceApprovalForm() {
    const [formData, setFormData] = useState({
        eCode: '',
        monthYear: '',
        attendanceCount: '',
        employeeRemarks: ''
    });
    const [files, setFiles] = useState([]);
    const [uploading, setUploading] = useState(false);

    const handleFileChange = (e) => {
        setFiles(Array.from(e.target.files));
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        setUploading(true);

        const data = new FormData();
        data.append('ECode', formData.eCode);
        data.append('MonthYear', formData.monthYear);
        data.append('AttendanceCount', formData.attendanceCount);
        data.append('EmployeeRemarks', formData.employeeRemarks);

        files.forEach(file => {
            data.append('Files', file);
        });

        try {
            const response = await axios.post(
                '/api/EmpAttendance/attendance-count-approval',
                data,
                {
                    headers: {
                        'Authorization': `Bearer ${localStorage.getItem('token')}`,
                        'Content-Type': 'multipart/form-data'
                    }
                }
            );

            alert('Request submitted successfully!');
            console.log('Approval ID:', response.data.approvalId);
        } catch (error) {
            alert('Error: ' + error.response?.data?.message);
        } finally {
            setUploading(false);
        }
    };

    return (
        <form onSubmit={handleSubmit}>
            <input 
                type="text" 
                placeholder="Employee Code"
                value={formData.eCode}
                onChange={(e) => setFormData({...formData, eCode: e.target.value})}
                required
            />
            <input 
                type="text" 
                placeholder="Month-Year (e.g., Jan-25)"
                value={formData.monthYear}
                onChange={(e) => setFormData({...formData, monthYear: e.target.value})}
                required
            />
            <input 
                type="number" 
                placeholder="Attendance Count"
                value={formData.attendanceCount}
                onChange={(e) => setFormData({...formData, attendanceCount: e.target.value})}
                required
            />
            <textarea 
                placeholder="Remarks"
                value={formData.employeeRemarks}
                onChange={(e) => setFormData({...formData, employeeRemarks: e.target.value})}
            />
            <input 
                type="file" 
                multiple
                accept=".pdf,.doc,.docx,.xls,.xlsx,.jpg,.jpeg,.png"
                onChange={handleFileChange}
            />
            <button type="submit" disabled={uploading}>
                {uploading ? 'Uploading...' : 'Submit Request'}
            </button>
        </form>
    );
}

export default AttendanceApprovalForm;
```

## Security Considerations

1. **File Type Validation**: Only allowed extensions are accepted
2. **File Size Limit**: 50 MB maximum to prevent abuse
3. **Authorization Required**: Only authenticated users can upload
4. **Unique File Names**: GUID prevents file name collisions
5. **Structured Storage**: Organized by date for easy management

## Troubleshooting

### Issue: "File size exceeds limit"
- **Solution**: Reduce file size or split into multiple smaller files

### Issue: "File type not allowed"
- **Solution**: Convert file to allowed format (.pdf, .jpg, .png, .doc, .docx, .xls, .xlsx)

### Issue: "Directory not found"
- **Solution**: Ensure `wwwroot` folder exists and has write permissions

### Issue: "Authorization failed"
- **Solution**: Verify Bearer token is valid and not expired

## Maintenance

### Cleanup Old Files

Create a scheduled job to delete files older than retention period:

```csharp
// Example: Delete files older than 2 years
var retentionDate = DateTime.Now.AddYears(-2);
var oldFilesPath = Path.Combine(webHostEnvironment.WebRootPath, "AttendanceApproval", retentionDate.Year.ToString());

if (Directory.Exists(oldFilesPath))
{
    Directory.Delete(oldFilesPath, recursive: true);
}
```

## Complete!

Your file upload system is now ready to use with:
- ✅ Physical file storage in structured folders
- ✅ File validation (type & size)
- ✅ Database tracking of file metadata
- ✅ Secure access with authentication
- ✅ Easy retrieval and management

