import React, { useState } from "react";
import {
  X,
  Upload,
  FileText,
  Brain,
  ThumbsUp,
  Meh,
  ThumbsDown,
  Download,
} from "lucide-react";
import styles from "./TrainAIModal.module.css";

// API configuration - set via VITE_API_URL environment variable
const API_BASE_URL = import.meta.env.VITE_API_URL || "http://localhost:5100/api";

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
  const [modelName, setModelName] = useState<string>("");
  const [temperature, setTemperature] = useState<number>(0);
  const [maxTokens, setMaxTokens] = useState<number>(0);

  const handleFileChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (file) {
      setSelectedFile(file);
      setActionItems([]);
      setFeedback(null);
      setCustomPrompt("");
      setTokensUsed(0);
      setEstimatedCost(0);
      setModelName("");
      setTemperature(0);
      setMaxTokens(0);
    }
  };

  const handleProcess = async () => {
    if (!selectedFile) return;

    setProcessing(true);
    try {
      const formData = new FormData();
      formData.append("file", selectedFile);

      const response = await fetch(
        `${API_BASE_URL}/training/process`,
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
      setModelName(data.modelName || "");
      setTemperature(data.temperature || 0);
      setMaxTokens(data.maxTokens || 0);
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
        `${API_BASE_URL}/configuration/custom-prompt`,
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

  const exportToCSV = () => {
    if (actionItems.length === 0) return;

    // Create CSV header
    const headers = ["Ticket #", "Title", "Description", "Priority", "Type", "Assignee"];
    
    // Create CSV rows
    const rows = actionItems.map((item, index) => [
      item.ticketNumber || `TRAIN-${index + 1}`,
      `"${item.title.replace(/"/g, '""')}"`, // Escape quotes
      `"${item.description.replace(/"/g, '""')}"`,
      item.priority,
      item.type,
      item.assignedTo || "Unassigned"
    ]);

    // Combine header and rows
    const csvContent = [
      headers.join(","),
      ...rows.map(row => row.join(","))
    ].join("\n");

    // Create and download file
    const blob = new Blob([csvContent], { type: "text/csv;charset=utf-8;" });
    const link = document.createElement("a");
    const url = URL.createObjectURL(blob);
    link.setAttribute("href", url);
    link.setAttribute("download", `training-action-items-${new Date().toISOString().split('T')[0]}.csv`);
    link.style.visibility = "hidden";
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  const exportToJSON = () => {
    if (actionItems.length === 0) return;

    // Create comprehensive export data
    const exportData = {
      exportDate: new Date().toISOString(),
      fileName: selectedFile?.name || "",
      modelInfo: {
        modelName,
        temperature,
        maxTokens
      },
      metrics: {
        tokensUsed,
        estimatedCost
      },
      actionItems: actionItems.map((item, index) => ({
        ticketNumber: item.ticketNumber || `TRAIN-${index + 1}`,
        title: item.title,
        description: item.description,
        priority: item.priority,
        type: item.type,
        assignedTo: item.assignedTo || "Unassigned"
      }))
    };

    // Create and download file
    const jsonContent = JSON.stringify(exportData, null, 2);
    const blob = new Blob([jsonContent], { type: "application/json;charset=utf-8;" });
    const link = document.createElement("a");
    const url = URL.createObjectURL(blob);
    link.setAttribute("href", url);
    link.setAttribute("download", `training-action-items-${new Date().toISOString().split('T')[0]}.json`);
    link.style.visibility = "hidden";
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
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
                
                {/* Model Information */}
                {modelName && (
                  <div className={styles.modelInfo}>
                    <div className={styles.modelBadge}>
                      <Brain className="h-4 w-4" />
                      <span><strong>Model:</strong> {modelName}</span>
                    </div>
                    <div className={styles.modelParams}>
                      <span>Temperature: {temperature}</span>
                      <span>Max Tokens: {maxTokens.toLocaleString()}</span>
                    </div>
                  </div>
                )}

                {/* Metrics Display */}
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

                {/* Export Buttons */}
                <div className={styles.exportButtons}>
                  <button onClick={exportToCSV} className={styles.exportButton}>
                    <Download className="h-4 w-4" />
                    <span>Export CSV</span>
                  </button>
                  <button onClick={exportToJSON} className={styles.exportButton}>
                    <Download className="h-4 w-4" />
                    <span>Export JSON</span>
                  </button>
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
