import React from "react";
import {
  FolderOpen,
  Settings,
  Upload,
  RefreshCw,
  Workflow,
  Shield,
  Layers,
} from "lucide-react";
import type { FolderInfo, SystemStatusDto } from "../services/api";
import styles from "./AppHeader.module.css";

interface AppHeaderProps {
  selectedFolder: FolderInfo | null;
  systemStatus: SystemStatusDto | null;
  loading: boolean;
  onShowUpload: () => void;
  onShowSettings: () => void;
  onShowWorkflow: () => void;
  onShowHallucinationDetector: () => void;
  onShowConsistencyManager: () => void;
  onRefresh: () => void;
  getFolderHeaderIcon: (folderName: string) => React.ReactElement;
}

const AppHeader: React.FC<AppHeaderProps> = ({
  selectedFolder,
  systemStatus,
  loading,
  onShowUpload,
  onShowSettings,
  onShowWorkflow,
  onShowHallucinationDetector,
  onShowConsistencyManager,
  onRefresh,
  getFolderHeaderIcon,
}) => {
  const statusDotClass = `${styles.statusDot} ${
    systemStatus?.isRunning ? styles.statusDotOnline : styles.statusDotOffline
  }`;

  return (
    <header className={styles.header}>
      <div className={styles.headerContainer}>
        <div className={styles.headerContent}>
          <div className={styles.headerLeft}>
            {selectedFolder ? (
              getFolderHeaderIcon(selectedFolder.name)
            ) : (
              <FolderOpen className={styles.appIcon} />
            )}
            <h1 className={styles.appTitle}>Meeting Transcript Processor</h1>
          </div>
          <div className={styles.headerRight}>
            {systemStatus && (
              <div className={styles.statusIndicator}>
                <div className={statusDotClass}></div>
                <span className={styles.statusText}>
                  {systemStatus.isRunning ? "Running" : "Offline"}
                </span>
              </div>
            )}
            <button onClick={onShowUpload} className={styles.uploadButton}>
              <Upload className="h-4 w-4" />
              <span>Upload</span>
            </button>
            <button onClick={onShowWorkflow} className={styles.iconButton}>
              <Workflow className="h-5 w-5" />
            </button>
            <button
              onClick={onShowHallucinationDetector}
              className={styles.iconButton}
              title="AI Validation System"
            >
              <Shield className="h-5 w-5" />
            </button>
            <button
              onClick={onShowConsistencyManager}
              className={styles.iconButton}
              title="Context Manager"
            >
              <Layers className="h-5 w-5" />
            </button>
            <button onClick={onShowSettings} className={styles.iconButton}>
              <Settings className="h-5 w-5" />
            </button>
            <button
              onClick={onRefresh}
              className={`${styles.iconButton} ${
                loading ? styles.iconButtonLoading : ""
              }`}
              disabled={loading}
            >
              <RefreshCw
                className={`h-5 w-5 ${loading ? styles.spinIcon : ""}`}
              />
            </button>
          </div>
        </div>
      </div>
    </header>
  );
};

export default AppHeader;
