import React, { useState, useEffect } from "react";
import { configurationApi } from "../services/api";
import type { ConfigurationDto } from "../services/api";
import PromptsTab from "./PromptsTab";
import AzureOpenAITab from "./AzureOpenAITab";
import ExtractionTab from "./ExtractionTab";
import JiraTab from "./JiraTab";
import styles from "./SettingsModal.module.css";

interface SettingsModalProps {
  onClose: () => void;
}

const SettingsModal: React.FC<SettingsModalProps> = ({ onClose }) => {
  const [activeTab, setActiveTab] = useState("prompts");
  const [loading, setLoading] = useState(false);
  const [config, setConfig] = useState<ConfigurationDto | null>(null);
  const [showTooltip, setShowTooltip] = useState<string | null>(null);

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

  const updatePrompts = async (formData: FormData) => {
    try {
      setLoading(true);
      const customPrompt = formData.get("customPrompt") as string;

      await configurationApi.updatePrompts({
        customPrompt,
      });
      await loadConfiguration();
      alert("Prompts configuration updated successfully!");
    } catch {
      alert("Failed to update prompts configuration");
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

  const toggleTooltip = (tooltipId: string) => {
    setShowTooltip(showTooltip === tooltipId ? null : tooltipId);
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
              onClick={() => setActiveTab("prompts")}
              className={`${styles.tab} ${
                activeTab === "prompts" ? styles.tabActive : ""
              }`}
            >
              Prompts
            </button>
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
          {activeTab === "prompts" && (
            <PromptsTab
              config={config}
              loading={loading}
              onSubmit={updatePrompts}
            />
          )}

          {activeTab === "azure" && (
            <AzureOpenAITab
              config={config}
              loading={loading}
              onSubmit={updateAzureOpenAI}
            />
          )}

          {activeTab === "extraction" && (
            <ExtractionTab
              config={config}
              loading={loading}
              showTooltip={showTooltip}
              onSubmit={updateExtraction}
              onTooltipToggle={toggleTooltip}
            />
          )}

          {activeTab === "jira" && (
            <JiraTab config={config} loading={loading} onSubmit={updateJira} />
          )}
        </div>
      </div>
    </div>
  );
};

export default SettingsModal;
