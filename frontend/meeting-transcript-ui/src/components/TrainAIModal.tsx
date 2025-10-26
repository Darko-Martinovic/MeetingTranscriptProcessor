import React, { useState } from "react";
import {
  X,
  Upload,
  FileText,
  Brain,
  ThumbsUp,
  Meh,
  ThumbsDown,
} from "lucide-react";
import styles from "./TrainAIModal.module.css";

interface ActionItem {
  id: string;
  title: string;
  description: string;
  priority: string;
  type: string;
  assignedTo?: string;
  ticketNumber?: string;
}

interface TrainAIModalProps {
  onClose: () => void;
}

type FeedbackType = "good" | "acceptable" | "poor" | null;

const TrainAIModal: React.FC<TrainAIModalProps> = ({ onClose }) => {
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [processing, setProcessing] = useState(false);
  const [actionItems, setActionItems] = useState<ActionItem[]>([]);
  const [feedback, setFeedback] = useState<FeedbackType>(null);
  const [customPrompt, setCustomPrompt] = useState("");
  const [updatingPrompt, setUpdatingPrompt] = useState(false);
  const [tokensUsed, setTokensUsed] = useState<number>(0);
  const [estimatedCost, setEstimatedCost] = useState<number>(0);

  const handleFileChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (file) {
      setSelectedFile(file);
      setActionItems([]);
      setFeedback(null);
      setCustomPrompt("");
      setTokensUsed(0);
      setEstimatedCost(0);
    }
  };

  const handleProcess = async () => {
    if (!selectedFile) return;

    setProcessing(true);
    try {
      const formData = new FormData();
      formData.append("file", selectedFile);

      const response = await fetch(
        "http://localhost:5000/api/training/process",
        {
          method: "POST",
          body: formData,
        }
      );

      if (!response.ok) {
        throw new Error("Failed to process transcript");
      }

      const data = await response.json();
      setActionItems(data.actionItems || []);
      setTokensUsed(data.tokensUsed || 0);
      setEstimatedCost(data.estimatedCost || 0);
    } catch (error) {
      console.error("Error processing transcript:", error);
      alert("Failed to process transcript. Please try again.");
    } finally {
      setProcessing(false);
    }
  };

  const handleFeedback = (type: FeedbackType) => {
    setFeedback(type);

    // Generate prompt based on feedback
    let promptSuggestion = "";
    switch (type) {
      case "good":
        promptSuggestion =
          "Continue with the current extraction approach. Focus on maintaining accuracy and consistency in identifying action items, assignees, and priorities.";
        break;
      case "acceptable":
        promptSuggestion =
          "Improve extraction by paying more attention to:\n- Context around action items\n- Implicit assignments (when someone says 'I will...')\n- Priority indicators in the discussion\n- Due dates mentioned in various formats";
        break;
      case "poor":
        promptSuggestion =
          "Significantly improve extraction quality by:\n- Being more conservative - only extract clear, explicit action items\n- Better understanding of meeting context and flow\n- Improved detection of who is responsible for what\n- More accurate priority assessment based on urgency language\n- Better handling of follow-up discussions vs new action items";
        break;
    }

    setCustomPrompt(promptSuggestion);
  };

  const handleUpdatePrompt = async () => {
    if (!customPrompt.trim()) return;

    setUpdatingPrompt(true);
    try {
      const response = await fetch(
        "http://localhost:5000/api/configuration/custom-prompt",
        {
          method: "PUT",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify({ customPrompt }),
        }
      );

      if (!response.ok) {
        throw new Error("Failed to update custom prompt");
      }

      alert("Custom prompt updated successfully!");
    } catch (error) {
      console.error("Error updating custom prompt:", error);
      alert("Failed to update custom prompt. Please try again.");
    } finally {
      setUpdatingPrompt(false);
    }
  };

  return (
    <div className={styles.modalOverlay} onClick={onClose}>
      <div className={styles.modalContent} onClick={(e) => e.stopPropagation()}>
        <div className={styles.modalHeader}>
          <div className={styles.headerTitle}>
            <Brain className="h-6 w-6" />
            <h2>Train AI</h2>
          </div>
          <button onClick={onClose} className={styles.closeButton}>
            <X className="h-5 w-5" />
          </button>
        </div>

        <div className={styles.modalBody}>
          {/* File Upload Section */}
          <div className={styles.uploadSection}>
            <h3>Upload Transcript for Training</h3>
            <div className={styles.fileUpload}>
              <input
                type="file"
                id="train-file"
                accept=".txt,.md,.json,.xml,.docx,.pdf,.vtt"
                onChange={handleFileChange}
                className={styles.fileInput}
              />
              <label htmlFor="train-file" className={styles.fileLabel}>
                {selectedFile ? (
                  <>
                    <FileText className="h-5 w-5" />
                    <span>{selectedFile.name}</span>
                  </>
                ) : (
                  <>
                    <Upload className="h-5 w-5" />
                    <span>Choose a transcript file</span>
                  </>
                )}
              </label>
            </div>
            <button
              onClick={handleProcess}
              disabled={!selectedFile || processing}
              className={styles.processButton}
            >
              {processing ? "Processing..." : "Process Transcript"}
            </button>
          </div>

          {/* Results Grid */}
          {actionItems.length > 0 && (
            <div className={styles.resultsSection}>
              <div className={styles.resultsSummary}>
                <h3>Extracted Action Items ({actionItems.length})</h3>
                <div className={styles.metricsDisplay}>
                  {tokensUsed > 0 && (
                    <span className={styles.metric}>
                      Tokens Used:{" "}
                      <strong>{tokensUsed.toLocaleString()}</strong>
                    </span>
                  )}
                  {estimatedCost > 0 && (
                    <span className={styles.metric}>
                      Estimated Cost:{" "}
                      <strong>${estimatedCost.toFixed(4)}</strong>
                    </span>
                  )}
                </div>
              </div>
              <div className={styles.resultsTable}>
                <table>
                  <thead>
                    <tr>
                      <th>Ticket #</th>
                      <th>Title</th>
                      <th>Description</th>
                      <th>Priority</th>
                      <th>Type</th>
                      <th>Assignee</th>
                    </tr>
                  </thead>
                  <tbody>
                    {actionItems.map((item, index) => (
                      <tr key={item.id}>
                        <td>{item.ticketNumber || `TRAIN-${index + 1}`}</td>
                        <td>{item.title}</td>
                        <td className={styles.descriptionCell}>
                          {item.description}
                        </td>
                        <td>
                          <span
                            className={`${styles.priorityBadge} ${
                              styles[`priority${item.priority}`]
                            }`}
                          >
                            {item.priority}
                          </span>
                        </td>
                        <td>
                          <span className={styles.typeBadge}>{item.type}</span>
                        </td>
                        <td>{item.assignedTo || "Unassigned"}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              {/* Feedback Section */}
              <div className={styles.feedbackSection}>
                <h3>How well did the AI perform?</h3>
                <div className={styles.feedbackButtons}>
                  <button
                    onClick={() => handleFeedback("good")}
                    className={`${styles.feedbackButton} ${
                      feedback === "good" ? styles.feedbackActive : ""
                    }`}
                  >
                    <ThumbsUp className="h-5 w-5" />
                    <span>Good</span>
                  </button>
                  <button
                    onClick={() => handleFeedback("acceptable")}
                    className={`${styles.feedbackButton} ${
                      feedback === "acceptable" ? styles.feedbackActive : ""
                    }`}
                  >
                    <Meh className="h-5 w-5" />
                    <span>Acceptable</span>
                  </button>
                  <button
                    onClick={() => handleFeedback("poor")}
                    className={`${styles.feedbackButton} ${
                      feedback === "poor" ? styles.feedbackActive : ""
                    }`}
                  >
                    <ThumbsDown className="h-5 w-5" />
                    <span>Poor</span>
                  </button>
                </div>
              </div>

              {/* Custom Prompt Editor */}
              {feedback && (
                <div className={styles.promptSection}>
                  <h3>Custom Training Prompt</h3>
                  <textarea
                    value={customPrompt}
                    onChange={(e) => setCustomPrompt(e.target.value)}
                    className={styles.promptTextarea}
                    rows={6}
                    placeholder="Enter custom prompt to guide AI behavior..."
                  />
                  <button
                    onClick={handleUpdatePrompt}
                    disabled={!customPrompt.trim() || updatingPrompt}
                    className={styles.updateButton}
                  >
                    {updatingPrompt ? "Updating..." : "Update Custom Prompt"}
                  </button>
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default TrainAIModal;
