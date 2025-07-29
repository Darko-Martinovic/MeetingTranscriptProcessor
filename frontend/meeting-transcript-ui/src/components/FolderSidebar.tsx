import React from "react";
import type { FolderInfo } from "../services/api";
import styles from "./FolderSidebar.module.css";

interface FolderSidebarProps {
  folders: FolderInfo[];
  selectedFolder: FolderInfo | null;
  onFolderSelect: (folder: FolderInfo) => void;
  getFolderIcon: (folderName: string) => React.ReactElement;
  getFolderButtonClass: (isSelected: boolean) => string;
}

const FolderSidebar: React.FC<FolderSidebarProps> = ({
  folders,
  selectedFolder,
  onFolderSelect,
  getFolderIcon,
  getFolderButtonClass,
}) => {
  return (
    <div className={styles.sidebarColumn}>
      <div className={styles.sidebar}>
        <h2 className={styles.sidebarTitle}>Folders</h2>
        <div className={styles.folderList}>
          {folders.map((folder) => (
            <button
              key={folder.name}
              onClick={() => onFolderSelect(folder)}
              className={getFolderButtonClass(
                selectedFolder?.name === folder.name
              )}
            >
              <div className={styles.folderContent}>
                <div className={styles.folderInfo}>
                  {getFolderIcon(folder.name)}
                  <span className={styles.folderName}>{folder.name}</span>
                </div>
                <span className={styles.folderCount}>
                  {folder.meetingCount}
                </span>
              </div>
            </button>
          ))}
        </div>
      </div>
    </div>
  );
};

export default FolderSidebar;
