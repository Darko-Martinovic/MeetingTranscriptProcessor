import React, { useState } from "react";
import {
  X,
  FolderInput,
  Settings,
  Brain,
  ExternalLink,
  MessageSquare,
  Archive,
  Clock,
  Star,
  ArrowRight,
  Workflow,
} from "lucide-react";
import styles from "./WorkflowModal.module.css";

interface WorkflowModalProps {
  onClose: () => void;
}

interface WorkflowStep {
  id: string;
  name: string;
  icon: React.ReactElement;
  description: string;
  details: string;
  color: string;
}

const WorkflowModal: React.FC<WorkflowModalProps> = ({ onClose }) => {
  const [selectedStep, setSelectedStep] = useState<string | null>(null);

  const workflowSteps: WorkflowStep[] = [
    {
      id: "incoming",
      name: "Incoming",
      icon: <FolderInput className="h-6 w-6" />,
      description: "Files uploaded and waiting to be processed",
      details:
        "This is where new meeting transcript files are initially placed when uploaded to the system. Files remain here until the processing service picks them up for analysis.",
      color: "#bfdbfe", // Pastel blue
    },
    {
      id: "processing",
      name: "Processing",
      icon: <Settings className="h-6 w-6" />,
      description: "Files currently being analyzed and processed",
      details:
        "During this stage, transcript files are parsed, cleaned, and prepared for AI analysis. The system extracts meeting metadata, participant information, and structures the content for optimal AI processing.",
      color: "#fed7aa", // Pastel orange
    },
    {
      id: "ai-analysis",
      name: "AI Analysis",
      icon: <Brain className="h-6 w-6" />,
      description: "AI extracts insights, summaries, and action items",
      details:
        "Azure OpenAI analyzes the processed transcript to generate meeting summaries, identify action items, extract key decisions, and determine meeting outcomes. This stage produces structured insights from the raw transcript data.",
      color: "#c4b5fd", // Pastel purple
    },
    {
      id: "jira-creation",
      name: "JIRA Creation",
      icon: <ExternalLink className="h-6 w-6" />,
      description: "Action items automatically created as JIRA tickets",
      details:
        "Identified action items and tasks are automatically converted into JIRA tickets with appropriate assignees, priorities, and project assignments. Each action item becomes a trackable work item in your project management system.",
      color: "#bbf7d0", // Pastel green
    },
    {
      id: "slack-notification",
      name: "Slack Notification",
      icon: <MessageSquare className="h-6 w-6" />,
      description: "Team members notified via Slack about completion",
      details:
        "Once processing is complete, relevant team members receive Slack notifications with meeting summaries, links to created JIRA tickets, and key outcomes. This ensures everyone stays informed about meeting results.",
      color: "#fecaca", // Pastel pink
    },
    {
      id: "archive",
      name: "Archive",
      icon: <Archive className="h-6 w-6" />,
      description: "Completed and historical meeting transcripts",
      details:
        "All successfully processed meeting transcripts are stored here for long-term retention. This serves as a searchable repository of all past meetings and their analysis results.",
      color: "#e5e7eb", // Pastel gray
    },
  ];

  const additionalFolders: WorkflowStep[] = [
    {
      id: "recent",
      name: "Recent",
      icon: <Clock className="h-6 w-6" />,
      description: "Recently processed meetings for quick access",
      details:
        "Quick access view showing the most recently processed meetings. This provides easy access to recent meeting insights and outcomes without navigating through the full archive.",
      color: "#fde68a", // Pastel yellow
    },
    {
      id: "favorites",
      name: "Favorites",
      icon: <Star className="h-6 w-6" />,
      description: "Starred meetings for easy reference",
      details:
        "Important or frequently referenced meetings can be marked as favorites for quick access. This helps users easily find key meetings and their insights.",
      color: "#fbbf24", // Pastel gold
    },
  ];

  const handleStepClick = (stepId: string) => {
    setSelectedStep(selectedStep === stepId ? null : stepId);
  };

  const handleOverlayClick = (e: React.MouseEvent) => {
    if (e.target === e.currentTarget) {
      onClose();
    }
  };

  return (
    <div className={styles.overlay} onClick={handleOverlayClick}>
      <div className={styles.modal}>
        <div className={styles.header}>
          <div className={styles.headerLeft}>
            <Workflow className="h-6 w-6 text-gray-600" />
            <h2 className={styles.title}>System Workflow</h2>
          </div>
          <button onClick={onClose} className={styles.closeButton}>
            <X className="h-5 w-5" />
          </button>
        </div>

        <div className={styles.content}>
          <div className={styles.section}>
            <h3 className={styles.sectionTitle}>Processing Workflow</h3>
            <p className={styles.sectionDescription}>
              Click on any step to learn more about that stage of the process
            </p>

            <div className={styles.workflow}>
              {workflowSteps.map((step, index) => (
                <React.Fragment key={step.id}>
                  <div
                    className={`${styles.workflowStep} ${
                      selectedStep === step.id ? styles.workflowStepActive : ""
                    }`}
                    onClick={() => handleStepClick(step.id)}
                    style={{ backgroundColor: step.color }}
                  >
                    <div className={styles.stepIcon}>{step.icon}</div>
                    <div className={styles.stepContent}>
                      <h4 className={styles.stepName}>{step.name}</h4>
                      <p className={styles.stepDescription}>
                        {step.description}
                      </p>
                    </div>
                  </div>

                  {index < workflowSteps.length - 1 && (
                    <div className={styles.arrow}>
                      <ArrowRight className="h-5 w-5 text-gray-400" />
                    </div>
                  )}
                </React.Fragment>
              ))}
            </div>

            {selectedStep && (
              <div className={styles.stepDetails}>
                <div className={styles.detailsCard}>
                  <h4 className={styles.detailsTitle}>
                    {workflowSteps.find((step) => step.id === selectedStep)
                      ?.name ||
                      additionalFolders.find(
                        (folder) => folder.id === selectedStep
                      )?.name}
                  </h4>
                  <p className={styles.detailsText}>
                    {workflowSteps.find((step) => step.id === selectedStep)
                      ?.details ||
                      additionalFolders.find(
                        (folder) => folder.id === selectedStep
                      )?.details}
                  </p>
                </div>
              </div>
            )}
          </div>

          <div className={styles.section}>
            <h3 className={styles.sectionTitle}>Additional Folders</h3>
            <div className={styles.additionalFolders}>
              {additionalFolders.map((folder) => (
                <div
                  key={folder.id}
                  className={`${styles.folderCard} ${
                    selectedStep === folder.id ? styles.folderCardActive : ""
                  }`}
                  onClick={() => handleStepClick(folder.id)}
                  style={{ backgroundColor: folder.color }}
                >
                  <div className={styles.folderIcon}>{folder.icon}</div>
                  <div className={styles.folderContent}>
                    <h4 className={styles.folderName}>{folder.name}</h4>
                    <p className={styles.folderDescription}>
                      {folder.description}
                    </p>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default WorkflowModal;
