import React, { useState } from "react";
import {
  X,
  CheckCircle2,
  AlertCircle,
  TrendingUp,
  BarChart3,
  Target,
  Crosshair,
  FileText,
  Layers2,
  ArrowRight,
  CheckSquare,
  AlertTriangle,
  ThumbsUp,
  ThumbsDown,
  Zap,
} from "lucide-react";
import styles from "./ActionItemValidatorModal.module.css";

interface ActionItemValidatorModalProps {
  isOpen: boolean;
  onClose: () => void;
}

interface ValidationTechnique {
  id: string;
  name: string;
  icon: React.ReactNode;
  description: string;
  weight: number;
  purpose: string;
  methodology: string[];
  examples: {
    good: {
      scenario: string;
      score: number;
      reason: string;
    };
    poor: {
      scenario: string;
      score: number;
      reason: string;
    };
  };
  metrics: {
    threshold: number;
    highQuality: string;
    mediumQuality: string;
    lowQuality: string;
  };
}

interface ValidationStep {
  step: number;
  title: string;
  description: string;
  icon: React.ReactNode;
  details: string[];
}

const validationTechniques: ValidationTechnique[] = [
  {
    id: "cross-validation",
    name: "Cross-Validation Scoring",
    icon: <Crosshair className={styles.techniqueIcon} />,
    description:
      "Compares AI extraction results against rule-based extraction for consistency verification",
    weight: 30,
    purpose:
      "Ensures AI extraction aligns with deterministic rule-based patterns for reliability",
    methodology: [
      "Execute parallel AI and rule-based extraction",
      "Calculate text similarity between extracted items",
      "Apply Jaccard-like similarity scoring",
      "Identify matching action items across methods",
    ],
    examples: {
      good: {
        scenario:
          'AI extracts "John will fix login bug by Friday" and rule-based finds "Fix login issue - John - Friday"',
        score: 0.85,
        reason:
          "High similarity in title, assignee, and deadline despite different wording",
      },
      poor: {
        scenario:
          'AI extracts "Team will brainstorm ideas" but rule-based finds "Schedule architecture review"',
        score: 0.15,
        reason: "No meaningful overlap between AI and rule-based results",
      },
    },
    metrics: {
      threshold: 0.7,
      highQuality: "Strong alignment (>0.8) indicates reliable extraction",
      mediumQuality:
        "Moderate alignment (0.5-0.8) suggests careful review needed",
      lowQuality:
        "Poor alignment (<0.5) indicates potential hallucination or missed items",
    },
  },
  {
    id: "context-coherence",
    name: "Context Coherence Validation",
    icon: <FileText className={styles.techniqueIcon} />,
    description:
      "Verifies that extracted action items make logical sense within the meeting context",
    weight: 30,
    purpose:
      "Prevents extraction of action items that are contextually irrelevant or impossible",
    methodology: [
      "Extract keywords from action item titles and descriptions",
      "Check keyword presence in original transcript",
      "Verify assignee names match meeting participants",
      "Validate context snippets exist in source material",
    ],
    examples: {
      good: {
        scenario:
          'Action item "Sarah will update database schema" where Sarah is a participant and database is discussed',
        score: 0.92,
        reason: "Keywords, assignee, and context all verified in transcript",
      },
      poor: {
        scenario:
          'Action item "Buy groceries after work" extracted from technical architecture meeting',
        score: 0.25,
        reason: "No contextual relevance to meeting topic or participants",
      },
    },
    metrics: {
      threshold: 0.6,
      highQuality: "High coherence (>0.8) means strong contextual alignment",
      mediumQuality:
        "Medium coherence (0.4-0.8) suggests partial context match",
      lowQuality:
        "Low coherence (<0.4) indicates likely false positive extraction",
    },
  },
  {
    id: "keyword-presence",
    name: "Keyword Presence Validation",
    icon: <Target className={styles.techniqueIcon} />,
    description:
      "Ensures action-oriented keywords exist in both transcript and extracted items",
    weight: 20,
    purpose:
      "Validates that genuine actionable language was present in the original discussion",
    methodology: [
      "Scan transcript for action-oriented keywords",
      "Identify actionable verbs in extracted items",
      "Check alignment between transcript and item keywords",
      "Score based on keyword density and relevance",
    ],
    examples: {
      good: {
        scenario:
          'Transcript contains "need to implement" and extracted item is "Implement user authentication feature"',
        score: 0.88,
        reason: "Strong presence of action verbs in both source and extraction",
      },
      poor: {
        scenario:
          "Transcript has no action keywords but AI extracts multiple action items",
        score: 0.2,
        reason:
          "Mismatch between source language patterns and extraction results",
      },
    },
    metrics: {
      threshold: 0.5,
      highQuality: "Rich keywords (>0.8) indicate strong actionable content",
      mediumQuality:
        "Moderate keywords (0.4-0.8) suggest some actionable language",
      lowQuality:
        "Poor keywords (<0.4) may indicate over-extraction or hallucination",
    },
  },
  {
    id: "structural-integrity",
    name: "Structural Integrity Validation",
    icon: <CheckSquare className={styles.techniqueIcon} />,
    description:
      "Validates the structural quality and completeness of extracted action items",
    weight: 20,
    purpose:
      "Ensures extracted items have proper format, meaningful content, and actionable verbs",
    methodology: [
      "Check title and description length and quality",
      "Validate presence of actionable verbs",
      "Assess grammatical structure and completeness",
      "Score based on structural completeness criteria",
    ],
    examples: {
      good: {
        scenario:
          'Well-formed item: "Review API documentation and provide feedback by next Tuesday"',
        score: 0.95,
        reason:
          "Complete title, clear action verb, specific timeline, proper structure",
      },
      poor: {
        scenario: 'Malformed item: "meeting" or "John will..."',
        score: 0.3,
        reason: "Too short, incomplete, or lacks actionable content",
      },
    },
    metrics: {
      threshold: 0.6,
      highQuality:
        "Well-structured (>0.8) items have complete, actionable format",
      mediumQuality: "Adequate structure (0.4-0.8) may need minor improvements",
      lowQuality: "Poor structure (<0.4) indicates incomplete or invalid items",
    },
  },
];

