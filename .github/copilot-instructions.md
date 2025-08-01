# Copilot Instructions for MeetingTranscriptProcessor

## Project Overview

MeetingTranscriptProcessor is an intelligent, full-stack application for processing meeting transcripts, extracting action items (using Azure OpenAI or rule-based fallback), and creating Jira tickets. It features a modern React+TypeScript frontend and a .NET 9 backend, with robust file/folder management and advanced filtering.

## Architecture & Data Flow

- **Backend (.NET 9, ASP.NET Core)**

  - `Controllers/`: Web API endpoints for meetings, configuration, and status.
  - `Services/`: Core logic (file watching, transcript parsing, AI analysis, Jira integration, validation).
  - `Models/`: Data contracts (MeetingTranscript, ActionItem, FolderInfo, etc.).
  - **File Flow:** `data/Incoming` → `data/Processing` → AI/Rule Extraction → Jira → `data/Archive`.
  - **AI/ML:** Azure OpenAI for extraction, with cross-validation, hallucination detection, and context-aware prompts.

- **Frontend (React 18, TypeScript, Vite)**
  - `src/components/`: Modular, memoized components (e.g., `MeetingDetails.tsx`, `MeetingFilter`, `AppHeader`, `WorkflowModal`).
  - `src/services/api.ts`: Axios-based API client, typed DTOs.
  - **UI Patterns:** Folder-based navigation (Archive, Incoming, Processing, Recent), advanced filtering, batch upload, favorites, and settings modals.
  - **Styling:** CSS Modules, pastel color palette, Lucide icons, responsive design.

## Developer Workflows

- **Build/Run**

  - Backend: `dotnet run --web` (dev: `dotnet watch run --web`)
  - Frontend: `cd frontend/meeting-transcript-ui && npm install && npm run dev`
  - Tests: `dotnet test`
  - Production: `dotnet publish -c Release`, `npm run build`

- **Configuration**

  - All settings via `.env` (see `.env.example`). Supports full AI, hybrid, and offline modes.
  - No credentials: rule-based fallback, simulated Jira.

- **API Endpoints**
  - Meetings: `/api/meetings/folders`, `/api/meetings/folders/{folderType}/meetings`, `/api/meetings/upload`
  - Config: `/api/configuration`, `/api/configuration/azure-openai`, `/api/configuration/jira`
  - Status: `/api/status`

## Project-Specific Conventions

- **Folder Structure:** All meeting files are managed in `data/` subfolders. Folder types are strictly enforced in both backend and frontend.
- **DTOs:** All API responses are strongly typed; update `api.ts` and backend models in sync.
- **Component Patterns:** Use memoized React components for performance. Favor composition and clear prop interfaces.
- **Styling:** Use CSS Modules for all components. Pastel color palette for UI consistency.
- **Favorites:** Starred meetings are managed via a dedicated favorites system, surfaced in both UI and API.
- **Workflow Visualization:** The workflow modal (see `WorkflowModal.tsx`) visually documents the end-to-end process and should be updated if the backend flow changes.

## Integration Points

- **Azure OpenAI:** Optional, enables advanced extraction. Fallback to rule-based if not configured.
- **Jira:** Optional, enables real ticket creation. Fallback to simulation if not configured.
- **Frontend/Backend Contract:** All folder, meeting, and action item operations are API-driven; keep DTOs and endpoints in sync.

## Examples

- To add a new folder type, update both backend enums/models and frontend folder navigation.
- To add a new AI validation step, implement in backend `Services/`, expose via API, and surface in frontend metrics.
- To extend filtering, update backend query logic and frontend filter UI in `MeetingFilter`.

## Key Files

- `MeetingTranscriptProcessor/README.md`: Full architecture, setup, and workflow details.
- `frontend/meeting-transcript-ui/src/components/`: All React UI components.
- `frontend/meeting-transcript-ui/src/services/api.ts`: API client and DTOs.
- `Services/`: Backend business logic.
- `Models/`: Shared data contracts.
