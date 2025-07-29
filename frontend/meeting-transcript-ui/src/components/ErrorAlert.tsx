import React from "react";
import styles from "./ErrorAlert.module.css";

interface ErrorAlertProps {
  error: string | null;
  onClose: () => void;
}

const ErrorAlert: React.FC<ErrorAlertProps> = ({ error, onClose }) => {
  if (!error) return null;

  return (
    <div className={styles.errorAlert}>
      {error}
      <button onClick={onClose} className={styles.errorCloseButton}>
        ×
      </button>
    </div>
  );
};

export default ErrorAlert;
