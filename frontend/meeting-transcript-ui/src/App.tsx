import React, { useState, useEffect, useCallback } from "react";
import {
  meetingApi,
  configurationApi,
  localStorageService,
  FolderType,
  type FolderInfo,
  type MeetingInfo,
  type MeetingTranscript,
  type SystemStatusDto,
  type MeetingFilter,
} from "./services/api";
import AppHeader from "./components/AppHeader";
import FolderSidebar from "./components/FolderSidebar";
import MainContentArea from "./components/MainContentArea";
import ErrorAlert from "./components/ErrorAlert";
import UploadModal from "./components/UploadModal";
import SettingsModal from "./components/SettingsModal";
import WorkflowModal from "./components/WorkflowModal";
import { useFolderIcons } from "./hooks/useFolderIcons";
import styles from "./App.module.css";

const App: React.FC = () => {
  const [folders, setFolders] = useState<FolderInfo[]>([]);
  const [selectedFolder, setSelectedFolder] = useState<FolderInfo | null>(null);
  const [meetings, setMeetings] = useState<MeetingInfo[]>([]);
  const [selectedMeeting, setSelectedMeeting] =
    useState<MeetingTranscript | null>(null);
  const [systemStatus, setSystemStatus] = useState<SystemStatusDto | null>(
    null
  );
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [showSettings, setShowSettings] = useState(false);
  const [showWorkflow, setShowWorkflow] = useState(false);
  const [showUpload, setShowUpload] = useState(false);
  const [favorites, setFavorites] = useState<string[]>([]);
  const [currentFilter, setCurrentFilter] = useState<MeetingFilter>({});
  const [showFilters, setShowFilters] = useState(false);

  // Use the folder icons hook
  const { getFolderIcon, getFolderHeaderIcon, getFolderButtonClass } =
    useFolderIcons();

  const loadSystemStatus = useCallback(async () => {
    try {
      const status = await configurationApi.getSystemStatus();
      setSystemStatus(status);
    } catch (err) {
      console.error("Failed to load system status:", err);
    }
  }, []);

  const loadMeetingsInFolder = useCallback(
    async (folder: FolderInfo, filter?: MeetingFilter) => {
      try {
        setLoading(true);
        const meetingsData = await meetingApi.getMeetingsInFolder(
          folder.type,
          filter
        );
        setMeetings(meetingsData);
        setSelectedFolder(folder);
        setSelectedMeeting(null);
      } catch (err) {
        setError("Failed to load meetings");
        console.error(err);
      } finally {
        setLoading(false);
      }
    },
    []
  );

  const handleFilterChange = useCallback(
    (filter: MeetingFilter) => {
      setCurrentFilter(filter);
      if (selectedFolder) {
        loadMeetingsInFolder(selectedFolder, filter);
      }
    },
    [selectedFolder, loadMeetingsInFolder]
  );

  const handleFolderSelect = useCallback(
    async (folder: FolderInfo) => {
      // Reset filters when switching folders
      const defaultFilter: MeetingFilter = {
        sortBy: "date",
        sortOrder: "desc",
      };
      setCurrentFilter(defaultFilter);
      setShowFilters(false);
      await loadMeetingsInFolder(folder, defaultFilter);
    },
    [loadMeetingsInFolder]
  );

  const updateFavoritesCount = useCallback(() => {
    setFolders((prevFolders) => {
      const updatedFolders = [...prevFolders];
      const favoritesFolder = updatedFolders.find(
        (f) => f.type === FolderType.Favorites
      );
      if (favoritesFolder) {
        const currentFavorites = localStorageService.getFavorites();
        favoritesFolder.meetingCount = currentFavorites.length;
      }
      return updatedFolders;
    });
  }, []);

  useEffect(() => {
    const loadFolders = async () => {
      try {
        setLoading(true);
        const foldersData = await meetingApi.getFolders();
        setFolders(foldersData);
      } catch (err) {
        setError("Failed to load folders");
        console.error(err);
      } finally {
        setLoading(false);
      }
    };

    loadFolders();
    loadSystemStatus();
    setFavorites(localStorageService.getFavorites());

    // Auto refresh every 30 seconds - but skip meeting list refresh when viewing details
    const interval = setInterval(() => {
      if (selectedFolder && !selectedMeeting) {
        loadMeetingsInFolder(selectedFolder, currentFilter);
      }
      loadSystemStatus();
    }, 30000);

    return () => clearInterval(interval);
  }, [
    selectedFolder,
    selectedMeeting,
    loadMeetingsInFolder,
    loadSystemStatus,
    currentFilter,
  ]);

  // Update Favorites count when folders are loaded
  useEffect(() => {
    if (folders.length > 0) {
      updateFavoritesCount();
    }
  }, [folders.length, updateFavoritesCount]);

  const loadFolders = useCallback(async () => {
    try {
      setLoading(true);
      const foldersData = await meetingApi.getFolders();

      // Update the Favorites folder count with the actual number from localStorage
      const favoritesFolder = foldersData.find(
        (f) => f.type === FolderType.Favorites
      );
      if (favoritesFolder) {
        const currentFavorites = localStorageService.getFavorites();
        favoritesFolder.meetingCount = currentFavorites.length;
      }

      setFolders(foldersData);
    } catch (err) {
      setError("Failed to load folders");
      console.error(err);
    } finally {
      setLoading(false);
    }
  }, []);

  const loadMeeting = async (meetingInfo: MeetingInfo) => {
    try {
      setLoading(true);
      const meeting = await meetingApi.getMeeting(meetingInfo.fileName);
      setSelectedMeeting(meeting);
      localStorageService.addRecentMeeting(meetingInfo.fileName);
    } catch (err) {
      setError("Failed to load meeting details");
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  const handleFileUpload = async (file: File) => {
    try {
      setLoading(true);
      await meetingApi.uploadMeeting(file);
      setShowUpload(false);
      // Refresh the incoming folder if it's selected
      const incomingFolder = folders.find(
        (f) => f.type === FolderType.Incoming
      );
      if (incomingFolder && selectedFolder?.type === FolderType.Incoming) {
        await loadMeetingsInFolder(incomingFolder);
      }
    } catch (err) {
      setError("Failed to upload file");
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  const handleMultipleFileUpload = async (files: File[]) => {
    try {
      setLoading(true);
      const uploadPromises = files.map((file) =>
        meetingApi.uploadMeeting(file)
      );
      await Promise.all(uploadPromises);
      setShowUpload(false);
      // Refresh the incoming folder if it's selected
      const incomingFolder = folders.find(
        (f) => f.type === FolderType.Incoming
      );
      if (incomingFolder && selectedFolder?.type === FolderType.Incoming) {
        await loadMeetingsInFolder(incomingFolder);
      }
    } catch (err) {
      setError("Failed to upload one or more files");
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  const toggleFavorite = async (fileName: string) => {
    const isFavorite = favorites.includes(fileName);
    if (isFavorite) {
      localStorageService.removeFavorite(fileName);
    } else {
      localStorageService.addFavorite(fileName);
    }

    const newFavorites = localStorageService.getFavorites();
    setFavorites(newFavorites);

    // Update the Favorites folder count immediately
    updateFavoritesCount();

    // If we're currently viewing the Favorites folder, refresh it
    if (selectedFolder?.type === FolderType.Favorites) {
      await loadMeetingsInFolder(selectedFolder, currentFilter);
    }
  };
  const handleEditTitle = async (fileName: string, newTitle: string) => {
    try {
      // Call the API to update meeting title
      const response = await meetingApi.updateMeetingTitle(fileName, newTitle);

      // Refresh the current folder to show updated title
      if (selectedFolder) {
        await loadMeetingsInFolder(selectedFolder, currentFilter);
      }

      // Clear any existing errors
      setError(null);
      console.log("Meeting title updated successfully:", response.title);
    } catch (err) {
      const errorMessage =
        err instanceof Error &&
        "response" in err &&
        typeof err.response === "object" &&
        err.response &&
        "data" in err.response &&
        typeof err.response.data === "object" &&
        err.response.data &&
        "error" in err.response.data
          ? String(err.response.data.error)
          : "Failed to update meeting title";
      setError(errorMessage);
      console.error("Error updating meeting title:", err);
    }
  };

  const handleMoveToArchive = async (fileName: string) => {
    try {
      // Call the API to move meeting to archive
      const response = await meetingApi.moveMeeting(
        fileName,
        FolderType.Archive
      );

      // Refresh the current folder to reflect the change
      if (selectedFolder) {
        await loadMeetingsInFolder(selectedFolder, currentFilter);
      }

      // Also refresh the folder counts
      await loadFolders();

      // Clear any existing errors
      setError(null);
      console.log("Meeting moved to archive successfully:", response.fileName);
    } catch (err) {
      const errorMessage =
        err instanceof Error &&
        "response" in err &&
        typeof err.response === "object" &&
        err.response &&
        "data" in err.response &&
        typeof err.response.data === "object" &&
        err.response.data &&
        "error" in err.response.data
          ? String(err.response.data.error)
          : "Failed to move meeting to archive";
      setError(errorMessage);
      console.error("Error moving meeting to archive:", err);
    }
  };

  const handleMoveToIncoming = async (fileName: string) => {
    try {
      // Call the API to move meeting to incoming
      const response = await meetingApi.moveMeeting(
        fileName,
        FolderType.Incoming
      );

      // Refresh the current folder to reflect the change
      if (selectedFolder) {
        await loadMeetingsInFolder(selectedFolder, currentFilter);
      }

      // Also refresh the folder counts
      await loadFolders();

      // Clear any existing errors
      setError(null);
      console.log("Meeting moved to incoming successfully:", response.fileName);
    } catch (err) {
      const errorMessage =
        err instanceof Error &&
        "response" in err &&
        typeof err.response === "object" &&
        err.response &&
        "data" in err.response &&
        typeof err.response.data === "object" &&
        err.response.data &&
        "error" in err.response.data
          ? String(err.response.data.error)
          : "Failed to move meeting to incoming";
      setError(errorMessage);
      console.error("Error moving meeting to incoming:", err);
    }
  };

  const handleDeleteMeeting = async (fileName: string) => {
    try {
      await meetingApi.deleteMeeting(fileName);
      console.log("Meeting deleted successfully:", fileName);
      // Refresh the current folder after successful deletion
      if (selectedFolder) {
        await loadMeetingsInFolder(selectedFolder, currentFilter);
      }
    } catch (err) {
      setError("Failed to delete meeting");
      console.error(err);
    }
  };

  return (
    <div className={styles.container}>
      <AppHeader
        selectedFolder={selectedFolder}
        systemStatus={systemStatus}
        loading={loading}
        onShowUpload={() => setShowUpload(true)}
        onShowSettings={() => setShowSettings(true)}
        onShowWorkflow={() => setShowWorkflow(true)}
        onRefresh={loadFolders}
        getFolderHeaderIcon={getFolderHeaderIcon}
      />

      <div className={styles.main}>
        <ErrorAlert error={error} onClose={() => setError(null)} />

        <div className={styles.layoutGrid}>
          <FolderSidebar
            folders={folders}
            selectedFolder={selectedFolder}
            onFolderSelect={handleFolderSelect}
            getFolderIcon={getFolderIcon}
            getFolderButtonClass={getFolderButtonClass}
          />

          <MainContentArea
            selectedFolder={selectedFolder}
            selectedMeeting={selectedMeeting}
            meetings={meetings}
            showFilters={showFilters}
            currentFilter={currentFilter}
            favorites={favorites}
            onToggleFilters={() => setShowFilters(!showFilters)}
            onFilterChange={handleFilterChange}
            onSelectMeeting={loadMeeting}
            onBackFromMeeting={() => setSelectedMeeting(null)}
            onToggleFavorite={toggleFavorite}
            onEditTitle={handleEditTitle}
            onMoveToArchive={handleMoveToArchive}
            onMoveToIncoming={handleMoveToIncoming}
            onDeleteMeeting={handleDeleteMeeting}
          />
        </div>
      </div>

      {/* Upload Modal */}
      {showUpload && (
        <UploadModal
          onClose={() => setShowUpload(false)}
          onUpload={handleFileUpload}
          onMultipleUpload={handleMultipleFileUpload}
          loading={loading}
        />
      )}

      {/* Settings Modal */}
      {showSettings && <SettingsModal onClose={() => setShowSettings(false)} />}

      {/* Workflow Modal */}
      {showWorkflow && <WorkflowModal onClose={() => setShowWorkflow(false)} />}
    </div>
  );
};

export default App;
