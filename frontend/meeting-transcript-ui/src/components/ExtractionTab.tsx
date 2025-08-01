import React from "react";
import type { ConfigurationDto } from "../services/api";
import InfoTooltip from "./InfoTooltip";
import styles from "./ExtractionTab.module.css";

interface ExtractionTabProps {
  config: ConfigurationDto;
  loading: boolean;
  showTooltip: string | null;
  onSubmit: (formData: FormData) => Promise<void>;
  onTooltipToggle: (tooltipId: string) => void;
}

const getTooltipContent = (type: string): string => {
  switch (type) {
    case "validation":
      return "Cross-validates AI-extracted action items with rule-based extraction to detect potential false positives and false negatives. Provides confidence scoring for extracted items and tracks validation metrics over time.";
    case "hallucination":
      return "Analyzes extracted action items for AI hallucinations by validating context snippets exist in the original transcript, checking assignee names against meeting participants, and filtering out items with low confidence scores.";
    case "consistency":
      return "Automatically detects meeting type (standup, sprint, architecture, etc.) and adapts extraction prompts based on meeting context. Supports multi-language transcript processing and optimizes AI parameters for different meeting types.";
    default:
      return "";
  }
};

const ExtractionTab: React.FC<ExtractionTabProps> = React.memo(
  ({ config, loading, showTooltip, onSubmit, onTooltipToggle }) => {
    const handleSubmit = (e: React.FormEvent<HTMLFormElement>) => {
      e.preventDefault();
      onSubmit(new FormData(e.currentTarget));
    };

    return (
      <form onSubmit={handleSubmit} className={styles.form}>
        <div className={styles.formGroup}>
          <label className={styles.label}>Max Concurrent Files</label>
          <input
            type="number"
            name="maxConcurrentFiles"
            min="1"
            max="10"
            defaultValue={config.extraction.maxConcurrentFiles}
            className={styles.input}
          />
        </div>
        <div className={styles.formGroup}>
          <label className={styles.label}>
            Validation Confidence Threshold
          </label>
          <input
            type="number"
            name="validationConfidenceThreshold"
            min="0"
            max="1"
            step="0.1"
            defaultValue={config.extraction.validationConfidenceThreshold}
            className={styles.input}
          />
        </div>
        <div className={styles.checkboxGroup}>
          <div className={styles.checkboxItem}>
            <label className={styles.checkboxLabel}>
              <input
                type="checkbox"
                name="enableValidation"
                defaultChecked={config.extraction.enableValidation}
                className={styles.checkbox}
              />
              Enable Validation
            </label>
            <InfoTooltip
              id="validation"
              content={getTooltipContent("validation")}
              isVisible={showTooltip === "validation"}
              onToggle={() => onTooltipToggle("validation")}
            />
          </div>
          <div className={styles.checkboxItem}>
            <label className={styles.checkboxLabel}>
              <input
                type="checkbox"
                name="enableHallucinationDetection"
                defaultChecked={config.extraction.enableHallucinationDetection}
                className={styles.checkbox}
              />
              Enable Hallucination Detection
            </label>
            <InfoTooltip
              id="hallucination"
              content={getTooltipContent("hallucination")}
              isVisible={showTooltip === "hallucination"}
              onToggle={() => onTooltipToggle("hallucination")}
            />
          </div>
          <div className={styles.checkboxItem}>
            <label className={styles.checkboxLabel}>
              <input
                type="checkbox"
                name="enableConsistencyManagement"
                defaultChecked={config.extraction.enableConsistencyManagement}
                className={styles.checkbox}
              />
              Enable Consistency Management
            </label>
            <InfoTooltip
              id="consistency"
              content={getTooltipContent("consistency")}
              isVisible={showTooltip === "consistency"}
              onToggle={() => onTooltipToggle("consistency")}
            />
          </div>
        </div>
        <button
          type="submit"
          disabled={loading}
          className={styles.submitButton}
        >
          {loading ? "Updating..." : "Update Extraction Settings"}
        </button>
      </form>
    );
  }
);

ExtractionTab.displayName = "ExtractionTab";

export default ExtractionTab;
