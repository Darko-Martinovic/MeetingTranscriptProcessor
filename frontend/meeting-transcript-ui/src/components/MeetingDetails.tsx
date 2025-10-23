import React, { useCallback } from "react";
import { Star, ExternalLink, Calendar, User, AlertCircle } from "lucide-react";
import type { MeetingTranscript } from "../services/api";
import LanguageBadge from "./common/LanguageBadge";
import styles from "./MeetingDetails.module.css";

interface MeetingDetailsProps {
  meeting: MeetingTranscript;
  onBack: () => void;
  onToggleFavorite: () => void;
  isFavorite: boolean;
}

const MeetingDetails: React.FC<MeetingDetailsProps> = React.memo(
  ({ meeting, onBack, onToggleFavorite, isFavorite }) => {
    // Use JIRA tickets directly from the meeting object since they're already loaded
    const jiraTickets = meeting.createdJiraTickets || [];

    const formatDate = useCallback((dateString: string): string => {
      return new Date(dateString).toLocaleString();
    }, []);

    const getPriorityColor = (priority: string) => {
      switch (priority.toLowerCase()) {
        case "high":
        case "critical":
          return styles.priorityHigh;
        case "medium":
          return styles.priorityMedium;
        case "low":
          return styles.priorityLow;
        default:
          return styles.priorityMedium;
      }
    };

    const getTypeIcon = (type: string) => {
      switch (type.toLowerCase()) {
        case "bug":
          return <AlertCircle className="h-4 w-4" />;
        case "task":
          return <User className="h-4 w-4" />;
        default:
          return <Calendar className="h-4 w-4" />;
      }
    };

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
            <div className={styles.languageSection}>
              <span className={styles.languageLabel}>Language:</span>
              <LanguageBadge
                language={meeting.detectedLanguage}
                size="medium"
              />
            </div>
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

          {/* JIRA Tickets */}
          <div className={styles.section}>
            <h3 className={styles.sectionTitle}>
              Created JIRA Tickets ({jiraTickets.length})
            </h3>
            {jiraTickets.length > 0 ? (
              <div className={styles.jiraTickets}>
                {jiraTickets.map((ticket) => (
                  <div key={ticket.ticketKey} className={styles.jiraTicket}>
                    <div className={styles.jiraTicketHeader}>
                      <div className={styles.jiraTicketTitle}>
                        {getTypeIcon(ticket.type)}
                        <a
                          href={ticket.ticketUrl}
                          target="_blank"
                          rel="noopener noreferrer"
                          className={styles.jiraTicketLink}
                        >
                          {ticket.ticketKey}
                        </a>
                        <ExternalLink className="h-4 w-4 ml-1" />
                      </div>
                      <div className={styles.jiraTicketMeta}>
                        <span
                          className={`${
                            styles.priorityBadge
                          } ${getPriorityColor(ticket.priority)}`}
                        >
                          {ticket.priority}
                        </span>
                        <span className={styles.typeBadge}>{ticket.type}</span>
                        <span className={styles.statusBadge}>
                          {ticket.status}
                        </span>
                      </div>
                    </div>
                    <div className={styles.jiraTicketContent}>
                      <p className={styles.jiraTicketDescription}>
                        {ticket.title}
                      </p>
                      <div className={styles.jiraTicketDetails}>
                        {ticket.assignedTo && (
                          <span className={styles.jiraTicketDetail}>
                            <User className="h-4 w-4" />
                            {ticket.assignedTo}
                          </span>
                        )}
                        <span className={styles.jiraTicketDetail}>
                          <Calendar className="h-4 w-4" />
                          {formatDate(ticket.createdAt)}
                        </span>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            ) : (
              <div className={styles.noTickets}>
                No JIRA tickets have been created for this meeting yet.
              </div>
            )}
          </div>

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