const validationSteps: ValidationStep[] = [
  {
    step: 1,
    title: "Parallel Extraction",
    description: "Execute both AI and rule-based extraction simultaneously",
    icon: <Layers2 className={styles.stepIcon} />,
    details: [
      "AI extracts using contextual understanding",
      "Rule-based extracts using keyword patterns",
      "Both methods process same transcript",
      "Results captured for comparison analysis",
    ],
  },
  {
    step: 2,
    title: "Cross-Validation Analysis",
    description: "Compare AI results against rule-based baseline",
    icon: <Crosshair className={styles.stepIcon} />,
    details: [
      "Calculate text similarity between extracted items",
      "Identify matching action items across methods",
      "Score using Jaccard-like similarity algorithm",
      "Flag significant discrepancies for review",
    ],
  },
  {
    step: 3,
    title: "Context Verification",
    description: "Validate contextual relevance and participant alignment",
    icon: <FileText className={styles.stepIcon} />,
    details: [
      "Extract keywords from action items",
      "Verify keywords exist in original transcript",
      "Check assignee names against participant list",
      "Validate context snippets are genuine",
    ],
  },
  {
    step: 4,
    title: "Quality Assessment",
    description: "Evaluate structural integrity and actionable content",
    icon: <CheckSquare className={styles.stepIcon} />,
    details: [
      "Assess title and description quality",
      "Verify presence of actionable verbs",
      "Check grammatical structure and completeness",
      "Score based on formatting and clarity standards",
    ],
  },
  {
    step: 5,
    title: "Confidence Calculation",
    description: "Compute overall confidence score with weighted metrics",
    icon: <BarChart3 className={styles.stepIcon} />,
    details: [
      "Apply weights: Cross-validation (30%), Context (30%), Keywords (20%), Structure (20%)",
      "Calculate composite confidence score",
      "Generate quality indicators and recommendations",
      "Track validation history for continuous improvement",
    ],
  },
  {
    step: 6,
    title: "Issue Detection",
    description: "Identify false positives and potential missed items",
    icon: <AlertTriangle className={styles.stepIcon} />,
    details: [
      "Detect common false positive patterns",
      "Identify questions misclassified as actions",
      "Flag potential false negatives from rule-based comparison",
      "Generate actionable feedback for system improvement",
    ],
  },
];

