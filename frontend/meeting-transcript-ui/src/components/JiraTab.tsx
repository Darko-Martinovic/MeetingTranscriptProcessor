import React from "react";
import type { ConfigurationDto } from "../services/api";
import styles from "./JiraTab.module.css";

interface JiraTabProps {
  config: ConfigurationDto;
}

// Jira's URL, email, token and project are set in the backend's .env, so they're shown here, not edited
const JiraTab: React.FC<JiraTabProps> = React.memo(({ config }) => {
  return (
    <div className={styles.form}>
      <p className={styles.note}>
        {config.environment.isJiraConfigured
          ? "Jira is configured. "
          : "Jira isn't configured: tickets are simulated. "}
        Set JIRA_URL, JIRA_EMAIL, JIRA_API_TOKEN and JIRA_PROJECT_KEY in the
        backend's .env file and restart it to change them.
      </p>
      <div className={styles.formGroup}>
        <label className={styles.label}>Jira URL</label>
        <input
          type="url"
          value={config.environment.jiraUrl ?? ""}
          className={styles.input}
          disabled
        />
      </div>
      <div className={styles.formGroup}>
        <label className={styles.label}>Email</label>
        <input
          type="email"
          value={config.environment.jiraEmail ?? ""}
          className={styles.input}
          disabled
        />
      </div>
      <div className={styles.formGroup}>
        <label className={styles.label}>Default Project</label>
        <input
          type="text"
          value={config.environment.jiraDefaultProject ?? ""}
          className={styles.input}
          disabled
        />
      </div>
    </div>
  );
});

JiraTab.displayName = "JiraTab";

export default JiraTab;
