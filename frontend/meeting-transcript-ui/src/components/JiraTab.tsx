import React from "react";
import type { ConfigurationDto } from "../services/api";
import styles from "./JiraTab.module.css";

interface JiraTabProps {
  config: ConfigurationDto;
  loading: boolean;
  onSubmit: (formData: FormData) => Promise<void>;
}

const JiraTab: React.FC<JiraTabProps> = React.memo(
  ({ config, loading, onSubmit }) => {
    const handleSubmit = (e: React.FormEvent<HTMLFormElement>) => {
      e.preventDefault();
      onSubmit(new FormData(e.currentTarget));
    };

    return (
      <form onSubmit={handleSubmit} className={styles.form}>
        <div className={styles.formGroup}>
          <label className={styles.label}>Jira URL</label>
          <input
            type="url"
            name="url"
            defaultValue={config.environment.jiraUrl}
            className={styles.input}
            placeholder="https://your-domain.atlassian.net"
          />
        </div>
        <div className={styles.formGroup}>
          <label className={styles.label}>Email</label>
          <input
            type="email"
            name="email"
            defaultValue={config.environment.jiraEmail}
            className={styles.input}
            placeholder="your-email@example.com"
          />
        </div>
        <div className={styles.formGroup}>
          <label className={styles.label}>API Token</label>
          <input
            type="password"
            name="apiToken"
            className={styles.input}
            placeholder="Enter your Jira API token"
          />
        </div>
        <div className={styles.formGroup}>
          <label className={styles.label}>Default Project</label>
          <input
            type="text"
            name="defaultProject"
            defaultValue={config.environment.jiraDefaultProject}
            className={styles.input}
            placeholder="TASK"
          />
        </div>
        <button
          type="submit"
          disabled={loading}
          className={styles.submitButton}
        >
          {loading ? "Updating..." : "Update Jira Settings"}
        </button>
      </form>
    );
  }
);

JiraTab.displayName = "JiraTab";

export default JiraTab;
