import React from "react";
import { FolderOpen, File } from "lucide-react";
import {
  FolderType,
  type FolderInfo,
  type MeetingInfo,
  type MeetingTranscript,
  type MeetingFilter,
} from "../services/api";
import MeetingListHeader from "./MeetingListHeader";
import MeetingFilterComponent from "./MeetingFilter";
import MeetingCard from "./meeting/MeetingCard";
import MeetingDetails from "./MeetingDetails";
import styles from "./MainContentArea.module.css";

interface MainContentAreaProps {
  selectedFolder: FolderInfo | null;
  selectedMeeting: MeetingTranscript | null;
  meetings: MeetingInfo[];
  showFilters: boolean;
  currentFilter: MeetingFilter;
  favorites: string[];
  onToggleFilters: () => void;
  onFilterChange: (filter: MeetingFilter) => void;
  onSelectMeeting: (meeting: MeetingInfo) => void;
  onBackFromMeeting: () => void;
  onToggleFavorite: (fileName: string) => void;
  onEditTitle: (fileName: string, newTitle: string) => void;
  onMoveToArchive: (fileName: string) => void;
  onMoveToIncoming: (fileName: string) => void;
  onDeleteMeeting: (fileName: string) => void;
}

const MainContentArea: React.FC<MainContentAreaProps> = ({
  selectedFolder,
  selectedMeeting,
  meetings,
  showFilters,
  currentFilter,
  favorites,
  onToggleFilters,
  onFilterChange,
  onSelectMeeting,
  onBackFromMeeting,
  onToggleFavorite,
  onEditTitle,
  onMoveToArchive,
  onMoveToIncoming,
  onDeleteMeeting,
}) => {
  if (!selectedFolder) {
    return (
      <div className={styles.contentColumn}>
        <div className={styles.emptyState}>
          <FolderOpen className={styles.emptyIcon} />
          <h3 className={styles.emptyTitle}>
            Select a folder to view meetings
          </h3>
          <p className={styles.emptyDescription}>
            Choose a folder from the sidebar to see all processed meetings.
          </p>
        </div>
      </div>
    );
  }

  if (selectedMeeting) {
    return (
      <div className={styles.contentColumn}>
        <MeetingDetails
          meeting={selectedMeeting}
          onBack={onBackFromMeeting}
          onToggleFavorite={() => onToggleFavorite(selectedMeeting.fileName)}
          isFavorite={favorites.includes(selectedMeeting.fileName)}
        />
      </div>
    );
  }

  return (
    <div className={styles.contentColumn}>
      <div className={styles.contentArea}>
        <div className={styles.meetingList}>
          <MeetingListHeader
            selectedFolder={selectedFolder}
            meetingCount={meetings.length}
            showFilters={showFilters}
            onToggleFilters={onToggleFilters}
            currentFilter={currentFilter}
          />

          {/* Show filter component only for Archive folder */}
          {selectedFolder.type === FolderType.Archive && (
            <MeetingFilterComponent
              onFilterChange={onFilterChange}
              meetings={meetings}
              isVisible={showFilters}
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
                  onSelect={onSelectMeeting}
                  onToggleFavorite={onToggleFavorite}
                  isFavorite={favorites.includes(meeting.fileName)}
                  onEditTitle={onEditTitle}
                  onMoveToArchive={onMoveToArchive}
                  onMoveToIncoming={onMoveToIncoming}
                  onDelete={onDeleteMeeting}
                  currentFolder={selectedFolder?.name}
                />
              ))
            )}
          </div>
        </div>
      </div>
    </div>
  );
};

export default MainContentArea;
