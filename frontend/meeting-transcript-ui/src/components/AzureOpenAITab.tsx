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
