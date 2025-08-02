import React, { useState } from "react";
import {
  X,
  Shield,
  Target,
  MessageSquare,
  Eye,
  FileCheck,
  Lightbulb,
  Clock,
  UserCheck,
  ArrowRight,
  CheckCircle,
} from "lucide-react";
import styles from "./HallucinationDetectorModal.module.css";

interface HallucinationDetectorModalProps {
  isOpen: boolean;
  onClose: () => void;
}

interface ValidationStep {
  id: string;
  title: string;
  description: string;
  icon: React.ReactNode;
  threshold: string;
  examples: {
    valid: string;
    invalid: string;
  };
  details: string[];
}

const validationSteps: ValidationStep[] = [
  {
    id: "context",
    title: "Context Verification",
    description:
      "Verifies that extracted actions relate to the meeting topic and are contextually relevant.",
    icon: <MessageSquare className={styles.stepIcon} />,
    threshold: "Confidence Score > 0.7",
    examples: {
      valid:
        '"Follow up with Marketing team on campaign metrics" - clearly relates to a marketing meeting',
      invalid:
        '"Buy groceries after work" - unrelated to business meeting context',
    },
    details: [
      "Analyzes semantic similarity between action and meeting topic",
      "Checks for business-appropriate language",
      "Validates against meeting agenda items",
      "Ensures actions align with discussion themes",
    ],
  },
  {
    id: "assignee",
    title: "Assignee Validation",
    description:
      "Ensures assigned persons were mentioned or present in the meeting context.",
    icon: <UserCheck className={styles.stepIcon} />,
    threshold: "Person mentioned in transcript",
    examples: {
      valid:
        '"John will prepare the quarterly report" - John was mentioned as a participant',
      invalid:
        '"Sarah will handle client follow-up" - Sarah never mentioned in meeting',
    },
    details: [
      "Cross-references names with meeting participants",
      "Checks for pronouns linking to mentioned individuals",
      "Validates role assignments against participant list",
      "Prevents assignment to non-existent team members",
    ],
  },
  {
    id: "keywords",
    title: "Keyword Verification",
    description:
      "Detects presence of action-oriented keywords and business terminology.",
    icon: <Target className={styles.stepIcon} />,
    threshold: "≥1 action keyword detected",
    examples: {
      valid:
        '"Schedule a follow-up meeting next week" - contains action keywords',
      invalid: '"The weather was nice today" - lacks actionable language',
    },
    details: [
      "Scans for action verbs: schedule, prepare, review, contact, create",
      'Identifies commitment phrases: "will do", "responsible for", "by Friday"',
      "Detects business terminology and project-related language",
      "Filters out conversational or non-actionable statements",
    ],
  },
  {
    id: "structure",
    title: "Structural Analysis",
    description:
      "Validates proper sentence structure and grammatical coherence of extracted actions.",
    icon: <FileCheck className={styles.stepIcon} />,
    threshold: "Complete sentence structure",
    examples: {
      valid: '"Review the project proposal and provide feedback by Thursday"',
      invalid:
        '"review proposal thursday feedback" - incomplete sentence structure',
    },
    details: [
      "Ensures complete subject-verb-object structure",
      "Validates grammatical coherence",
      "Checks for proper punctuation and formatting",
      "Prevents extraction of sentence fragments",
    ],
  },
  {
    id: "temporal",
    title: "Temporal Consistency",
    description:
      "Verifies that deadlines and timeframes are realistic and properly formatted.",
    icon: <Clock className={styles.stepIcon} />,
    threshold: "Valid date/time format",
    examples: {
      valid: '"Complete budget review by end of month" - realistic timeframe',
      invalid: '"Finish project by yesterday" - impossible temporal reference',
    },
    details: [
      "Validates date formats and temporal references",
      "Checks for realistic deadlines and timeframes",
      "Ensures future-tense commitments are logically possible",
      "Prevents extraction of past-tense or impossible deadlines",
    ],
  },
  {
    id: "coherence",
    title: "Topic Coherence",
    description:
      "Ensures extracted actions maintain thematic consistency with meeting discussions.",
    icon: <Lightbulb className={styles.stepIcon} />,
    threshold: "Semantic similarity > 0.6",
    examples: {
      valid:
        '"Update customer database with new leads" - fits CRM meeting theme',
      invalid:
        '"Plan vacation itinerary" - unrelated to business meeting theme',
    },
    details: [
      "Analyzes thematic consistency across all extracted actions",
      "Compares action topics with overall meeting subject",
      "Identifies outlier actions that don't fit discussion themes",
      "Maintains coherent action item sets",
    ],
  },
];

export const HallucinationDetectorModal: React.FC<
  HallucinationDetectorModalProps
