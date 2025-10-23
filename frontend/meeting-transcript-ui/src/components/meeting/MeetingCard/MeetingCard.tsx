import React, { useState, useCallback, useMemo } from "react";
import { Archive, Inbox, Trash2 } from "lucide-react";
import type { MeetingInfo } from "../../../services/api";
import ConfirmationModal from "../../common/ConfirmationModal";
import StatusBadge from "../../common/StatusBadge";
import LanguageBadge from "../../common/LanguageBadge";
import ActionButtons from "./ActionButtons";
import EditableTitle from "./EditableTitle";
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
    const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
    const [showMoveToArchiveConfirm, setShowMoveToArchiveConfirm] =
      useState(false);
    const [showMoveToIncomingConfirm, setShowMoveToIncomingConfirm] =
      useState(false);

    // Memoized participants extraction
    const participants = useMemo(() => {
      // First try to extract multi-line participant lists (bullet format)
      const multiLineMatch = meeting.previewContent.match(
        /Participants?:\s*\n((?:\s*[-•]\s*[^\n]+(?:\n|$))+)/i
      );

      if (multiLineMatch) {
        const lines = multiLineMatch[1].split("\n");
        const extractedParticipants: string[] = [];

        lines.forEach((line) => {
          const trimmedLine = line.trim();
          if (trimmedLine.startsWith("-") || trimmedLine.startsWith("•")) {
            const participant = trimmedLine.substring(1).trim();
            if (participant) {
              // Clean up participant name (remove role descriptions in parentheses)
              const cleanName = participant.replace(/\s*\([^)]*\)/, "").trim();
              if (cleanName) {
                extractedParticipants.push(cleanName);
              }
            }
          }
        });

        if (extractedParticipants.length > 0) {
          return extractedParticipants.slice(0, 5); // Limit to 5 for card display
        }
      }

      // Fallback: single-line format
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

    // Event handlers for confirmation modals
    const handleDelete = useCallback(() => {
      if (onDelete) {
        onDelete(meeting.fileName);
      }
      setShowDeleteConfirm(false);
    }, [onDelete, meeting.fileName]);

    const handleMoveToArchive = useCallback(() => {
      if (onMoveToArchive) {
        onMoveToArchive(meeting.fileName);
      }
      setShowMoveToArchiveConfirm(false);
    }, [onMoveToArchive, meeting.fileName]);

    const handleMoveToIncoming = useCallback(() => {
      if (onMoveToIncoming) {
        onMoveToIncoming(meeting.fileName);
      }
      setShowMoveToIncomingConfirm(false);
    }, [onMoveToIncoming, meeting.fileName]);

    const handleCancelDelete = useCallback((e: React.MouseEvent) => {
      e.stopPropagation();
      setShowDeleteConfirm(false);
    }, []);

    const handleCancelMoveToArchive = useCallback((e: React.MouseEvent) => {
      e.stopPropagation();
      setShowMoveToArchiveConfirm(false);
    }, []);

    const handleCancelMoveToIncoming = useCallback((e: React.MouseEvent) => {
      e.stopPropagation();
      setShowMoveToIncomingConfirm(false);
    }, []);

    return (
      <div className={styles.card}>
        {/* Header with title and action buttons */}
        <div className={styles.header}>
          <div className={styles.titleSection}>
            <EditableTitle
              title={meeting.title}
              fileName={meeting.fileName}
              onEditTitle={onEditTitle}
            />
          </div>

          <ActionButtons
            meeting={meeting}
            isFavorite={isFavorite}
            currentFolder={currentFolder}
            onSelect={onSelect}
            onToggleFavorite={onToggleFavorite}
            onShowDeleteConfirm={
              onDelete ? () => setShowDeleteConfirm(true) : undefined
            }
            onShowMoveToArchiveConfirm={
              onMoveToArchive
                ? () => setShowMoveToArchiveConfirm(true)
                : undefined
            }
            onShowMoveToIncomingConfirm={
              onMoveToIncoming
                ? () => setShowMoveToIncomingConfirm(true)
                : undefined
            }
          />
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
            <StatusBadge status={meeting.status} />
            <LanguageBadge language={meeting.language} />
            <span className={styles.fileSize}>
              {formatFileSize(meeting.size)}
            </span>
          </div>
          <div className={styles.dateText}>
            {formatDate(meeting.lastModified)}
          </div>
        </div>

        {/* Confirmation Modals */}
        <ConfirmationModal
          isVisible={showDeleteConfirm}
          title="Delete Meeting"
          message={`Are you sure you want to delete "${meeting.title}"? This action cannot be undone.`}
          icon={Trash2}
          confirmText="Delete"
          onConfirm={(e) => {
            e.stopPropagation();
            handleDelete();
          }}
          onCancel={handleCancelDelete}
          variant="danger"
        />

        <ConfirmationModal
          isVisible={showMoveToArchiveConfirm}
          title="Move to Archive"
          message={`Are you sure you want to move "${meeting.title}" to the Archive folder?`}
          icon={Archive}
          confirmText="Move to Archive"
          onConfirm={(e) => {
            e.stopPropagation();
            handleMoveToArchive();
          }}
          onCancel={handleCancelMoveToArchive}
          variant="primary"
        />

        <ConfirmationModal
          isVisible={showMoveToIncomingConfirm}
          title="Move to Incoming"
          message={`Are you sure you want to move "${meeting.title}" to the Incoming folder?`}
          icon={Inbox}
          confirmText="Move to Incoming"
          onConfirm={(e) => {
            e.stopPropagation();
            handleMoveToIncoming();
          }}
          onCancel={handleCancelMoveToIncoming}
          variant="primary"
        />
      </div>
    );
  }
);

// Set display name for debugging
MeetingCard.displayName = "MeetingCard";

export default MeetingCard;
