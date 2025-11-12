import React from "react";
import type { ConfigurationDto } from "../services/api";
import styles from "./AzureOpenAITab.module.css";

interface AzureOpenAITabProps {
  config: ConfigurationDto;
  loading: boolean;
  onSubmit: (formData: FormData) => Promise<void>;
}

const AzureOpenAITab: React.FC<AzureOpenAITabProps> = React.memo(
  ({ config, loading, onSubmit }) => {
    const handleSubmit = (e: React.FormEvent<HTMLFormElement>) => {
      e.preventDefault();
      onSubmit(new FormData(e.currentTarget));
    };

    return (
      <form onSubmit={handleSubmit} className={styles.form}>
        <div className={styles.formGroup}>
          <label className={styles.label}>Endpoint</label>
          <input
            type="url"
            name="endpoint"
            defaultValue={config.azureOpenAI.endpoint}
            className={styles.input}
            placeholder="https://your-resource.openai.azure.com/"
          />
        </div>
        <div className={styles.formGroup}>
          <label className={styles.label}>API Key</label>
          <input
            type="password"
            name="apiKey"
            className={styles.input}
            placeholder="Enter your API key"
          />
        </div>
        <div className={styles.formGroup}>
          <label className={styles.label}>Deployment Name</label>
          <input
            type="text"
            name="deploymentName"
            defaultValue={config.azureOpenAI.deploymentName}
            className={styles.input}
            placeholder="gpt-4"
          />
        </div>
        
        <div className={styles.formGroup}>
          <label className={styles.label}>
            Temperature (0.0 - 1.0)
            <span className={styles.helpText}>Lower = more focused, Higher = more creative</span>
          </label>
          <input
            type="number"
            name="temperature"
            defaultValue={0.1}
            min="0"
            max="1"
            step="0.1"
            className={styles.input}
            placeholder="0.1"
          />
        </div>
        
        <div className={styles.formGroup}>
          <label className={styles.label}>Max Tokens</label>
          <input
            type="number"
            name="maxTokens"
            defaultValue={4000}
            min="100"
            max="8000"
            step="100"
            className={styles.input}
            placeholder="4000"
          />
        </div>
        
        <div className={styles.formGroup}>
          <label className={styles.label}>
            Top P (0.0 - 1.0)
            <span className={styles.helpText}>Nucleus sampling threshold</span>
          </label>
          <input
            type="number"
            name="topP"
            defaultValue={0.95}
            min="0"
            max="1"
            step="0.05"
            className={styles.input}
            placeholder="0.95"
          />
        </div>
        
        <button
          type="submit"
          disabled={loading}
          className={styles.submitButton}
        >
          {loading ? "Updating..." : "Update Azure OpenAI Settings"}
        </button>
      </form>
    );
  }
);

AzureOpenAITab.displayName = "AzureOpenAITab";

export default AzureOpenAITab;
