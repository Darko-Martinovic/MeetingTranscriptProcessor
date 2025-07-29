import React, { useState, useEffect, useCallback, useMemo } from "react";
import {
  FolderOpen,
  File,
  Settings,
  Upload,
  RefreshCw,
  Clock,
  Archive,
  Inbox,
  Clock3,
  Folder,
} from "lucide-react";
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
import MeetingFilterComponent from "./components/MeetingFilter";
import MeetingCard from "./components/MeetingCard";
import MeetingDetails from "./components/MeetingDetails";
import UploadModal from "./components/UploadModal";
import SettingsModal from "./components/SettingsModal";
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
  const [showUpload, setShowUpload] = useState(false);
  const [favorites, setFavorites] = useState<string[]>([]);
  const [currentFilter, setCurrentFilter] = useState<MeetingFilter>({});
  const [showFilters, setShowFilters] = useState(false);

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

    // Auto refresh every 30 seconds
    const interval = setInterval(() => {
      if (selectedFolder) {
        loadMeetingsInFolder(selectedFolder, currentFilter);
      }
      loadSystemStatus();
    }, 30000);

    return () => clearInterval(interval);
  }, [selectedFolder, loadMeetingsInFolder, loadSystemStatus, currentFilter]);

  const loadFolders = useCallback(async () => {
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

  const toggleFavorite = (fileName: string) => {
    const isFavorite = favorites.includes(fileName);
    if (isFavorite) {
      localStorageService.removeFavorite(fileName);
    } else {
      localStorageService.addFavorite(fileName);
    }
    setFavorites(localStorageService.getFavorites());
  };
  const handleEditTitle = async (fileName: string, newTitle: string) => {
    try {
      // TODO: Implement API call to update meeting title
      console.log("Editing title for", fileName, "to", newTitle);
      // For now, just refresh the current folder
      if (selectedFolder) {
        await loadMeetingsInFolder(selectedFolder, currentFilter);
      }
    } catch (err) {
      setError("Failed to update meeting title");
      console.error(err);
    }
  };

  const handleMoveToArchive = async (fileName: string) => {
    try {
      // TODO: Implement API call to move meeting to archive
      console.log("Moving to archive:", fileName);
      // For now, just refresh the current folder
      if (selectedFolder) {
        await loadMeetingsInFolder(selectedFolder, currentFilter);
      }
    } catch (err) {
      setError("Failed to move meeting to archive");
      console.error(err);
    }
  };

  const handleMoveToIncoming = async (fileName: string) => {
    try {
      // TODO: Implement API call to move meeting to incoming
      console.log("Moving to incoming:", fileName);
      // For now, just refresh the current folder
      if (selectedFolder) {
        await loadMeetingsInFolder(selectedFolder, currentFilter);
      }
    } catch (err) {
      setError("Failed to move meeting to incoming");
      console.error(err);
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

  // Memoized folder icon functions
  const getFolderIcon = useCallback((folderName: string) => {
    switch (folderName.toLowerCase()) {
      case "archive":
        return (
          <Archive
            className={`${styles.folderIcon} ${styles.folderIconArchive}`}
          />
        );
      case "incoming":
        return (
          <Inbox
            className={`${styles.folderIcon} ${styles.folderIconIncoming}`}
          />
        );
      case "processing":
        return (
          <Clock3
            className={`${styles.folderIcon} ${styles.folderIconProcessing}`}
          />
        );
      case "recent":
        return (
          <Clock
            className={`${styles.folderIcon} ${styles.folderIconOutgoing}`}
          />
        );
      default:
        return (
          <Folder
            className={`${styles.folderIcon} ${styles.folderIconGeneral}`}
          />
        );
    }
  }, []);

  const getFolderHeaderIcon = useCallback((folderName: string) => {
    switch (folderName.toLowerCase()) {
      case "archive":
        return (
          <Archive
            className={`${styles.appIcon} ${styles.folderIconArchive}`}
          />
        );
      case "incoming":
        return (
          <Inbox className={`${styles.appIcon} ${styles.folderIconIncoming}`} />
        );
      case "processing":
        return (
          <Clock3
            className={`${styles.appIcon} ${styles.folderIconProcessing}`}
          />
        );
      case "recent":
        return (
          <Clock className={`${styles.appIcon} ${styles.folderIconOutgoing}`} />
        );
      default:
        return (
          <FolderOpen
            className={`${styles.appIcon} ${styles.folderIconIncoming}`}
          />
        );
    }
  }, []);

  // Memoized status dot class
  const statusDotClass = useMemo(() => {
    return `${styles.statusDot} ${
      systemStatus?.isRunning ? styles.statusDotOnline : styles.statusDotOffline
    }`;
  }, [systemStatus?.isRunning]);

  // Memoized folder button class function
  const getFolderButtonClass = useCallback(
    (_folder: FolderInfo, isSelected: boolean) => {
      return `${styles.folderButton} ${
        isSelected ? styles.folderButtonActive : styles.folderButtonInactive
      }`;
    },
    []
  );

  return (
    <div className={styles.container}>
      {/* Header */}
      <header className={styles.header}>
        <div className={styles.headerContainer}>
          <div className={styles.headerContent}>
            <div className={styles.headerLeft}>
              {selectedFolder ? (
                getFolderHeaderIcon(selectedFolder.name)
              ) : (
                <FolderOpen className={styles.appIcon} />
              )}
              <h1 className={styles.appTitle}>Meeting Transcript Processor</h1>
            </div>
            <div className={styles.headerRight}>
              {systemStatus && (
                <div className={styles.statusIndicator}>
                  <div className={statusDotClass}></div>
                  <span className={styles.statusText}>
                    {systemStatus.isRunning ? "Running" : "Offline"}
                  </span>
                </div>
              )}
              <button
                onClick={() => setShowUpload(true)}
                className={styles.uploadButton}
              >
                <Upload className="h-4 w-4" />
                <span>Upload</span>
              </button>
              <button
                onClick={() => setShowSettings(true)}
                className={styles.iconButton}
              >
                <Settings className="h-5 w-5" />
              </button>
              <button
                onClick={loadFolders}
                className={`${styles.iconButton} ${
                  loading ? styles.iconButtonLoading : ""
                }`}
                disabled={loading}
              >
                <RefreshCw
                  className={`h-5 w-5 ${loading ? styles.spinIcon : ""}`}
                />
              </button>
            </div>
          </div>
        </div>
      </header>

      {/* Main Content */}
      <div className={styles.main}>
        {error && (
          <div className={styles.errorAlert}>
            {error}
            <button
              onClick={() => setError(null)}
              className={styles.errorCloseButton}
            >
              ×
            </button>
          </div>
        )}

        <div className={styles.layoutGrid}>
          {/* Sidebar - Folders */}
          <div className={styles.sidebarColumn}>
            <div className={styles.sidebar}>
              <h2 className={styles.sidebarTitle}>Folders</h2>
              <div className={styles.folderList}>
                {folders.map((folder) => (
                  <button
                    key={folder.name}
                    onClick={() => handleFolderSelect(folder)}
                    className={getFolderButtonClass(
                      folder,
                      selectedFolder?.name === folder.name
                    )}
                  >
                    <div className={styles.folderContent}>
                      <div className={styles.folderInfo}>
                        {getFolderIcon(folder.name)}
                        <span className={styles.folderName}>{folder.name}</span>
                      </div>
                      <span className={styles.folderCount}>
                        {folder.meetingCount}
                      </span>
                    </div>
                  </button>
                ))}
              </div>
            </div>
          </div>

          {/* Main Content Area */}
          <div className={styles.contentColumn}>
            {!selectedFolder ? (
              <div className={styles.emptyState}>
                <FolderOpen className={styles.emptyIcon} />
                <h3 className={styles.emptyTitle}>
                  Select a folder to view meetings
                </h3>
                <p className={styles.emptyDescription}>
                  Choose a folder from the sidebar to see all processed
                  meetings.
                </p>
              </div>
            ) : !selectedMeeting ? (
              <div className={styles.contentArea}>
                <div className={styles.meetingList}>
                  <div className={styles.meetingListHeader}>
                    <h2 className={styles.meetingListTitle}>
                      {selectedFolder.name} ({meetings.length} meetings)
                    </h2>
                  </div>

                  {/* Show filter component only for Archive folder */}
                  {selectedFolder.type === FolderType.Archive && (
                    <MeetingFilterComponent
                      onFilterChange={handleFilterChange}
                      meetings={meetings}
                      isVisible={showFilters}
                      onToggleVisibility={() => setShowFilters(!showFilters)}
                    />
                  )}

                  <div className={styles.meetingGrid}>
                    {meetings.length === 0 ? (
                      <div className={styles.emptyState}>
                        <File className={styles.emptyIcon} />
                        <h3 className={styles.emptyTitle}>No meetings found</h3>
                        <p className={styles.emptyDescription}>
                          This folder doesn't contain any meeting files yet.
                        </p>
                      </div>
                    ) : (
                      meetings.map((meeting) => (
                        <MeetingCard
                          key={meeting.fileName}
                          meeting={meeting}
                          onSelect={loadMeeting}
                          onToggleFavorite={toggleFavorite}
                          isFavorite={favorites.includes(meeting.fileName)}
                          onEditTitle={handleEditTitle}
                          onMoveToArchive={handleMoveToArchive}
                          onMoveToIncoming={handleMoveToIncoming}
                          onDelete={handleDeleteMeeting}
                          currentFolder={selectedFolder?.name}
                        />
                      ))
                    )}
                  </div>
                </div>
              </div>
            ) : (
              <MeetingDetails
                meeting={selectedMeeting}
                onBack={() => setSelectedMeeting(null)}
                onToggleFavorite={() =>
                  toggleFavorite(selectedMeeting.fileName)
                }
                isFavorite={favorites.includes(selectedMeeting.fileName)}
              />
            )}
          </div>
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
    </div>
  );
};

export default App;
