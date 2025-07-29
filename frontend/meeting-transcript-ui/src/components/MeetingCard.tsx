import React, { useState, useCallback, useMemo } from "react";
import {
  Star,
  CheckCircle,
  XCircle,
  Clock,
  Edit2,
  Save,
  X,
  Archive,
  Inbox,
  Trash2,
} from "lucide-react";
import type { MeetingInfo } from "../services/api";
import styles from "./MeetingCard.module.css";

interface MeetingCardProps {
  meeting: MeetingInfo;
  onSelect: (meeting: MeetingInfo) => void;
  onToggleFavorite: (fileName: string) => void;
  isFavorite: boolean;
  onEditTitle?: (fileName: string, newTitle: string) => void;
  onMoveToArchive?: (fileName: string) => void;
  onMoveToIncoming?: (fileName: string) => void;
  onDelete?: (fileName: string) => void;
  currentFolder?: string;
}

const MeetingCard: React.FC<MeetingCardProps> = React.memo(
  ({
    meeting,
    onSelect,
    onToggleFavorite,
    isFavorite,
    onEditTitle,
    onMoveToArchive,
    onMoveToIncoming,
    onDelete,
    currentFolder,
  }) => {
    const [isEditing, setIsEditing] = useState(false);
    const [editedTitle, setEditedTitle] = useState(meeting.title);
    const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
    const [showMoveToArchiveConfirm, setShowMoveToArchiveConfirm] =
      useState(false);
    const [showMoveToIncomingConfirm, setShowMoveToIncomingConfirm] =
      useState(false);

    // Memoized status badge component
    const statusBadge = useMemo(() => {
      const getStatusBadgeClasses = (status: string) => {
        switch (status.toLowerCase()) {
          case "success":
            return {
              containerClass: `${styles.statusBadge} ${styles.statusBadgeSuccess}`,
              icon: <CheckCircle className={styles.statusIcon} />,
              text: "Success",
            };
          case "error":
            return {
              containerClass: `${styles.statusBadge} ${styles.statusBadgeError}`,
              icon: <XCircle className={styles.statusIcon} />,
              text: "Error",
            };
          case "processing":
            return {
              containerClass: `${styles.statusBadge} ${styles.statusBadgeProcessing}`,
              icon: (
                <Clock
                  className={`${styles.statusIcon} ${styles.statusIconProcessing}`}
                />
              ),
              text: "Processing",
            };
          default:
            return {
              containerClass: `${styles.statusBadge} ${styles.statusBadgeUnknown}`,
              icon: <Clock className={styles.statusIcon} />,
              text: "Unknown",
            };
        }
      };

      const { containerClass, icon, text } = getStatusBadgeClasses(
        meeting.status
      );
      return (
        <span className={containerClass}>
          {icon}
          {text}
        </span>
      );
    }, [meeting.status]);

    // Memoized participants extraction
    const participants = useMemo(() => {
      const participantMatch = meeting.previewContent.match(
        /Participants?:\s*([^\n]+)/i
      );
      if (participantMatch) {
        return participantMatch[1]
          .split(",")
          .map((p) => p.trim())
          .filter((p) => p.length > 0)
          .slice(0, 5);
      }
      return [];
    }, [meeting.previewContent]);

    // Memoized folder state
    const folderState = useMemo(
      () => ({
        isInArchive: currentFolder?.toLowerCase() === "archive",
        isInIncoming: currentFolder?.toLowerCase() === "incoming",
      }),
      [currentFolder]
    );

    // Utility functions
    const formatFileSize = useCallback((bytes: number): string => {
      if (bytes === 0) return "0 Bytes";
      const k = 1024;
      const sizes = ["Bytes", "KB", "MB", "GB"];
      const i = Math.floor(Math.log(bytes) / Math.log(k));
      return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + " " + sizes[i];
    }, []);

    const formatDate = useCallback((dateString: string): string => {
      const date = new Date(dateString);
      return (
        date.toLocaleDateString() +
        " " +
        date.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })
      );
    }, []);

    // Event handlers
    const handleCardClick = useCallback(() => {
      onSelect(meeting);
    }, [onSelect, meeting]);

    const handleToggleFavorite = useCallback(
      (e: React.MouseEvent) => {
        e.stopPropagation();
        onToggleFavorite(meeting.fileName);
      },
      [onToggleFavorite, meeting.fileName]
    );

    const handleStartEdit = useCallback((e: React.MouseEvent) => {
      e.stopPropagation();
      setIsEditing(true);
    }, []);

    const handleSaveTitle = useCallback(() => {
      const trimmedTitle = editedTitle.trim();

      // Validate title length
      if (trimmedTitle.length > 200) {
        // Automatically truncate if too long
        const truncatedTitle = trimmedTitle.substring(0, 200);
        setEditedTitle(truncatedTitle);

        if (onEditTitle && truncatedTitle !== meeting.title) {
          onEditTitle(meeting.fileName, truncatedTitle);
        }
      } else if (
        onEditTitle &&
        trimmedTitle !== meeting.title &&
        trimmedTitle
      ) {
        onEditTitle(meeting.fileName, trimmedTitle);
      }

      setIsEditing(false);
    }, [onEditTitle, editedTitle, meeting.title, meeting.fileName]);

    const handleCancelEdit = useCallback(() => {
      setEditedTitle(meeting.title);
      setIsEditing(false);
    }, [meeting.title]);

    const handleTitleInputChange = useCallback(
      (e: React.ChangeEvent<HTMLInputElement>) => {
        setEditedTitle(e.target.value);
      },
      []
    );

    const handleTitleKeyDown = useCallback(
      (e: React.KeyboardEvent) => {
        if (e.key === "Enter") {
          handleSaveTitle();
        } else if (e.key === "Escape") {
          handleCancelEdit();
        }
      },
      [handleSaveTitle, handleCancelEdit]
    );

    const handleDelete = useCallback(() => {
      if (onDelete) {
        onDelete(meeting.fileName);
      }
      setShowDeleteConfirm(false);
    }, [onDelete, meeting.fileName]);

    const handleShowDeleteConfirm = useCallback((e: React.MouseEvent) => {
      e.stopPropagation();
      setShowDeleteConfirm(true);
    }, []);

    const handleCancelDelete = useCallback((e: React.MouseEvent) => {
      e.stopPropagation();
      setShowDeleteConfirm(false);
    }, []);

    const handleShowMoveToArchiveConfirm = useCallback(
      (e: React.MouseEvent) => {
        e.stopPropagation();
        setShowMoveToArchiveConfirm(true);
      },
      []
    );

    const handleMoveToArchive = useCallback(() => {
      if (onMoveToArchive) {
        onMoveToArchive(meeting.fileName);
      }
      setShowMoveToArchiveConfirm(false);
    }, [onMoveToArchive, meeting.fileName]);

    const handleCancelMoveToArchive = useCallback((e: React.MouseEvent) => {
      e.stopPropagation();
      setShowMoveToArchiveConfirm(false);
    }, []);

    const handleShowMoveToIncomingConfirm = useCallback(
      (e: React.MouseEvent) => {
        e.stopPropagation();
        setShowMoveToIncomingConfirm(true);
      },
      []
    );

    const handleMoveToIncoming = useCallback(() => {
      if (onMoveToIncoming) {
        onMoveToIncoming(meeting.fileName);
      }
      setShowMoveToIncomingConfirm(false);
    }, [onMoveToIncoming, meeting.fileName]);

    const handleCancelMoveToIncoming = useCallback((e: React.MouseEvent) => {
      e.stopPropagation();
      setShowMoveToIncomingConfirm(false);
    }, []);

    return (
      <div className={styles.card} onClick={handleCardClick}>
        {/* Header with title and action buttons */}
        <div className={styles.header}>
          <div className={styles.titleSection}>
            {isEditing ? (
              <div className={styles.titleEditContainer}>
                <input
                  type="text"
                  value={editedTitle}
                  onChange={handleTitleInputChange}
                  className={styles.titleInput}
                  onClick={(e) => e.stopPropagation()}
                  onKeyDown={handleTitleKeyDown}
                  autoFocus
                />
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    handleSaveTitle();
                  }}
                  className={`${styles.iconButton} ${styles.saveButton}`}
                  title="Save title"
                >
                  <Save className="h-4 w-4" />
                </button>
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    handleCancelEdit();
                  }}
                  className={`${styles.iconButton} ${styles.cancelButton}`}
                  title="Cancel editing"
                >
                  <X className="h-4 w-4" />
                </button>
              </div>
            ) : (
              <div className={styles.titleDisplay}>
                <h3 className={styles.title}>{meeting.title}</h3>
                {onEditTitle && (
                  <button
                    onClick={handleStartEdit}
                    className={`${styles.iconButtonSmall} ${styles.editButton}`}
                    title="Edit title"
                  >
                    <Edit2 className="h-4 w-4" />
                  </button>
                )}
              </div>
            )}
          </div>

          <div className={styles.actionButtons}>
            {/* Folder move buttons */}
            {folderState.isInArchive && onMoveToIncoming && (
              <button
                onClick={handleShowMoveToIncomingConfirm}
                className={`${styles.iconButton} ${styles.moveToIncomingButton}`}
                title="Move to Incoming"
              >
                <Inbox className="h-4 w-4" />
              </button>
            )}

            {folderState.isInIncoming && onMoveToArchive && (
              <button
                onClick={handleShowMoveToArchiveConfirm}
                className={`${styles.iconButton} ${styles.moveToArchiveButton}`}
                title="Move to Archive"
              >
                <Archive className="h-4 w-4" />
              </button>
            )}

            {/* Delete button */}
            {onDelete && (
              <button
                onClick={handleShowDeleteConfirm}
                className={`${styles.iconButton} ${styles.deleteButton}`}
                title="Delete meeting"
              >
                <Trash2 className="h-4 w-4" />
              </button>
            )}

            {/* Favorite button */}
            <button
              onClick={handleToggleFavorite}
              className={`${styles.iconButton} ${styles.favoriteButton}`}
              title={isFavorite ? "Remove from favorites" : "Add to favorites"}
            >
              <Star
                className={`${styles.favoriteIcon} ${
                  isFavorite
                    ? styles.favoriteIconActive
                    : styles.favoriteIconInactive
                }`}
              />
            </button>
          </div>
        </div>

        {/* Participants row */}
        {participants.length > 0 && (
          <div className={styles.participantsSection}>
            <div className={styles.participantsLabel}>
              <span className={styles.participantsLabelText}>
                Participants:
              </span>
            </div>
            <div className={styles.participantsList}>
              {participants.map((participant, index) => (
                <span key={index} className={styles.participantBadge}>
                  {participant}
                </span>
              ))}
            </div>
          </div>
        )}

        {/* Status and metadata row */}
        <div className={styles.statusSection}>
          <div className={styles.statusMetadata}>
            {statusBadge}
            <span className={styles.fileSize}>
              {formatFileSize(meeting.size)}
            </span>
          </div>
          <div className={styles.dateText}>
            {formatDate(meeting.lastModified)}
          </div>
        </div>

        {/* Delete Confirmation Modal */}
        {showDeleteConfirm && (
          <div className={styles.modalOverlay}>
            <div className={styles.modal}>
              <div className={styles.modalHeader}>
                <Trash2 className={styles.modalIcon} />
                <h3 className={styles.modalTitle}>Delete Meeting</h3>
              </div>
              <p className={styles.modalContent}>
                Are you sure you want to delete "{meeting.title}"? This action
                cannot be undone.
              </p>
              <div className={styles.modalActions}>
                <button
                  onClick={handleCancelDelete}
                  className={`${styles.modalButton} ${styles.modalButtonSecondary}`}
                >
                  Cancel
                </button>
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    handleDelete();
                  }}
                  className={`${styles.modalButton} ${styles.modalButtonDanger}`}
                >
                  Delete
                </button>
              </div>
            </div>
          </div>
        )}

        {/* Move to Archive Confirmation Modal */}
        {showMoveToArchiveConfirm && (
          <div className={styles.modalOverlay}>
            <div className={styles.modal}>
              <div className={styles.modalHeader}>
                <Archive className={styles.modalIcon} />
                <h3 className={styles.modalTitle}>Move to Archive</h3>
              </div>
              <p className={styles.modalContent}>
                Are you sure you want to move "{meeting.title}" to the Archive
                folder?
              </p>
              <div className={styles.modalActions}>
                <button
                  onClick={handleCancelMoveToArchive}
                  className={`${styles.modalButton} ${styles.modalButtonSecondary}`}
                >
                  Cancel
                </button>
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    handleMoveToArchive();
                  }}
                  className={`${styles.modalButton} ${styles.modalButtonPrimary}`}
                >
                  Move to Archive
                </button>
              </div>
            </div>
          </div>
        )}

        {/* Move to Incoming Confirmation Modal */}
        {showMoveToIncomingConfirm && (
          <div className={styles.modalOverlay}>
            <div className={styles.modal}>
              <div className={styles.modalHeader}>
                <Inbox className={styles.modalIcon} />
                <h3 className={styles.modalTitle}>Move to Incoming</h3>
              </div>
              <p className={styles.modalContent}>
                Are you sure you want to move "{meeting.title}" to the Incoming
                folder?
              </p>
              <div className={styles.modalActions}>
                <button
                  onClick={handleCancelMoveToIncoming}
                  className={`${styles.modalButton} ${styles.modalButtonSecondary}`}
                >
                  Cancel
                </button>
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    handleMoveToIncoming();
                  }}
                  className={`${styles.modalButton} ${styles.modalButtonPrimary}`}
                >
                  Move to Incoming
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    );
  }
);

// Set display name for debugging
MeetingCard.displayName = "MeetingCard";

export default MeetingCard;
