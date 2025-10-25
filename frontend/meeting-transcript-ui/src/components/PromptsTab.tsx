import React, { useState, useEffect } from "react";
import type { ConfigurationDto } from "../services/api";
import styles from "./PromptsTab.module.css";

interface PromptsTabProps {
  config: ConfigurationDto;
  loading: boolean;
  onSubmit: (formData: FormData) => Promise<void>;
}

const PromptsTab: React.FC<PromptsTabProps> = ({
  config,
  loading,
  onSubmit,
}) => {
  const [customPrompt, setCustomPrompt] = useState("");

  useEffect(() => {
    // Load custom prompt from config if available
    if (config.azureOpenAI?.customPrompt) {
      setCustomPrompt(config.azureOpenAI.customPrompt);
    }
  }, [config]);

  const handleSubmit = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const formData = new FormData(e.currentTarget);
    await onSubmit(formData);
  };

  return (
    <form onSubmit={handleSubmit} className={styles.form}>
      <div className={styles.section}>
        <h4 className={styles.sectionTitle}>System Prompt (Read-Only)</h4>
        <p className={styles.description}>
          This is the base system prompt used for all AI interactions. It cannot
          be modified directly.
        </p>
        <div className={styles.formGroup}>
          <textarea
            value={config.azureOpenAI?.systemPrompt || ""}
            readOnly
            className={`${styles.textarea} ${styles.readOnly}`}
            rows={6}
          />
        </div>
      </div>

      <div className={styles.section}>
        <h4 className={styles.sectionTitle}>Custom Training Prompt</h4>
        <p className={styles.description}>
          Add custom instructions to fine-tune AI behavior based on your
          feedback and requirements. This will be merged with the system prompt.
        </p>
        <div className={styles.formGroup}>
          <textarea
            name="customPrompt"
            value={customPrompt}
            onChange={(e) => setCustomPrompt(e.target.value)}
            className={styles.textarea}
            rows={8}
            placeholder="Enter custom prompt to guide AI behavior..."
          />
        </div>

        <div className={styles.exampleSection}>
          <h5 className={styles.exampleTitle}>Example Custom Prompts:</h5>
          <ul className={styles.exampleList}>
            <li>
              <strong>Be more conservative:</strong> "Only extract clear,
              explicit action items with specific assignees. Avoid interpreting
              vague statements as action items."
            </li>
            <li>
              <strong>Focus on priorities:</strong> "Pay special attention to
              urgency indicators like 'ASAP', 'urgent', 'critical'. Assign
              higher priorities accordingly."
            </li>
            <li>
              <strong>Improve context:</strong> "Always include surrounding
              context in action item descriptions to make them
              self-explanatory."
            </li>
          </ul>
        </div>
      </div>

      <div className={styles.formActions}>
        <button
          type="submit"
          disabled={loading}
          className={styles.submitButton}
        >
          {loading ? "Saving..." : "Save Prompts Configuration"}
        </button>
      </div>
    </form>
  );
};

export default PromptsTab;
