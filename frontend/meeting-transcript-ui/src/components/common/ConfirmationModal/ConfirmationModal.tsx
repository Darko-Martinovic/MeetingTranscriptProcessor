import React from "react";
import type { LucideIcon } from "lucide-react";
import styles from "./ConfirmationModal.module.css";

interface ConfirmationModalProps {
  isVisible: boolean;
  title: string;
  message: string;
  icon: LucideIcon;
  confirmText: string;
  cancelText?: string;
  onConfirm: (e: React.MouseEvent) => void;
  onCancel: (e: React.MouseEvent) => void;
  variant?: "danger" | "primary";
}

const ConfirmationModal: React.FC<ConfirmationModalProps> = React.memo(
  ({
    isVisible,
    title,
    message,
    icon: Icon,
    confirmText,
    cancelText = "Cancel",
    onConfirm,
    onCancel,
    variant = "primary",
  }) => {
    if (!isVisible) return null;

    return (
      <div className={styles.modalOverlay}>
        <div className={styles.modal}>
          <div className={styles.modalHeader}>
            <Icon className={styles.modalIcon} />
            <h3 className={styles.modalTitle}>{title}</h3>
          </div>
          <p className={styles.modalContent}>{message}</p>
          <div className={styles.modalActions}>
            <button
              onClick={onCancel}
              className={`${styles.modalButton} ${styles.modalButtonSecondary}`}
            >
              {cancelText}
            </button>
            <button
              onClick={onConfirm}
              className={`${styles.modalButton} ${
                variant === "danger"
                  ? styles.modalButtonDanger
                  : styles.modalButtonPrimary
              }`}
            >
              {confirmText}
            </button>
          </div>
        </div>
      </div>
    );
  }
);

ConfirmationModal.displayName = "ConfirmationModal";

export default ConfirmationModal;
