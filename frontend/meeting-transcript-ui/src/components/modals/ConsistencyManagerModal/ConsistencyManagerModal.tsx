import React, { useState } from "react";
import {
  X,
  Settings,
  Globe,
  FileText,
  Target,
  Clock,
  Lightbulb,
  ArrowRight,
  CheckCircle,
  Users,
  Calendar,
  AlertTriangle,
  Code,
} from "lucide-react";
import styles from "./ConsistencyManagerModal.module.css";

interface ConsistencyManagerModalProps {
  isOpen: boolean;
  onClose: () => void;
}

interface MeetingTypeInfo {
  id: string;
  name: string;
  icon: React.ReactNode;
  description: string;
  keywords: string[];
  focusAreas: string[];
  extractionParams: {
    temperature: number;
    maxTokens: number;
    focusOnImmediate: boolean;
    specialRules?: string[];
  };
  examples: {
    typical: string;
    actionItems: string[];
  };
}

interface LanguageSupport {
  code: string;
  name: string;
  flag: string;
  keywords: string[];
  actionVerbs: string[];
  example: string;
}

const meetingTypes: MeetingTypeInfo[] = [
  {
    id: "standup",
    name: "Daily Standup",
    icon: <Users className={styles.typeIcon} />,
    description:
      "Quick daily synchronization meetings focused on immediate blockers and today's tasks",
    keywords: ["standup", "daily", "scrum", "sprint check", "status update"],
    focusAreas: [
      "Blockers to resolve",
      "Tasks for today",
      "Status updates requiring action",
    ],
    extractionParams: {
      temperature: 0.05,
      maxTokens: 2000,
      focusOnImmediate: true,
      specialRules: [
        "Max 1 day timeframe",
        "Requires assignee",
        "Minimal action words needed",
      ],
    },
    examples: {
      typical:
        "Daily team sync discussing yesterday's progress, today's plan, and any blockers",
      actionItems: [
        "John will fix the login bug blocking QA testing",
        "Sarah needs help with database migration - will sync with DevOps team",
        "Team will review sprint backlog after deployment",
      ],
    },
  },
  {
    id: "sprint",
    name: "Sprint Planning",
    icon: <Calendar className={styles.typeIcon} />,
    description:
      "Sprint ceremonies including planning, review, and retrospectives with clear deliverables",
    keywords: [
      "sprint planning",
      "sprint review",
      "sprint retrospective",
      "backlog",
    ],
    focusAreas: [
      "Story assignments",
      "Sprint commitments",
      "Backlog refinements",
      "Impediment removal",
    ],
    extractionParams: {
      temperature: 0.1,
      maxTokens: 4000,
      focusOnImmediate: false,
      specialRules: [
        "Sprint-specific deadlines",
        "Story point estimation",
        "Clear deliverables required",
      ],
    },
    examples: {
      typical:
        "Planning next sprint with story prioritization, estimation, and team capacity discussion",
      actionItems: [
        "Alice will implement user authentication feature (8 points) by sprint end",
        "Team will refine backlog items for payment integration next week",
        "Bob will investigate performance issues in the reporting module",
      ],
    },
  },
  {
    id: "architecture",
    name: "Architecture Review",
    icon: <Code className={styles.typeIcon} />,
    description:
      "Technical design discussions requiring implementation decisions and documentation",
    keywords: [
      "architecture",
      "design review",
      "technical design",
      "system design",
    ],
    focusAreas: [
      "Design decisions",
      "Technical debt items",
      "Architectural changes",
      "Documentation updates",
    ],
    extractionParams: {
      temperature: 0.15,
      maxTokens: 5000,
      focusOnImmediate: false,
      specialRules: [
        "Longer timeframes allowed",
        "Technical complexity considered",
        "Documentation focus",
      ],
    },
    examples: {
      typical:
        "Review of microservices architecture with discussion on API design and data flow",
      actionItems: [
        "David will create API specification document for user service by next Friday",
        "Team will evaluate database migration strategy for Q2",
        "Lisa will update architecture diagram with new service dependencies",
      ],
    },
  },
  {
    id: "incident",
    name: "Incident Response",
    icon: <AlertTriangle className={styles.typeIcon} />,
    description:
      "Critical issue resolution with immediate fixes and preventive measures",
    keywords: [
      "incident",
      "postmortem",
      "outage",
      "root cause",
      "incident response",
    ],
    focusAreas: [
      "Immediate fixes",
      "Investigation tasks",
      "Preventive measures",
      "Follow-up actions",
    ],
    extractionParams: {
      temperature: 0.05,
      maxTokens: 3000,
      focusOnImmediate: true,
      specialRules: [
        "High priority focus",
        "Urgent timeframes",
        "Clear ownership required",
        "Priority levels assigned",
      ],
    },
    examples: {
      typical:
        "Post-incident review of database outage with root cause analysis and prevention planning",
      actionItems: [
        "Mike will implement database connection pooling fix by EOD (HIGH PRIORITY)",
        "Team will add monitoring alerts for connection saturation by tomorrow",
        "Jennifer will document incident timeline and share with leadership",
      ],
    },
  },
  {
    id: "project",
    name: "Project Planning",
    icon: <Target className={styles.typeIcon} />,
    description:
      "Strategic planning sessions with roadmaps, milestones, and deliverable planning",
    keywords: [
      "project planning",
      "roadmap",
      "milestone",
      "timeline",
      "deliverable",
    ],
    focusAreas: [
      "Milestone planning",
      "Resource allocation",
      "Timeline definition",
      "Deliverable tracking",
    ],
    extractionParams: {
      temperature: 0.1,
      maxTokens: 4000,
      focusOnImmediate: false,
      specialRules: [
        "Long-term planning focus",
        "Milestone-driven deadlines",
        "Strategic alignment",
      ],
    },
    examples: {
      typical:
        "Quarterly planning session defining product roadmap and major feature releases",
      actionItems: [
        "Product team will finalize Q3 feature specifications by month end",
        "Engineering will provide effort estimates for mobile app development",
        "Marketing will create go-to-market strategy for new subscription tier",
      ],
    },
  },
  {
    id: "client",
    name: "Client Meeting",
    icon: <FileText className={styles.typeIcon} />,
    description:
      "External stakeholder meetings with presentations, demos, and client commitments",
    keywords: ["client", "customer", "stakeholder", "demo", "presentation"],
    focusAreas: [
      "Client commitments",
      "Follow-up actions",
      "Demo preparations",
      "Stakeholder updates",
    ],
    extractionParams: {
      temperature: 0.1,
      maxTokens: 4000,
      focusOnImmediate: false,
      specialRules: [
        "Client-facing focus",
        "Professional commitments",
        "Clear deliverables",
      ],
    },
    examples: {
      typical:
        "Client demo session with feature walkthrough and feedback collection",
      actionItems: [
        "Sales team will send updated pricing proposal to client by Wednesday",
        "Engineering will implement requested dashboard customization for demo",
        "Account manager will schedule follow-up call to discuss contract terms",
      ],
    },
  },
];

