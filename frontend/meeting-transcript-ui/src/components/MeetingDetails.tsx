import React, { useCallback } from "react";
import { Star } from "lucide-react";
import type { MeetingTranscript } from "../services/api";
import styles from "./MeetingDetails.module.css";

interface MeetingDetailsProps {
  meeting: MeetingTranscript;
  onBack: () => void;
  onToggleFavorite: () => void;
  isFavorite: boolean;
}

const MeetingDetails: React.FC<MeetingDetailsProps> = React.memo(
  ({ meeting, onBack, onToggleFavorite, isFavorite }) => {
    const formatDate = useCallback((dateString: string): string => {
      return new Date(dateString).toLocaleString();
    }, []);

    return (
      <div className={styles.meetingDetail}>
        <div className={styles.meetingDetailHeader}>
          <div className={styles.headerContent}>
            <div className={styles.headerLeft}>
              <button onClick={onBack} className={styles.iconButton}>
                ←
              </button>
              <h2 className={styles.meetingDetailTitle}>{meeting.title}</h2>
            </div>
            <button onClick={onToggleFavorite} className={styles.iconButton}>
              <Star
                className={`h-5 w-5 ${
                  isFavorite ? "text-yellow-500 fill-current" : "text-gray-400"
                }`}
              />
            </button>
          </div>
          <div className={styles.meetingDetailMeta}>
            <span>Meeting Date: {formatDate(meeting.meetingDate)}</span>
            <span>Processed: {formatDate(meeting.processedAt)}</span>
            <span>Language: {meeting.detectedLanguage}</span>
          </div>
        </div>

        <div className={styles.meetingDetailContent}>
          {/* Participants */}
          {meeting.participants.length > 0 && (
            <div className={styles.section}>
              <h3 className={styles.sectionTitle}>Participants</h3>
              <div className={styles.participants}>
                {meeting.participants.map((participant, index) => (
                  <span key={index} className={styles.participantTag}>
                    {participant}
                  </span>
                ))}
              </div>
            </div>
          )}

          {/* Action Items */}
          {meeting.actionItems.length > 0 && (
            <div className={styles.section}>
              <h3 className={styles.sectionTitle}>
                Action Items ({meeting.actionItems.length})
              </h3>
              <div className={styles.actionItems}>
                {meeting.actionItems.map((item) => (
                  <div key={item.id} className={styles.actionItem}>
                    <div className={styles.actionItemContent}>
                      <div className={styles.actionItemMain}>
                        <p className={styles.actionItemDescription}>
                          {item.description}
                        </p>
                        <div className={styles.actionItemMeta}>
                          {item.assignee && (
                            <span>Assignee: {item.assignee}</span>
                          )}
                          {item.dueDate && (
                            <span>Due: {formatDate(item.dueDate)}</span>
                          )}
                          <span>Priority: {item.priority}</span>
                          <span>Status: {item.status}</span>
                        </div>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Content */}
          <div className={styles.section}>
            <h3 className={styles.sectionTitle}>Meeting Content</h3>
            <div className={styles.meetingContent}>
              <pre className={styles.meetingText}>{meeting.content}</pre>
            </div>
          </div>
        </div>
      </div>
    );
  }
);

// Set display name for debugging
MeetingDetails.displayName = "MeetingDetails";

export default MeetingDetails;
