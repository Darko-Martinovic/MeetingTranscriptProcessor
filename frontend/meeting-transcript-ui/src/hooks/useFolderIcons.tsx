import { useCallback } from "react";
import {
  Archive,
  Inbox,
  Clock3,
  Clock,
  Folder,
  FolderOpen,
} from "lucide-react";
import styles from "../App.module.css";

export const useFolderIcons = () => {
  const getFolderIcon = useCallback((folderName: string) => {
    switch (folderName.toLowerCase()) {
      case "archive":
        return (
          <Archive
            className={`${styles.folderIcon} ${styles.folderIconArchive}`}
          />
        );
      case "incoming":
        return (
          <Inbox
            className={`${styles.folderIcon} ${styles.folderIconIncoming}`}
          />
        );
      case "processing":
        return (
          <Clock3
            className={`${styles.folderIcon} ${styles.folderIconProcessing}`}
          />
        );
      case "recent":
        return (
          <Clock
            className={`${styles.folderIcon} ${styles.folderIconOutgoing}`}
          />
        );
      default:
        return (
          <Folder
            className={`${styles.folderIcon} ${styles.folderIconGeneral}`}
          />
        );
    }
  }, []);

  const getFolderHeaderIcon = useCallback((folderName: string) => {
    switch (folderName.toLowerCase()) {
      case "archive":
        return (
          <Archive
            className={`${styles.appIcon} ${styles.folderIconArchive}`}
          />
        );
      case "incoming":
        return (
          <Inbox className={`${styles.appIcon} ${styles.folderIconIncoming}`} />
        );
      case "processing":
        return (
          <Clock3
            className={`${styles.appIcon} ${styles.folderIconProcessing}`}
          />
        );
      case "recent":
        return (
          <Clock className={`${styles.appIcon} ${styles.folderIconOutgoing}`} />
        );
      default:
        return (
          <FolderOpen
            className={`${styles.appIcon} ${styles.folderIconIncoming}`}
          />
        );
    }
  }, []);

  const getFolderButtonClass = useCallback((isSelected: boolean) => {
    return `${styles.folderButton} ${
      isSelected ? styles.folderButtonActive : styles.folderButtonInactive
    }`;
  }, []);

  return {
    getFolderIcon,
    getFolderHeaderIcon,
    getFolderButtonClass,
  };
};
