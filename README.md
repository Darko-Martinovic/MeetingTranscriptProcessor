# Meeting Transcript Processor

A web application that reads meeting transcripts, finds the action items with Azure OpenAI (or with rules when no model is configured), and creates Jira tickets for them.

[![CI](https://github.com/Darko-Martinovic/MeetingTranscriptProcessor/actions/workflows/ci.yml/badge.svg)](https://github.com/Darko-Martinovic/MeetingTranscriptProcessor/actions/workflows/ci.yml)

## ✨ Key Features

- 🤖 **AI extraction** with Azure OpenAI, checked against rule-based extraction and for hallucinations
- 🎫 **Jira integration**: a ticket per action item, or simulated tickets when Jira isn't configured
- 🖥️ **Web UI**: React frontend with folders, filters, favourites and batch upload
- 📄 **Formats**: .txt, .md, .json (including the MS Teams / Graph layout), .xml, .docx, .pdf and .vtt (MS Teams recordings)
- ⚙️ **Offline mode**: without a model, the action items are found with rules
- 🎯 **Context-aware**: adapts the prompt to the meeting type (standup, sprint, incident...) and language (English, French, Dutch)
- 💰 **Token usage and cost** of every meeting

## 🎬 Demo Video

https://github.com/user-attachments/assets/a66fb21f-1d1f-48f7-9840-7bbb8113e0d9

### Latest Features Demo

https://github.com/user-attachments/assets/12b2ecf6-cfd0-4797-958a-8018b7a732e4

1. **Train AI**: try the extraction on a transcript at a chosen temperature and turn feedback into a custom prompt
2. **Dutch** as an input language
3. **Token usage and cost** on each meeting card
4. **VTT and DOCX files** (native MS Teams transcripts)

The videos show version 1, which is kept under the [`v1`](https://github.com/Darko-Martinovic/MeetingTranscriptProcessor/tree/v1) tag.

## 🚀 Quick Start

Prerequisites: the .NET 10 SDK and Node.js 20 or later. Azure OpenAI and Jira are optional.

```powershell
git clone https://github.com/Darko-Martinovic/MeetingTranscriptProcessor.git
cd MeetingTranscriptProcessor
Copy-Item .env.example .env      # then fill in what you have, or leave the credentials out

# Backend, from the repository root (it reads .env, config/ and data/ from there)
dotnet run

# Frontend, in a second terminal
cd frontend/meeting-transcript-ui
npm install
npm run dev
```

Open http://localhost:5175. The API listens on http://localhost:5100/api/v1.

## 📁 How It Works

```
Upload → data/Incoming → data/Processing → model or rules → Jira → data/Archive
```

1. A file uploaded on the web page, or copied into `data/Incoming`, is picked up by the background worker. Several files are processed at a time (3 by default).
2. The file is read as text with its title, date, participants and Jira project.
3. The model extracts the action items. With validation on, they are compared with what the rules find; with hallucination detection on, items the transcript doesn't support are dropped. Similar items are merged.
4. A Jira ticket is created for each item that needs one. If Jira refuses a ticket, the meeting is archived with the `error` status.
5. The file is archived as `yyyyMMdd_HHmmss_status_Language_name`, with what was found saved beside it in `name.meta.json`.

## ⚙️ Configuration

Everything is optional: with no credentials, the rules find the action items and the tickets are simulated.

### `.env`

| Setting | What it does |
|---|---|
| `AOAI_ENDPOINT`, `AOAI_APIKEY`, `CHATCOMPLETION_DEPLOYMENTNAME` | The Azure OpenAI resource, its key and the deployment. The app calls the resource's v1 API (`<endpoint>/openai/v1/`), so no API version is needed |
| `AI_TEMPERATURE`, `AI_TOP_P`, `AI_MAX_TOKENS`, `AI_SYSTEM_PROMPT` | Model settings, also editable on the Settings page |
| `AI_INPUT_PRICE_PER_1K`, `AI_OUTPUT_PRICE_PER_1K` | Prices in dollars of 1,000 tokens, for the cost shown with each meeting (gpt-4o's by default) |
| `JIRA_URL`, `JIRA_EMAIL`, `JIRA_API_TOKEN`, `JIRA_PROJECT_KEY` | Jira Cloud and the default project. A transcript that names a project (`Project: ABC`, or an issue key such as `ABC-123`) uses that one |
| `ENABLE_VALIDATION`, `ENABLE_HALLUCINATION_DETECTION`, `ENABLE_CONSISTENCY_MANAGEMENT`, `VALIDATION_CONFIDENCE_THRESHOLD`, `MAX_CONCURRENT_FILES` | The checks on what the model finds and how many files are processed at a time |
| `INCOMING_DIRECTORY`, `PROCESSING_DIRECTORY`, `ARCHIVE_DIRECTORY` | The folders, `data/...` by default; relative paths start from the folder the app runs in |
| `API_KEY`, `CORS_ORIGINS` | Reaching the API from other machines (below) |
| `EXTRACTION_*`, `MEETING_*` | The keywords of the rule-based extraction and the meeting type detection |

The credentials are read from `.env` only. The Settings page shows them but can't change them, and the API key is never written to the files in `config/`.

### The Settings page

The Settings page changes the model settings (deployment, temperature, max tokens, top P) and the processing settings (the checks, the confidence threshold, files at a time). They are saved in `config/azure-openai.json` and `config/processing.json` and apply to the next file. A value set in `.env` wins when the app starts.

### Reaching the API from other machines

The backend and the Vite dev server listen on this machine only. To serve other machines:

1. Set `API_KEY` in `.env`. Without it, the backend refuses to start on an address other machines can reach, such as `ASPNETCORE_URLS=http://0.0.0.0:5100`. With it, every `/api` request must send the key in the `X-Api-Key` header.
2. Put the same value in `VITE_API_KEY` in `frontend/meeting-transcript-ui/.env`, and list the frontend's origin in `CORS_ORIGINS`.
3. Start the frontend with `npm run dev -- --host`.

Anyone who can open the frontend can read the key from it, so serve it only to people you trust.

## 🏗️ Architecture

- **Backend** (.NET 10, ASP.NET Core): `Program.cs` hosts the API and `TranscriptWorker`, the background service that processes incoming files.
  - `TranscriptReader` reads every format.
  - `TranscriptProcessorService` runs the extraction and the checks, with `ConsistencyManager`, `ActionItemValidator` and `HallucinationDetector`.
  - `AzureOpenAIService` calls the model through Microsoft.Extensions.AI.
  - `JiraTicketService` creates the tickets.
  - `MeetingStore` manages the folders and the metadata.
- **Frontend** (React 19, TypeScript, Vite): folder navigation, filters, batch upload, favourites, the Settings page and the Train AI page. `src/services/api.ts` is the API client.
- **Tests**: xUnit v3 in `tests/`. They run the app on temporary folders with a fake model and a fake Jira, and never call Azure OpenAI or Jira.

## 📖 API

All routes are under `/api/v1`.

| Route | What it does |
|---|---|
| `GET meetings/folders` | The folders with their meeting counts |
| `GET meetings/folders/{Archive\|Incoming\|Processing\|Recent\|Favorites}/meetings` | The meetings in a folder; filters: `search`, `status`, `language`, `participants`, `dateFrom`, `dateTo`, `hasJiraTickets`, `sortBy`, `sortOrder`, `favoriteFileNames` |
| `GET meetings/meeting/{fileName}` | A meeting with its action items and tickets |
| `GET meetings/meeting/{fileName}/tickets` | Its Jira tickets |
| `POST meetings/upload` | Upload a transcript (form field `file`) |
| `PUT meetings/meeting/{fileName}/title`, `PUT .../move`, `DELETE meetings/meeting/{fileName}` | Rename, move or delete a meeting |
| `GET processing/queue`, `GET processing/status/{id}`, `DELETE processing/completed` | The processing queue |
| `GET configuration`, `GET configuration/system-status` | Settings and status |
| `PUT configuration/azure-openai`, `PUT configuration/extraction`, `PUT configuration/prompts` | Model settings, processing settings, custom prompt |
| `POST training/process` | Extract the action items of a transcript at a given `temperature` without archiving it or creating tickets |

The Postman collection in `postman/` has every request.

## 🛠️ Development

```powershell
dotnet build
dotnet test                 # never calls Azure OpenAI or Jira

cd frontend/meeting-transcript-ui
npm run lint
npm run build
```

GitHub Actions runs the same checks on every pull request. Sample transcripts in every format are in `data/Templates`.

## 📄 License

MIT. See [LICENSE](LICENSE).

## ⚠️ Disclaimer

This project was developed independently on personal equipment and in personal time.
It is not affiliated with, endorsed by, or derived from the intellectual property of EPAM Systems or any of its clients.
All examples, configurations, and data are generic and intended solely for demonstration and educational purposes.
