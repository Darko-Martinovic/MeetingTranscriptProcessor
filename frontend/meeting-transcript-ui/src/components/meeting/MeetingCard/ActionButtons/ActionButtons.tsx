import React from "react";
import { Star, Eye, Archive, Inbox, Trash2 } from "lucide-react";
import type { MeetingInfo } from "../../../../services/api";
import styles from "./ActionButtons.module.css";

interface ActionButtonsProps {
  meeting: MeetingInfo;
  isFavorite: boolean;
  currentFolder?: string;
  onSelect: (meeting: MeetingInfo) => void;
  onToggleFavorite: (fileName: string) => void;
  onShowDeleteConfirm?: () => void;
  onShowMoveToArchiveConfirm?: () => void;
  onShowMoveToIncomingConfirm?: () => void;
}

const ActionButtons: React.FC<ActionButtonsProps> = React.memo(
  ({
    meeting,
    isFavorite,
    currentFolder,
    onSelect,
    onToggleFavorite,
    onShowDeleteConfirm,
    onShowMoveToArchiveConfirm,
    onShowMoveToIncomingConfirm,
  }) => {
    const isInArchive = currentFolder?.toLowerCase() === "archive";
    const isInIncoming = currentFolder?.toLowerCase() === "incoming";

    const handleToggleFavorite = (e: React.MouseEvent) => {
      e.stopPropagation();
      onToggleFavorite(meeting.fileName);
    };

    const handleViewDetails = (e: React.MouseEvent) => {
      e.stopPropagation();
      onSelect(meeting);
    };

    const handleShowDeleteConfirm = (e: React.MouseEvent) => {
      e.stopPropagation();
      onShowDeleteConfirm?.();
    };

    const handleShowMoveToArchiveConfirm = (e: React.MouseEvent) => {
      e.stopPropagation();
      onShowMoveToArchiveConfirm?.();
    };

    const handleShowMoveToIncomingConfirm = (e: React.MouseEvent) => {
      e.stopPropagation();
      onShowMoveToIncomingConfirm?.();
    };

    return (
      <div className={styles.actionButtons}>
        {/* Details button */}
        <button
          onClick={handleViewDetails}
          className={`${styles.iconButton} ${styles.detailsButton}`}
          title="View details"
        >
          <Eye className="h-4 w-4" />
        </button>

        {/* Folder move buttons */}
        {isInArchive && onShowMoveToIncomingConfirm && (
          <button
            onClick={handleShowMoveToIncomingConfirm}
            className={`${styles.iconButton} ${styles.moveToIncomingButton}`}
            title="Move to Incoming"
          >
            <Inbox className="h-4 w-4" />
          </button>
        )}

        {isInIncoming && onShowMoveToArchiveConfirm && (
          <button
            onClick={handleShowMoveToArchiveConfirm}
            className={`${styles.iconButton} ${styles.moveToArchiveButton}`}
            title="Move to Archive"
          >
            <Archive className="h-4 w-4" />
          </button>
        )}

        {/* Delete button */}
        {onShowDeleteConfirm && (
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
    );
  }
);

ActionButtons.displayName = "ActionButtons";

export default ActionButtons;