const languageSupport: LanguageSupport[] = [
  {
    code: "en",
    name: "English",
    flag: "🇺🇸",
    keywords: [
      "the",
      "and",
      "action",
      "item",
      "task",
      "should",
      "will",
      "need",
    ],
    actionVerbs: [
      "implement",
      "create",
      "fix",
      "review",
      "update",
      "investigate",
      "analyze",
      "configure",
      "setup",
      "test",
    ],
    example: "John will implement the user authentication feature by Friday",
  },
  {
    code: "fr",
    name: "French",
    flag: "🇫🇷",
    keywords: ["le", "la", "et", "action", "tâche", "doit", "besoin", "faire"],
    actionVerbs: [
      "implémenter",
      "créer",
      "corriger",
      "réviser",
      "mettre à jour",
      "enquêter",
      "analyser",
      "configurer",
    ],
    example:
      "Jean va implémenter la fonctionnalité d'authentification avant vendredi",
  },
  {
    code: "nl",
    name: "Dutch",
    flag: "🇳🇱",
    keywords: ["de", "het", "en", "actie", "taak", "moet", "zal", "nodig"],
    actionVerbs: [
      "implementeren",
      "creëren",
      "corrigeren",
      "herzien",
      "bijwerken",
      "onderzoeken",
      "analyseren",
      "configureren",
    ],
    example: "Jan gaat de authenticatiefunctie implementeren voor vrijdag",
  },
];