const ActionItemValidatorModal: React.FC<ActionItemValidatorModalProps> = ({
  isOpen,
  onClose,
}) => {
  const [selectedTechnique, setSelectedTechnique] = useState<string | null>(
    null
  );
  const [activeTab, setActiveTab] = useState<
    "techniques" | "workflow" | "metrics"
  >("techniques");

  if (!isOpen) return null;

  const handleTechniqueClick = (techniqueId: string) => {
    setSelectedTechnique(
      selectedTechnique === techniqueId ? null : techniqueId
    );
  };

  const getConfidenceColor = (score: number) => {
    if (score >= 0.8) return styles.highConfidence;
    if (score >= 0.5) return styles.mediumConfidence;
    return styles.lowConfidence;
  };

  const getConfidenceIcon = (score: number) => {
    if (score >= 0.8) return <ThumbsUp className={styles.confidenceIcon} />;
    if (score >= 0.5) return <AlertCircle className={styles.confidenceIcon} />;
    return <ThumbsDown className={styles.confidenceIcon} />;
  };

  return (
    <div className={styles.overlay}>
      <div className={styles.modal}>
        <div className={styles.header}>
          <div className={styles.titleSection}>
            <CheckCircle2 className={styles.headerIcon} />
            <div>
              <h2 className={styles.title}>Action Item Validator</h2>
              <p className={styles.subtitle}>
                Multi-Technique Validation & Quality Assurance System
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
              The Action Item Validator ensures extraction quality through
              multi-technique validation, comparing AI results against
              rule-based baselines while verifying contextual coherence,
              structural integrity, and actionable content to prevent false
              positives and missed items.
            </p>
          </div>

          <div className={styles.tabNavigation}>
            <button
              className={`${styles.tab} ${
                activeTab === "techniques" ? styles.activeTab : ""
              }`}
              onClick={() => setActiveTab("techniques")}
            >
              <Target className={styles.tabIcon} />
              Validation Techniques
            </button>
            <button
              className={`${styles.tab} ${
                activeTab === "workflow" ? styles.activeTab : ""
              }`}
              onClick={() => setActiveTab("workflow")}
            >
              <Zap className={styles.tabIcon} />
              Validation Workflow
            </button>
            <button
              className={`${styles.tab} ${
                activeTab === "metrics" ? styles.activeTab : ""
              }`}
              onClick={() => setActiveTab("metrics")}
            >
              <TrendingUp className={styles.tabIcon} />
              Quality Metrics
            </button>
          </div>

          {activeTab === "techniques" && (
            <div className={styles.tabContent}>
              <h3>Validation Techniques</h3>
              <p className={styles.tabDescription}>
                Four complementary validation techniques work together to ensure
                extracted action items are accurate, contextually relevant, and
                structurally sound.
              </p>

              <div className={styles.techniquesGrid}>
                {validationTechniques.map((technique) => (
                  <div
                    key={technique.id}
                    className={`${styles.techniqueCard} ${
                      selectedTechnique === technique.id ? styles.expanded : ""
                    }`}
                    onClick={() => handleTechniqueClick(technique.id)}
                  >
                    <div className={styles.cardHeader}>
                      {technique.icon}
                      <div className={styles.cardInfo}>
                        <div className={styles.cardTitleRow}>
                          <h4 className={styles.cardTitle}>{technique.name}</h4>
                          <div className={styles.weightBadge}>
                            {technique.weight}%
                          </div>
                        </div>
                        <p className={styles.cardDescription}>
                          {technique.description}
                        </p>
                      </div>
                      <ArrowRight
                        className={`${styles.expandIcon} ${
                          selectedTechnique === technique.id
                            ? styles.rotated
                            : ""
                        }`}
                      />
                    </div>

                    {selectedTechnique === technique.id && (
                      <div className={styles.cardDetails}>
                        <div className={styles.detailsGrid}>
                          <div className={styles.detailSection}>
                            <h5>Purpose</h5>
                            <p className={styles.purposeText}>
                              {technique.purpose}
                            </p>
                          </div>

                          <div className={styles.detailSection}>
                            <h5>Methodology</h5>
                            <ul className={styles.methodologyList}>
                              {technique.methodology.map((step, idx) => (
                                <li key={idx}>{step}</li>
                              ))}
                            </ul>
                          </div>

                          <div className={styles.detailSection}>
                            <h5>Quality Examples</h5>
                            <div className={styles.examplesGrid}>
                              <div className={styles.example}>
                                <div className={styles.exampleHeader}>
                                  {getConfidenceIcon(
                                    technique.examples.good.score
                                  )}
                                  <span className={styles.exampleLabel}>
                                    High Quality
                                  </span>
                                  <span
                                    className={`${
                                      styles.scoreValue
                                    } ${getConfidenceColor(
                                      technique.examples.good.score
                                    )}`}
                                  >
                                    {technique.examples.good.score.toFixed(2)}
                                  </span>
                                </div>
                                <p className={styles.exampleScenario}>
                                  {technique.examples.good.scenario}
                                </p>
                                <p className={styles.exampleReason}>
                                  {technique.examples.good.reason}
                                </p>
                              </div>
                              <div className={styles.example}>
                                <div className={styles.exampleHeader}>
                                  {getConfidenceIcon(
                                    technique.examples.poor.score
                                  )}
                                  <span className={styles.exampleLabel}>
                                    Low Quality
                                  </span>
                                  <span
                                    className={`${
                                      styles.scoreValue
                                    } ${getConfidenceColor(
                                      technique.examples.poor.score
                                    )}`}
                                  >
                                    {technique.examples.poor.score.toFixed(2)}
                                  </span>
                                </div>
                                <p className={styles.exampleScenario}>
                                  {technique.examples.poor.scenario}
                                </p>
                                <p className={styles.exampleReason}>
                                  {technique.examples.poor.reason}
                                </p>
                              </div>
                            </div>
                          </div>

                          <div className={styles.detailSection}>
                            <h5>Quality Thresholds</h5>
                            <div className={styles.thresholdsList}>
                              <div
                                className={`${styles.threshold} ${styles.high}`}
                              >
                                <div className={styles.thresholdRange}>
                                  ≥{technique.metrics.threshold + 0.2}
                                </div>
                                <div className={styles.thresholdLabel}>
                                  {technique.metrics.highQuality}
                                </div>
                              </div>
                              <div
                                className={`${styles.threshold} ${styles.medium}`}
                              >
                                <div className={styles.thresholdRange}>
                                  {technique.metrics.threshold - 0.2}-
                                  {technique.metrics.threshold + 0.2}
                                </div>
                                <div className={styles.thresholdLabel}>
                                  {technique.metrics.mediumQuality}
                                </div>
                              </div>
                              <div
                                className={`${styles.threshold} ${styles.low}`}
                              >
                                <div className={styles.thresholdRange}>
                                  &#60;{technique.metrics.threshold - 0.2}
                                </div>
                                <div className={styles.thresholdLabel}>
                                  {technique.metrics.lowQuality}
                                </div>
                              </div>
                            </div>
                          </div>
                        </div>
                      </div>
                    )}
                  </div>
                ))}
              </div>
            </div>
          )}

          {activeTab === "workflow" && (
            <div className={styles.tabContent}>
              <h3>Validation Workflow</h3>
              <p className={styles.tabDescription}>
                6-step comprehensive validation process that analyzes AI
                extraction results through multiple quality dimensions and
                generates actionable insights.
              </p>

              <div className={styles.workflowSteps}>
                {validationSteps.map((step, index) => (
                  <div key={step.step} className={styles.workflowStep}>
                    <div className={styles.stepNumber}>{step.step}</div>
                    <div className={styles.stepContent}>
                      <div className={styles.stepHeader}>
                        {step.icon}
                        <h4 className={styles.stepTitle}>{step.title}</h4>
                      </div>
                      <p className={styles.stepDescription}>
                        {step.description}
                      </p>
                      <div className={styles.stepDetails}>
                        {step.details.map((detail, idx) => (
                          <div key={idx} className={styles.stepDetail}>
                            <span className={styles.bullet}>▸</span>
                            <span>{detail}</span>
                          </div>
                        ))}
                      </div>
                    </div>
                    {index < validationSteps.length - 1 && (
                      <div className={styles.stepConnector}></div>
                    )}
                  </div>
                ))}
              </div>
            </div>
          )}

          {activeTab === "metrics" && (
            <div className={styles.tabContent}>
              <h3>Quality Metrics & Monitoring</h3>
              <p className={styles.tabDescription}>
                Comprehensive metrics tracking system that monitors validation
                performance, identifies trends, and provides insights for
                continuous system improvement.
              </p>

              <div className={styles.metricsGrid}>
                <div className={styles.metricCard}>
                  <div className={styles.metricHeader}>
                    <TrendingUp className={styles.metricIcon} />
                    <h4>Confidence Tracking</h4>
                  </div>
                  <div className={styles.metricContent}>
                    <div className={styles.metricItem}>
                      <span className={styles.metricLabel}>
                        Average Confidence
                      </span>
                      <span className={styles.metricValue}>
                        Based on weighted scores across all techniques
                      </span>
                    </div>
                    <div className={styles.metricItem}>
                      <span className={styles.metricLabel}>
                        High Confidence Rate
                      </span>
                      <span className={styles.metricValue}>
                        Percentage of validations scoring &gt;0.8
                      </span>
                    </div>
                    <div className={styles.metricItem}>
                      <span className={styles.metricLabel}>
                        Low Confidence Rate
                      </span>
                      <span className={styles.metricValue}>
                        Percentage of validations scoring &lt;0.4
                      </span>
                    </div>
                  </div>
                </div>

                <div className={styles.metricCard}>
                  <div className={styles.metricHeader}>
                    <Target className={styles.metricIcon} />
                    <h4>Accuracy Metrics</h4>
                  </div>
                  <div className={styles.metricContent}>
                    <div className={styles.metricItem}>
                      <span className={styles.metricLabel}>
                        Cross-Validation Score
                      </span>
                      <span className={styles.metricValue}>
                        AI vs Rule-based alignment accuracy
                      </span>
                    </div>
                    <div className={styles.metricItem}>
                      <span className={styles.metricLabel}>
                        Context Coherence
                      </span>
                      <span className={styles.metricValue}>
                        Contextual relevance and participant alignment
                      </span>
                    </div>
                    <div className={styles.metricItem}>
                      <span className={styles.metricLabel}>
                        False Positive Rate
                      </span>
                      <span className={styles.metricValue}>
                        Incorrectly extracted non-action items
                      </span>
                    </div>
                  </div>
                </div>

                <div className={styles.metricCard}>
                  <div className={styles.metricHeader}>
                    <AlertTriangle className={styles.metricIcon} />
                    <h4>Issue Detection</h4>
                  </div>
                  <div className={styles.metricContent}>
                    <div className={styles.metricItem}>
                      <span className={styles.metricLabel}>
                        False Positives
                      </span>
                      <span className={styles.metricValue}>
                        Questions, statements, or irrelevant items
                      </span>
                    </div>
                    <div className={styles.metricItem}>
                      <span className={styles.metricLabel}>
                        False Negatives
                      </span>
                      <span className={styles.metricValue}>
                        Valid actions missed by AI extraction
                      </span>
                    </div>
                    <div className={styles.metricItem}>
                      <span className={styles.metricLabel}>
                        Structural Issues
                      </span>
                      <span className={styles.metricValue}>
                        Incomplete, malformed, or unclear items
                      </span>
                    </div>
                  </div>
                </div>

                <div className={styles.metricCard}>
                  <div className={styles.metricHeader}>
                    <BarChart3 className={styles.metricIcon} />
                    <h4>Performance Insights</h4>
                  </div>
                  <div className={styles.metricContent}>
                    <div className={styles.metricItem}>
                      <span className={styles.metricLabel}>
                        Validation History
                      </span>
                      <span className={styles.metricValue}>
                        Track last 100 validations for trend analysis
                      </span>
                    </div>
                    <div className={styles.metricItem}>
                      <span className={styles.metricLabel}>
                        Quality Improvement
                      </span>
                      <span className={styles.metricValue}>
                        Identify patterns for system optimization
                      </span>
                    </div>
                    <div className={styles.metricItem}>
                      <span className={styles.metricLabel}>
                        Continuous Learning
                      </span>
                      <span className={styles.metricValue}>
                        Feedback loop for enhanced accuracy
                      </span>
                    </div>
                  </div>
                </div>
              </div>

              <div className={styles.benefitsSection}>
                <h4>System Benefits</h4>
                <div className={styles.benefitsList}>
                  <div className={styles.benefit}>
                    <CheckCircle2 className={styles.benefitIcon} />
                    <div>
                      <h5>Quality Assurance</h5>
                      <p>
                        Multi-technique validation ensures high-quality action
                        item extraction with confidence scoring
                      </p>
                    </div>
                  </div>
                  <div className={styles.benefit}>
                    <AlertCircle className={styles.benefitIcon} />
                    <div>
                      <h5>Error Prevention</h5>
                      <p>
                        Identifies false positives and false negatives to
                        prevent incorrect or missed action items
                      </p>
                    </div>
                  </div>
                  <div className={styles.benefit}>
                    <TrendingUp className={styles.benefitIcon} />
                    <div>
                      <h5>Continuous Improvement</h5>
                      <p>
                        Validation metrics enable ongoing system optimization
                        and accuracy enhancement
                      </p>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          )}
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

export default ActionItemValidatorModal;
