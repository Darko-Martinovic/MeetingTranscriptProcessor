# Data Directory Structure

This directory contains the file processing folders for the Meeting Transcript Processor application.

## Directory Structure

```
data/
├── Templates/          # Sample files and templates (tracked in Git)
│   └── sample_meeting_transcript.txt
├── Incoming/           # Drop new transcript files here (ignored by Git)
├── Processing/         # Temporary processing location (ignored by Git)
└── Archive/           # Processed files with timestamps (ignored by Git)
```

## Usage

### Templates/

Contains sample transcript files and templates. These files are tracked in Git to provide examples and templates for users.

### Incoming/

Place new meeting transcript files here for automatic processing. Supported formats:

- `.txt` - Plain text files
- `.md` - Markdown files
- `.json` - JSON formatted transcripts
- `.xml` - XML formatted transcripts
- `.docx` - Microsoft Word documents
- `.pdf` - PDF documents

### Processing/

Temporary directory used during file processing. Files are briefly moved here while being analyzed and converted to Jira tickets.

### Archive/

Contains processed files with timestamps and status indicators. Files are automatically moved here after processing with names like:

- `YYYYMMDD_HHMMSS_success_original_filename.txt`
- `YYYYMMDD_HHMMSS_error_original_filename.txt`

## Git Configuration

- **Tracked**: Only `Templates/` directory and `.gitkeep` files
- **Ignored**: All content in `Incoming/`, `Processing/`, and `Archive/` directories
- **Preserved**: Directory structure is maintained with `.gitkeep` files

This ensures sensitive meeting data and temporary files are not accidentally committed to version control while preserving the required directory structure for the application to function properly.
