import React, { useMemo } from "react";
import { Filter } from "lucide-react";
import {
  FolderType,
  type FolderInfo,
  type MeetingFilter,
} from "../services/api";
import styles from "./MeetingListHeader.module.css";

interface MeetingListHeaderProps {
  selectedFolder: FolderInfo;
  meetingCount: number;
  showFilters: boolean;
  onToggleFilters: () => void;
  currentFilter: MeetingFilter;
}

const MeetingListHeader: React.FC<MeetingListHeaderProps> = ({
  selectedFolder,
  meetingCount,
  showFilters,
  onToggleFilters,
  currentFilter,
}) => {
  // Calculate if there are active filters
  const hasActiveFilters = useMemo(() => {
    return (
      currentFilter.searchText ||
      (currentFilter.status && currentFilter.status.length > 0) ||
      (currentFilter.language && currentFilter.language.length > 0) ||
      (currentFilter.participants && currentFilter.participants.length > 0) ||
      currentFilter.dateFrom ||
      currentFilter.dateTo ||
      currentFilter.hasJiraTickets !== undefined
    );
  }, [currentFilter]);

  // Calculate active filter count
  const activeFilterCount = useMemo(() => {
    return (
      (currentFilter.status?.length || 0) +
      (currentFilter.language?.length || 0) +
      (currentFilter.participants?.length || 0) +
      (currentFilter.searchText ? 1 : 0) +
      (currentFilter.dateFrom ? 1 : 0) +
      (currentFilter.dateTo ? 1 : 0) +
      (currentFilter.hasJiraTickets !== undefined ? 1 : 0)
    );
  }, [currentFilter]);

  return (
    <div className={styles.meetingListHeader}>
      <div className={styles.meetingListHeaderContent}>
        <h2 className={styles.meetingListTitle}>
          {selectedFolder.name} ({meetingCount} meetings)
        </h2>
        {/* Show filter button only for Archive folder */}
        {selectedFolder.type === FolderType.Archive && (
          <button
            onClick={onToggleFilters}
            className={`${styles.filterToggleButton} ${
              showFilters
                ? styles.filterToggleButtonVisible
                : hasActiveFilters
                ? styles.filterToggleButtonActive
                : styles.filterToggleButtonInactive
            }`}
          >
            <Filter className="h-4 w-4" />
            <span>
              {showFilters
                ? "Hide Filters"
                : hasActiveFilters
                ? "Filters Active"
                : "Show Filters"}
            </span>
            {hasActiveFilters && !showFilters && (
              <span className={styles.filterCountBadge}>
                {activeFilterCount}
              </span>
            )}
          </button>
        )}
      </div>
    </div>
  );
};

export default MeetingListHeader;
