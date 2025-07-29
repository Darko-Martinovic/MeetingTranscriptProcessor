import React, { useState, useEffect } from "react";
import { configurationApi } from "../services/api";
import type { ConfigurationDto } from "../services/api";
import styles from "./SettingsModal.module.css";

interface SettingsModalProps {
  onClose: () => void;
}

const SettingsModal: React.FC<SettingsModalProps> = ({ onClose }) => {
  const [activeTab, setActiveTab] = useState("azure");
  const [loading, setLoading] = useState(false);
  const [config, setConfig] = useState<ConfigurationDto | null>(null);

  useEffect(() => {
    loadConfiguration();
  }, []);

  const loadConfiguration = async () => {
    try {
      const configData = await configurationApi.getConfiguration();
      setConfig(configData);
    } catch (err) {
      console.error("Failed to load configuration:", err);
    }
  };

  const updateAzureOpenAI = async (formData: FormData) => {
    try {
      setLoading(true);
      const endpoint = formData.get("endpoint") as string;
      const apiKey = formData.get("apiKey") as string;
      const deploymentName = formData.get("deploymentName") as string;

      await configurationApi.updateAzureOpenAI({
        endpoint,
        apiKey,
        deploymentName,
      });
      await loadConfiguration();
      alert("Azure OpenAI configuration updated successfully!");
    } catch {
      alert("Failed to update Azure OpenAI configuration");
    } finally {
      setLoading(false);
    }
  };

  const updateExtraction = async (formData: FormData) => {
    try {
      setLoading(true);
      const maxConcurrentFiles = parseInt(
        formData.get("maxConcurrentFiles") as string
      );
      const validationConfidenceThreshold = parseFloat(
        formData.get("validationConfidenceThreshold") as string
      );
      const enableValidation = formData.get("enableValidation") === "on";
      const enableHallucinationDetection =
        formData.get("enableHallucinationDetection") === "on";
      const enableConsistencyManagement =
        formData.get("enableConsistencyManagement") === "on";

      await configurationApi.updateExtraction({
        maxConcurrentFiles,
        validationConfidenceThreshold,
        enableValidation,
        enableHallucinationDetection,
        enableConsistencyManagement,
      });
      await loadConfiguration();
      alert("Extraction configuration updated successfully!");
    } catch {
      alert("Failed to update extraction configuration");
    } finally {
      setLoading(false);
    }
  };

  const updateJira = async (formData: FormData) => {
    try {
      setLoading(true);
      const url = formData.get("url") as string;
      const email = formData.get("email") as string;
      const apiToken = formData.get("apiToken") as string;
      const defaultProject = formData.get("defaultProject") as string;

      await configurationApi.updateJira({
        url,
        email,
        apiToken,
        defaultProject,
      });
      await loadConfiguration();
      alert("Jira configuration updated successfully!");
    } catch {
      alert("Failed to update Jira configuration");
    } finally {
      setLoading(false);
    }
  };

  if (!config) return null;

  return (
    <div className={styles.modal}>
      <div className={styles.modalContent}>
        <div className={styles.modalHeader}>
          <div className={styles.headerContent}>
            <h3 className={styles.modalTitle}>Configuration Settings</h3>
            <button onClick={onClose} className={styles.modalClose}>
              ×
            </button>
          </div>

          <div className={styles.tabs}>
            <button
              onClick={() => setActiveTab("azure")}
              className={`${styles.tab} ${
                activeTab === "azure" ? styles.tabActive : ""
              }`}
            >
              Azure OpenAI
            </button>
            <button
              onClick={() => setActiveTab("extraction")}
              className={`${styles.tab} ${
                activeTab === "extraction" ? styles.tabActive : ""
              }`}
            >
              Extraction
            </button>
            <button
              onClick={() => setActiveTab("jira")}
              className={`${styles.tab} ${
                activeTab === "jira" ? styles.tabActive : ""
              }`}
            >
              Jira
            </button>
          </div>
        </div>

        <div className={styles.modalBody}>
          {activeTab === "azure" && (
            <form
              onSubmit={(e) => {
                e.preventDefault();
                updateAzureOpenAI(new FormData(e.currentTarget));
              }}
              className={styles.form}
            >
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
          )}

          {activeTab === "extraction" && (
            <form
              onSubmit={(e) => {
                e.preventDefault();
                updateExtraction(new FormData(e.currentTarget));
              }}
              className={styles.form}
            >
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
                <label className={styles.checkboxLabel}>
                  <input
                    type="checkbox"
                    name="enableValidation"
                    defaultChecked={config.extraction.enableValidation}
                    className={styles.checkbox}
                  />
                  Enable Validation
                </label>
                <label className={styles.checkboxLabel}>
                  <input
                    type="checkbox"
                    name="enableHallucinationDetection"
                    defaultChecked={
                      config.extraction.enableHallucinationDetection
                    }
                    className={styles.checkbox}
                  />
                  Enable Hallucination Detection
                </label>
                <label className={styles.checkboxLabel}>
                  <input
                    type="checkbox"
                    name="enableConsistencyManagement"
                    defaultChecked={
                      config.extraction.enableConsistencyManagement
                    }
                    className={styles.checkbox}
                  />
                  Enable Consistency Management
                </label>
              </div>
              <button
                type="submit"
                disabled={loading}
                className={styles.submitButton}
              >
                {loading ? "Updating..." : "Update Extraction Settings"}
              </button>
            </form>
          )}

          {activeTab === "jira" && (
            <form
              onSubmit={(e) => {
                e.preventDefault();
                updateJira(new FormData(e.currentTarget));
              }}
              className={styles.form}
            >
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
          )}
        </div>
      </div>
    </div>
  );
};

export default SettingsModal;
