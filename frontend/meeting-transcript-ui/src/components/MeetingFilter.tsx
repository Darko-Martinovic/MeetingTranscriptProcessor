import React, { useState, useEffect, useCallback, useMemo } from "react";
import {
  Search,
  Filter,
  X,
  Calendar,
  Users,
  CheckSquare,
  Languages,
} from "lucide-react";
import type { MeetingFilter, MeetingInfo } from "../services/api";
import styles from "./MeetingFilter.module.css";

interface MeetingFilterProps {
  onFilterChange: (filter: MeetingFilter) => void;
  meetings: MeetingInfo[];
  isVisible: boolean;
  onToggleVisibility: () => void;
}

const MeetingFilterComponent: React.FC<MeetingFilterProps> = React.memo(
  ({ onFilterChange, meetings, isVisible, onToggleVisibility }) => {
    const [filter, setFilter] = useState<MeetingFilter>({
      searchText: "",
      status: [],
      language: [],
      participants: [],
      dateFrom: "",
      dateTo: "",
      hasJiraTickets: undefined,
      sortBy: "date",
      sortOrder: "desc",
    });

    // Memoized available filters
    const availableFilters = useMemo(() => {
      const statuses = [
        ...new Set(
          meetings.map((m) => m.status).filter((s) => s && s !== "unknown")
        ),
      ];
      const languages = [
        ...new Set(
          meetings.map((m) => m.language).filter((l) => l && l !== "unknown")
        ),
      ];
      const participants = [
        ...new Set(meetings.flatMap((m) => m.participants || [])),
      ];

      return {
        statuses: statuses.sort(),
        languages: languages.filter((l) => l !== undefined).sort(),
        participants: participants.sort(),
      };
    }, [meetings]);

    // Memoized active filters check
    const hasActiveFilters = useMemo(() => {
      return (
        filter.searchText ||
        (filter.status && filter.status.length > 0) ||
        (filter.language && filter.language.length > 0) ||
        (filter.participants && filter.participants.length > 0) ||
        filter.dateFrom ||
        filter.dateTo ||
        filter.hasJiraTickets !== undefined
      );
    }, [filter]);

    // Memoized active filter count
    const activeFilterCount = useMemo(() => {
      return (
        (filter.status?.length || 0) +
        (filter.language?.length || 0) +
        (filter.participants?.length || 0) +
        (filter.searchText ? 1 : 0) +
        (filter.dateFrom ? 1 : 0) +
        (filter.dateTo ? 1 : 0) +
        (filter.hasJiraTickets !== undefined ? 1 : 0)
      );
    }, [filter]);

    useEffect(() => {
      onFilterChange(filter);
    }, [filter, onFilterChange]);

    const updateFilter = useCallback(
      <K extends keyof MeetingFilter>(key: K, value: MeetingFilter[K]) => {
        setFilter((prev) => ({ ...prev, [key]: value }));
      },
      []
    );

    const clearFilters = useCallback(() => {
      setFilter({
        searchText: "",
        status: [],
        language: [],
        participants: [],
        dateFrom: "",
        dateTo: "",
        hasJiraTickets: undefined,
        sortBy: "date",
        sortOrder: "desc",
      });
    }, []);

    const toggleArrayFilter = useCallback(
      <T extends string>(
        key: "status" | "language" | "participants",
        value: T
      ) => {
        const currentArray = filter[key] as T[];
        const newArray = currentArray.includes(value)
          ? currentArray.filter((item) => item !== value)
          : [...currentArray, value];
        updateFilter(key, newArray);
      },
      [filter, updateFilter]
    );

    return (
      <div className={styles.container}>
        {/* Filter Toggle Button */}
        <div className={styles.toggleSection}>
          <button
            onClick={onToggleVisibility}
            className={
              isVisible
                ? styles.toggleButtonVisible
                : hasActiveFilters
                ? styles.toggleButtonActive
                : styles.toggleButtonInactive
            }
          >
            <Filter className="h-4 w-4" />
            <span>
              {isVisible
                ? "Hide Filters"
                : hasActiveFilters
                ? "Filters Active"
                : "Show Filters"}
            </span>
            {hasActiveFilters && !isVisible && (
              <span className={styles.filterCountBadge}>
                {activeFilterCount}
              </span>
            )}
          </button>

          {hasActiveFilters && (
            <button onClick={clearFilters} className={styles.clearButton}>
              <X className="h-4 w-4" />
              <span>Clear All</span>
            </button>
          )}
        </div>

        {/* Filter Panel */}
        {isVisible && (
          <div className={`${styles.filterPanel} ${styles.fadeIn}`}>
            {/* Search */}
            <div className={styles.searchSection}>
              <Search className={styles.searchIcon} size={20} />
              <input
                type="text"
                placeholder="Search meetings, participants, content..."
                value={filter.searchText}
                onChange={(e) => updateFilter("searchText", e.target.value)}
                className={styles.searchInput}
              />
            </div>

            <div className={styles.filterGrid}>
              {/* Status Filter */}
              <div className={styles.filterCard}>
                <div className={styles.filterCardHeader}>
                  <div className={styles.statusDot}></div>
                  <span>Status</span>
                </div>
                <div className={styles.filterOptions}>
                  {availableFilters.statuses.map((status) => (
                    <label key={status} className={styles.filterOption}>
                      <input
                        type="checkbox"
                        checked={filter.status?.includes(status) || false}
                        onChange={() => toggleArrayFilter("status", status)}
                        className={`${styles.checkbox} ${styles.checkboxStatus}`}
                      />
                      <span className={styles.filterOptionLabel}>{status}</span>
                    </label>
                  ))}
                </div>
              </div>

              {/* Language Filter */}
              <div className={styles.filterCard}>
                <div className={styles.filterCardHeader}>
                  <Languages size={16} className={styles.iconLanguage} />
                  <span>Language</span>
                </div>
                <div className={styles.filterOptions}>
                  {availableFilters.languages.map((language) => (
                    <label key={language} className={styles.filterOption}>
                      <input
                        type="checkbox"
                        checked={filter.language?.includes(language) || false}
                        onChange={() => toggleArrayFilter("language", language)}
                        className={`${styles.checkbox} ${styles.checkboxLanguage}`}
                      />
                      <span className={styles.filterOptionLabel}>
                        {language}
                      </span>
                    </label>
                  ))}
                </div>
              </div>

              {/* Participants Filter */}
              <div className={styles.filterCard}>
                <div className={styles.filterCardHeader}>
                  <Users size={16} className={styles.iconParticipants} />
                  <span>Participants</span>
                </div>
                <div className={styles.filterOptions}>
                  {availableFilters.participants
                    .slice(0, 10)
                    .map((participant) => (
                      <label key={participant} className={styles.filterOption}>
                        <input
                          type="checkbox"
                          checked={
                            filter.participants?.includes(participant) || false
                          }
                          onChange={() =>
                            toggleArrayFilter("participants", participant)
                          }
                          className={`${styles.checkbox} ${styles.checkboxParticipants}`}
                        />
                        <span className={styles.filterOptionLabel}>
                          {participant}
                        </span>
                      </label>
                    ))}
                  {availableFilters.participants.length > 10 && (
                    <div className={styles.participantsOverflow}>
                      +{availableFilters.participants.length - 10} more
                      participants
                    </div>
                  )}
                </div>
              </div>

              {/* Date Range and Special Filters */}
              <div className={styles.filterCard}>
                <div className={styles.dateSpecialSection}>
                  {/* Date Range */}
                  <div className={styles.dateRangeSection}>
                    <div className={styles.filterCardHeader}>
                      <Calendar size={16} className={styles.iconCalendar} />
                      <span>Date Range</span>
                    </div>
                    <div className={styles.dateInputGroup}>
                      <label className={styles.dateLabel}>From</label>
                      <input
                        type="date"
                        value={filter.dateFrom}
                        onChange={(e) =>
                          updateFilter("dateFrom", e.target.value)
                        }
                        className={styles.dateInput}
                      />
                    </div>
                    <div className={styles.dateInputGroup}>
                      <label className={styles.dateLabel}>To</label>
                      <input
                        type="date"
                        value={filter.dateTo}
                        onChange={(e) => updateFilter("dateTo", e.target.value)}
                        className={styles.dateInput}
                      />
                    </div>
                  </div>

                  {/* Jira Tickets Filter */}
                  <div className={styles.jiraSection}>
                    <div className={styles.filterCardHeader}>
                      <CheckSquare size={16} className={styles.iconJira} />
                      <span>Jira Tickets</span>
                    </div>
                    <select
                      value={
                        filter.hasJiraTickets === undefined
                          ? ""
                          : filter.hasJiraTickets.toString()
                      }
                      onChange={(e) =>
                        updateFilter(
                          "hasJiraTickets",
                          e.target.value === ""
                            ? undefined
                            : e.target.value === "true"
                        )
                      }
                      className={styles.jiraSelect}
                    >
                      <option value="">All Meetings</option>
                      <option value="true">✅ Has Jira Tickets</option>
                      <option value="false">❌ No Jira Tickets</option>
                    </select>
                  </div>
                </div>
              </div>
            </div>

            {/* Sorting */}
            <div className={styles.sortingSection}>
              <div className={styles.sortingGroup}>
                <label className={styles.sortingLabel}>🔄 Sort By</label>
                <select
                  value={filter.sortBy}
                  onChange={(e) =>
                    updateFilter(
                      "sortBy",
                      e.target.value as
                        | "date"
                        | "title"
                        | "size"
                        | "status"
                        | "language"
                        | "participants"
                    )
                  }
                  className={styles.sortingSelect}
                >
                  <option value="date">📅 Date</option>
                  <option value="title">📝 Title</option>
                  <option value="size">📏 Size</option>
                  <option value="status">⚡ Status</option>
                  <option value="language">🌐 Language</option>
                  <option value="participants">👥 Participants Count</option>
                </select>
              </div>
              <div className={styles.sortingGroup}>
                <label className={styles.sortingLabel}>📊 Order</label>
                <select
                  value={filter.sortOrder}
                  onChange={(e) =>
                    updateFilter("sortOrder", e.target.value as "asc" | "desc")
                  }
                  className={styles.sortingSelect}
                >
                  <option value="desc">⬇️ Descending</option>
                  <option value="asc">⬆️ Ascending</option>
                </select>
              </div>
            </div>
          </div>
        )}
      </div>
    );
  }
);

// Set display name for debugging
MeetingFilterComponent.displayName = "MeetingFilterComponent";

export default MeetingFilterComponent;
