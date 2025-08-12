import axios from "axios";

const API_BASE_URL = "http://localhost:5000/api";

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    "Content-Type": "application/json",
  },
});

// Types
export interface FolderInfo {
  name: string;
  path: string;
  type: FolderType;
  meetingCount: number;
}

export interface MeetingInfo {
  fileName: string;
  originalName: string;
  title: string;
  previewContent: string;
  size: number;
  lastModified: string;
  folderType: FolderType;
  status: string;
  language?: string;
  participants?: string[];
  hasJiraTickets?: boolean;
  actionItemCount?: number;
  processingDate?: string;
}

export interface MeetingTranscript {
  id: string;
  fileName: string;
  title: string;
  meetingDate: string;
  content: string;
  participants: string[];
  projectKey?: string;
  detectedLanguage: string;
  processedAt: string;
  processedBy: string;
  actionItems: ActionItem[];
  createdJiraTickets: JiraTicketReference[];
  status: TranscriptStatus;
}

export interface JiraTicketReference {
  ticketKey: string;
  ticketUrl: string;
  title: string;
  actionItemId: string;
  createdAt: string;
  priority: string;
  type: string;
  assignedTo?: string;
  status: string;
}

export interface ActionItem {
  id: string;
  description: string;
  assignee?: string;
  dueDate?: string;
  priority: string;
  status: string;
  extractedFromContext: string;
}

export interface ConfigurationDto {
  azureOpenAI: AzureOpenAIDto;
  extraction: ExtractionDto;
  environment: EnvironmentDto;
}

export interface AzureOpenAIDto {
  endpoint: string;
  deploymentName: string;
  apiVersion: string;
  isConfigured: boolean;
}

export interface ExtractionDto {
  maxConcurrentFiles: number;
  validationConfidenceThreshold: number;
  enableValidation: boolean;
  enableHallucinationDetection: boolean;
  enableConsistencyManagement: boolean;
}

export interface EnvironmentDto {
  incomingDirectory?: string;
  processingDirectory?: string;
  archiveDirectory?: string;
  jiraUrl?: string;
  jiraEmail?: string;
  jiraDefaultProject?: string;
  isJiraConfigured: boolean;
}

export interface SystemStatusDto {
  isRunning: boolean;
  azureOpenAIConfigured: boolean;
  jiraConfigured: boolean;
  validationEnabled: boolean;
  hallucinationDetectionEnabled: boolean;
  consistencyManagementEnabled: boolean;
  currentTime: string;
}

export const FolderType = {
  Archive: 0,
  Incoming: 1,
  Processing: 2,
  Recent: 3,
  Favorites: 4,
} as const;

export type FolderType = (typeof FolderType)[keyof typeof FolderType];

export const TranscriptStatus = {
  New: 0,
  Processing: 1,
  Processed: 2,
  Error: 3,
  Archived: 4,
} as const;

export type TranscriptStatus =
  (typeof TranscriptStatus)[keyof typeof TranscriptStatus];

export interface MeetingFilter {
  searchText?: string;
  status?: string[];
  language?: string[];
  participants?: string[];
  dateFrom?: string;
  dateTo?: string;
  hasJiraTickets?: boolean;
  sortBy?: "date" | "title" | "size" | "status" | "language" | "participants";
  sortOrder?: "asc" | "desc";
}

export interface AppSettings {
  theme: "light" | "dark";
  autoRefresh: boolean;
  refreshInterval: number;
}