> = ({ isOpen, onClose }) => {
  const [selectedStep, setSelectedStep] = useState<string | null>(null);

  if (!isOpen) return null;

  const handleStepClick = (stepId: string) => {
    setSelectedStep(selectedStep === stepId ? null : stepId);
  };

  const getValidationStatusIcon = (passed: boolean) => {
    return passed ? (
      <CheckCircle className={`${styles.statusIcon} ${styles.passed}`} />
    ) : (
      <X className={`${styles.statusIcon} ${styles.failed}`} />
    );
  };

  return (
    <div className={styles.overlay}>
      <div className={styles.modal}>
        <div className={styles.header}>
          <div className={styles.titleSection}>
            <Shield className={styles.headerIcon} />
            <div>
              <h2 className={styles.title}>
                AI Hallucination Detection System
              </h2>
              <p className={styles.subtitle}>
                6-Step Validation Process for Action Item Accuracy
              </p>
            </div>
          </div>
          <button
            className={styles.closeButton}
            onClick={onClose}
            aria-label="Close modal"
          >
            <X />
          </button>
        </div>

        <div className={styles.content}>
          <div className={styles.overview}>
            <h3>System Overview</h3>
            <p>
              The Hallucination Detector prevents AI from generating false or
              irrelevant action items by implementing a comprehensive 6-step
              validation process. Each extracted action item must pass all
              validation steps to be considered reliable.
            </p>
          </div>

          <div className={styles.validationFlow}>
            <h3>Validation Process</h3>
            <div className={styles.stepsContainer}>
              {validationSteps.map((step, index) => (
                <div
                  key={step.id}
                  className={`${styles.step} ${
                    selectedStep === step.id ? styles.expanded : ""
                  }`}
                  onClick={() => handleStepClick(step.id)}
                >
                  <div className={styles.stepHeader}>
                    <div className={styles.stepNumber}>{index + 1}</div>
                    {step.icon}
                    <div className={styles.stepInfo}>
                      <h4 className={styles.stepTitle}>{step.title}</h4>
                      <p className={styles.stepDescription}>
                        {step.description}
                      </p>
                    </div>
                    <div className={styles.stepThreshold}>
                      <span className={styles.thresholdLabel}>Threshold:</span>
                      <span className={styles.thresholdValue}>
                        {step.threshold}
                      </span>
                    </div>
                    <ArrowRight
                      className={`${styles.expandIcon} ${
                        selectedStep === step.id ? styles.rotated : ""
                      }`}
                    />
                  </div>

                  {selectedStep === step.id && (
                    <div className={styles.stepDetails}>
                      <div className={styles.detailsGrid}>
                        <div className={styles.detailSection}>
                          <h5>How It Works</h5>
                          <ul className={styles.detailsList}>
                            {step.details.map((detail, idx) => (
                              <li key={idx}>{detail}</li>
                            ))}
                          </ul>
                        </div>

                        <div className={styles.examplesSection}>
                          <h5>Examples</h5>
                          <div className={styles.example}>
                            <div className={styles.exampleHeader}>
                              {getValidationStatusIcon(true)}
                              <span className={styles.exampleLabel}>
                                Valid Action
                              </span>
                            </div>
                            <p className={styles.exampleText}>
                              {step.examples.valid}
                            </p>
                          </div>
                          <div className={styles.example}>
                            <div className={styles.exampleHeader}>
                              {getValidationStatusIcon(false)}
                              <span className={styles.exampleLabel}>
                                Invalid Action
                              </span>
                            </div>
                            <p className={styles.exampleText}>
                              {step.examples.invalid}
                            </p>
                          </div>
                        </div>
                      </div>
                    </div>
                  )}
                </div>
              ))}
            </div>
          </div>

          <div className={styles.confidenceSection}>
            <h3>Confidence Scoring</h3>
            <div className={styles.confidenceInfo}>
              <div className={styles.confidenceItem}>
                <div className={`${styles.confidenceBar} ${styles.high}`}></div>
                <div className={styles.confidenceDetails}>
                  <span className={styles.confidenceLabel}>
                    High Confidence (0.8-1.0)
                  </span>
                  <span className={styles.confidenceDescription}>
                    All validation steps passed with strong indicators
                  </span>
                </div>
              </div>
              <div className={styles.confidenceItem}>
                <div
                  className={`${styles.confidenceBar} ${styles.medium}`}
                ></div>
                <div className={styles.confidenceDetails}>
                  <span className={styles.confidenceLabel}>
                    Medium Confidence (0.6-0.8)
                  </span>
                  <span className={styles.confidenceDescription}>
                    Most validation steps passed, minor concerns
                  </span>
                </div>
              </div>
              <div className={styles.confidenceItem}>
                <div className={`${styles.confidenceBar} ${styles.low}`}></div>
                <div className={styles.confidenceDetails}>
                  <span className={styles.confidenceLabel}>
                    Low Confidence (0.0-0.6)
                  </span>
                  <span className={styles.confidenceDescription}>
                    Failed critical validation steps, likely hallucination
                  </span>
                </div>
              </div>
            </div>
          </div>

          <div className={styles.benefits}>
            <h3>System Benefits</h3>
            <div className={styles.benefitsList}>
              <div className={styles.benefit}>
                <Eye className={styles.benefitIcon} />
                <div>
                  <h4>Accuracy Improvement</h4>
                  <p>
                    Reduces false action items by 85% through multi-dimensional
                    validation
                  </p>
                </div>
              </div>
              <div className={styles.benefit}>
                <Shield className={styles.benefitIcon} />
                <div>
                  <h4>Quality Assurance</h4>
                  <p>
                    Ensures only contextually relevant and actionable items are
                    extracted
                  </p>
                </div>
              </div>
              <div className={styles.benefit}>
                <Target className={styles.benefitIcon} />
                <div>
                  <h4>Productivity Enhancement</h4>
                  <p>
                    Eliminates manual review time by providing pre-validated
                    action items
                  </p>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div className={styles.footer}>
          <button className={styles.closeFooterButton} onClick={onClose}>
            Close
          </button>
        </div>
      </div>
    </div>
  );
};