export const ConsistencyManagerModal: React.FC<
  ConsistencyManagerModalProps
> = ({ isOpen, onClose }) => {
  const [selectedMeetingType, setSelectedMeetingType] = useState<string | null>(
    null
  );
  const [selectedLanguage, setSelectedLanguage] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<
    "types" | "languages" | "workflow"
  >("types");

  if (!isOpen) return null;

  const handleMeetingTypeClick = (typeId: string) => {
    setSelectedMeetingType(selectedMeetingType === typeId ? null : typeId);
  };

  const handleLanguageClick = (langCode: string) => {
    setSelectedLanguage(selectedLanguage === langCode ? null : langCode);
  };

  return (
    <div className={styles.overlay}>
      <div className={styles.modal}>
        <div className={styles.header}>
          <div className={styles.titleSection}>
            <Settings className={styles.headerIcon} />
            <div>
              <h2 className={styles.title}>Consistency Manager</h2>
              <p className={styles.subtitle}>
                Context-Aware Action Item Extraction System
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
              The Consistency Manager automatically adapts AI extraction
              behavior based on meeting type and language, ensuring contextually
              appropriate action items with optimal accuracy across different
              scenarios and international teams.
            </p>
          </div>

          <div className={styles.tabNavigation}>
            <button
              className={`${styles.tab} ${
                activeTab === "types" ? styles.activeTab : ""
              }`}
              onClick={() => setActiveTab("types")}
            >
              <FileText className={styles.tabIcon} />
              Meeting Types
            </button>
            <button
              className={`${styles.tab} ${
                activeTab === "languages" ? styles.activeTab : ""
              }`}
              onClick={() => setActiveTab("languages")}
            >
              <Globe className={styles.tabIcon} />
              Languages
            </button>
            <button
              className={`${styles.tab} ${
                activeTab === "workflow" ? styles.activeTab : ""
              }`}
              onClick={() => setActiveTab("workflow")}
            >
              <Lightbulb className={styles.tabIcon} />
              How It Works
            </button>
          </div>

          {activeTab === "types" && (
            <div className={styles.tabContent}>
              <h3>Meeting Type Recognition</h3>
              <p className={styles.tabDescription}>
                The system automatically classifies meetings and adapts
                extraction parameters for optimal results based on context and
                expected outcomes.
              </p>

              <div className={styles.meetingTypesGrid}>
                {meetingTypes.map((type) => (
                  <div
                    key={type.id}
                    className={`${styles.meetingTypeCard} ${
                      selectedMeetingType === type.id ? styles.expanded : ""
                    }`}
                    onClick={() => handleMeetingTypeClick(type.id)}
                  >
                    <div className={styles.cardHeader}>
                      {type.icon}
                      <div className={styles.cardInfo}>
                        <h4 className={styles.cardTitle}>{type.name}</h4>
                        <p className={styles.cardDescription}>
                          {type.description}
                        </p>
                      </div>
                      <ArrowRight
                        className={`${styles.expandIcon} ${
                          selectedMeetingType === type.id ? styles.rotated : ""
                        }`}
                      />
                    </div>

                    {selectedMeetingType === type.id && (
                      <div className={styles.cardDetails}>
                        <div className={styles.detailsGrid}>
                          <div className={styles.detailSection}>
                            <h5>Recognition Keywords</h5>
                            <div className={styles.keywordList}>
                              {type.keywords.map((keyword, idx) => (
                                <span key={idx} className={styles.keyword}>
                                  {keyword}
                                </span>
                              ))}
                            </div>
                          </div>

                          <div className={styles.detailSection}>
                            <h5>Focus Areas</h5>
                            <ul className={styles.focusList}>
                              {type.focusAreas.map((area, idx) => (
                                <li key={idx}>{area}</li>
                              ))}
                            </ul>
                          </div>

                          <div className={styles.detailSection}>
                            <h5>Extraction Parameters</h5>
                            <div className={styles.parametersList}>
                              <div className={styles.parameter}>
                                <span className={styles.paramLabel}>
                                  Temperature:
                                </span>
                                <span className={styles.paramValue}>
                                  {type.extractionParams.temperature}
                                </span>
                              </div>
                              <div className={styles.parameter}>
                                <span className={styles.paramLabel}>
                                  Max Tokens:
                                </span>
                                <span className={styles.paramValue}>
                                  {type.extractionParams.maxTokens}
                                </span>
                              </div>
                              <div className={styles.parameter}>
                                <span className={styles.paramLabel}>
                                  Focus:
                                </span>
                                <span className={styles.paramValue}>
                                  {type.extractionParams.focusOnImmediate
                                    ? "Immediate"
                                    : "Long-term"}
                                </span>
                              </div>
                            </div>
                            {type.extractionParams.specialRules && (
                              <div className={styles.specialRules}>
                                <h6>Special Rules:</h6>
                                <ul>
                                  {type.extractionParams.specialRules.map(
                                    (rule, idx) => (
                                      <li key={idx}>{rule}</li>
                                    )
                                  )}
                                </ul>
                              </div>
                            )}
                          </div>

                          <div className={styles.detailSection}>
                            <h5>Example Action Items</h5>
                            <div className={styles.exampleContext}>
                              <p className={styles.contextDescription}>
                                {type.examples.typical}
                              </p>
                              <div className={styles.actionItemsList}>
                                {type.examples.actionItems.map((item, idx) => (
                                  <div key={idx} className={styles.actionItem}>
                                    <CheckCircle
                                      className={styles.actionIcon}
                                    />
                                    <span>{item}</span>
                                  </div>
                                ))}
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

          {activeTab === "languages" && (
            <div className={styles.tabContent}>
              <h3>Multi-Language Support</h3>
              <p className={styles.tabDescription}>
                Automatic language detection with localized action verb
                recognition and culturally appropriate prompt generation for
                international teams.
              </p>

              <div className={styles.languagesGrid}>
                {languageSupport.map((lang) => (
                  <div
                    key={lang.code}
                    className={`${styles.languageCard} ${
                      selectedLanguage === lang.code ? styles.expanded : ""
                    }`}
                    onClick={() => handleLanguageClick(lang.code)}
                  >
                    <div className={styles.cardHeader}>
                      <div className={styles.languageFlag}>{lang.flag}</div>
                      <div className={styles.cardInfo}>
                        <h4 className={styles.cardTitle}>{lang.name}</h4>
                        <p className={styles.cardDescription}>
                          Native language processing and action detection
                        </p>
                      </div>
                      <ArrowRight
                        className={`${styles.expandIcon} ${
                          selectedLanguage === lang.code ? styles.rotated : ""
                        }`}
                      />
                    </div>

                    {selectedLanguage === lang.code && (
                      <div className={styles.cardDetails}>
                        <div className={styles.detailsGrid}>
                          <div className={styles.detailSection}>
                            <h5>Detection Keywords</h5>
                            <div className={styles.keywordList}>
                              {lang.keywords.map((keyword, idx) => (
                                <span key={idx} className={styles.keyword}>
                                  {keyword}
                                </span>
                              ))}
                            </div>
                          </div>

                          <div className={styles.detailSection}>
                            <h5>Action Verbs</h5>
                            <div className={styles.keywordList}>
                              {lang.actionVerbs.map((verb, idx) => (
                                <span key={idx} className={styles.actionVerb}>
                                  {verb}
                                </span>
                              ))}
                            </div>
                          </div>

                          <div className={styles.detailSection}>
                            <h5>Example Action Item</h5>
                            <div className={styles.languageExample}>
                              <CheckCircle className={styles.actionIcon} />
                              <span className={styles.exampleText}>
                                {lang.example}
                              </span>
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
              <h3>Adaptive Extraction Workflow</h3>
              <p className={styles.tabDescription}>
                5-step intelligent process that analyzes context and
                automatically optimizes extraction parameters for maximum
                accuracy and relevance.
              </p>

              <div className={styles.workflowSteps}>
                <div className={styles.workflowStep}>
                  <div className={styles.stepNumber}>1</div>
                  <div className={styles.stepContent}>
                    <h4>Meeting Type Detection</h4>
                    <p>
                      Analyzes meeting title, content, and participant count to
                      classify meeting type using keyword patterns and
                      heuristics.
                    </p>
                    <div className={styles.stepDetails}>
                      <span>• Keyword pattern matching</span>
                      <span>• Participant count analysis</span>
                      <span>• Content length heuristics</span>
                    </div>
                  </div>
                </div>

                <div className={styles.workflowStep}>
                  <div className={styles.stepNumber}>2</div>
                  <div className={styles.stepContent}>
                    <h4>Language Recognition</h4>
                    <p>
                      Identifies primary language using common word patterns and
                      linguistic markers for accurate processing.
                    </p>
                    <div className={styles.stepDetails}>
                      <span>• Common word frequency analysis</span>
                      <span>• Linguistic pattern recognition</span>
                      <span>• Multi-language support</span>
                    </div>
                  </div>
                </div>

                <div className={styles.workflowStep}>
                  <div className={styles.stepNumber}>3</div>
                  <div className={styles.stepContent}>
                    <h4>Context-Aware Prompting</h4>
                    <p>
                      Generates specialized system prompts tailored to meeting
                      type and language for optimal AI guidance.
                    </p>
                    <div className={styles.stepDetails}>
                      <span>• Type-specific focus areas</span>
                      <span>• Language-localized instructions</span>
                      <span>• Consistency rule enforcement</span>
                    </div>
                  </div>
                </div>

                <div className={styles.workflowStep}>
                  <div className={styles.stepNumber}>4</div>
                  <div className={styles.stepContent}>
                    <h4>Parameter Optimization</h4>
                    <p>
                      Configures AI extraction parameters (temperature, tokens,
                      focus) based on meeting context and requirements.
                    </p>
                    <div className={styles.stepDetails}>
                      <span>• Temperature adjustment</span>
                      <span>• Token limit optimization</span>
                      <span>• Focus area prioritization</span>
                    </div>
                  </div>
                </div>

                <div className={styles.workflowStep}>
                  <div className={styles.stepNumber}>5</div>
                  <div className={styles.stepContent}>
                    <h4>Validation Rule Application</h4>
                    <p>
                      Applies meeting-specific validation rules for required
                      fields, timeframes, and quality assurance.
                    </p>
                    <div className={styles.stepDetails}>
                      <span>• Required field validation</span>
                      <span>• Timeframe constraints</span>
                      <span>• Quality thresholds</span>
                    </div>
                  </div>
                </div>
              </div>

              <div className={styles.benefitsSection}>
                <h4>System Benefits</h4>
                <div className={styles.benefitsList}>
                  <div className={styles.benefit}>
                    <Target className={styles.benefitIcon} />
                    <div>
                      <h5>Contextual Accuracy</h5>
                      <p>
                        Automatically adapts to meeting context for more
                        relevant action item extraction
                      </p>
                    </div>
                  </div>
                  <div className={styles.benefit}>
                    <Globe className={styles.benefitIcon} />
                    <div>
                      <h5>Multi-Language Support</h5>
                      <p>
                        Seamlessly processes meetings in English, French, and
                        Dutch with native accuracy
                      </p>
                    </div>
                  </div>
                  <div className={styles.benefit}>
                    <Clock className={styles.benefitIcon} />
                    <div>
                      <h5>Intelligent Prioritization</h5>
                      <p>
                        Automatically adjusts urgency and timeframes based on
                        meeting type and context
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