// API functions
export const meetingApi = {
  getFolders: async (): Promise<FolderInfo[]> => {
    const response = await api.get("/meetings/folders");
    return response.data;
  },

  getMeetingsInFolder: async (
    folderType: FolderType,
    filter?: MeetingFilter
  ): Promise<MeetingInfo[]> => {
    const params = new URLSearchParams();
    if (filter) {
      if (filter.searchText) params.append("search", filter.searchText);
      if (filter.status?.length)
        params.append("status", filter.status.join(","));
      if (filter.language?.length)
        params.append("language", filter.language.join(","));
      if (filter.participants?.length)
        params.append("participants", filter.participants.join(","));
      if (filter.dateFrom) params.append("dateFrom", filter.dateFrom);
      if (filter.dateTo) params.append("dateTo", filter.dateTo);
      if (filter.hasJiraTickets !== undefined)
        params.append("hasJiraTickets", filter.hasJiraTickets.toString());
      if (filter.sortBy) params.append("sortBy", filter.sortBy);
      if (filter.sortOrder) params.append("sortOrder", filter.sortOrder);
    }

    // For Favorites folder, add favorite file names as a parameter
    if (folderType === FolderType.Favorites) {
      const favorites = localStorageService.getFavorites();
      if (favorites.length > 0) {
        params.append("favoriteFileNames", favorites.join(","));
      }
    }

    const queryString = params.toString();
    const url = `/meetings/folders/${folderType}/meetings${
      queryString ? `?${queryString}` : ""
    }`;
    const response = await api.get(url);
    return response.data;
  },

  getMeeting: async (fileName: string): Promise<MeetingTranscript> => {
    const response = await api.get(
      `/meetings/meeting/${encodeURIComponent(fileName)}`
    );
    return response.data;
  },

  getMeetingJiraTickets: async (fileName: string): Promise<JiraTicketReference[]> => {
    const response = await api.get(
      `/meetings/meeting/${encodeURIComponent(fileName)}/tickets`
    );
    return response.data;
  },

  uploadMeeting: async (
    file: File
  ): Promise<{ message: string; fileName: string }> => {
    const formData = new FormData();
    formData.append("file", file);
    const response = await api.post("/meetings/upload", formData, {
      headers: {
        "Content-Type": "multipart/form-data",
      },
    });
    return response.data;
  },

  deleteMeeting: async (fileName: string): Promise<{ message: string }> => {
    const response = await api.delete(
      `/meetings/meeting/${encodeURIComponent(fileName)}`
    );
    return response.data;
  },

  updateMeetingTitle: async (
    fileName: string,
    title: string
  ): Promise<{ message: string; title: string }> => {
    const response = await api.put(
      `/meetings/meeting/${encodeURIComponent(fileName)}/title`,
      { title }
    );
    return response.data;
  },

  moveMeeting: async (
    fileName: string,
    targetFolderType: FolderType
  ): Promise<{
    message: string;
    fileName: string;
    targetFolder: FolderType;
  }> => {
    const response = await api.put(
      `/meetings/meeting/${encodeURIComponent(fileName)}/move`,
      { targetFolderType }
    );
    return response.data;
  },
};

export const configurationApi = {
  getConfiguration: async (): Promise<ConfigurationDto> => {
    const response = await api.get("/configuration");
    return response.data;
  },

  updateAzureOpenAI: async (config: {
    endpoint?: string;
    apiKey?: string;
    deploymentName?: string;
  }) => {
    const response = await api.put("/configuration/azure-openai", config);
    return response.data;
  },

  updateExtraction: async (config: {
    maxConcurrentFiles?: number;
    validationConfidenceThreshold?: number;
    enableValidation?: boolean;
    enableHallucinationDetection?: boolean;
    enableConsistencyManagement?: boolean;
  }) => {
    const response = await api.put("/configuration/extraction", config);
    return response.data;
  },

  updateJira: async (config: {
    url?: string;
    email?: string;
    apiToken?: string;
    defaultProject?: string;
  }) => {
    const response = await api.put("/configuration/jira", config);
    return response.data;
  },

  getSystemStatus: async (): Promise<SystemStatusDto> => {
    const response = await api.get("/configuration/system-status");
    return response.data;
  },
};

// Local storage utilities
export const localStorageService = {
  getFavorites: (): string[] => {
    const favorites = localStorage.getItem("meeting-favorites");
    return favorites ? JSON.parse(favorites) : [];
  },

  addFavorite: (fileName: string): void => {
    const favorites = localStorageService.getFavorites();
    if (!favorites.includes(fileName)) {
      favorites.push(fileName);
      localStorage.setItem("meeting-favorites", JSON.stringify(favorites));
    }
  },

  removeFavorite: (fileName: string): void => {
    const favorites = localStorageService.getFavorites();
    const updated = favorites.filter((f) => f !== fileName);
    localStorage.setItem("meeting-favorites", JSON.stringify(updated));
  },

  getRecentMeetings: (): string[] => {
    const recent = localStorage.getItem("recent-meetings");
    return recent ? JSON.parse(recent) : [];
  },

  addRecentMeeting: (fileName: string): void => {
    const recent = localStorageService.getRecentMeetings();
    const updated = [fileName, ...recent.filter((f) => f !== fileName)].slice(
      0,
      10
    );
    localStorage.setItem("recent-meetings", JSON.stringify(updated));
  },

  getSettings: (): AppSettings => {
    const settings = localStorage.getItem("app-settings");
    return settings
      ? JSON.parse(settings)
      : {
          theme: "light" as const,
          autoRefresh: true,
          refreshInterval: 30000,
        };
  },

  saveSettings: (settings: AppSettings) => {
    localStorage.setItem("app-settings", JSON.stringify(settings));
  },
};
