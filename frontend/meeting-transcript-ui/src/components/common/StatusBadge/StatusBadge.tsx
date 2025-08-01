import React from "react";
import { CheckCircle, XCircle, Clock } from "lucide-react";
import styles from "./StatusBadge.module.css";

interface StatusBadgeProps {
  status: string;
}

const StatusBadge: React.FC<StatusBadgeProps> = React.memo(({ status }) => {
  const getStatusBadgeClasses = (status: string) => {
    switch (status.toLowerCase()) {
      case "success":
        return {
          containerClass: `${styles.statusBadge} ${styles.statusBadgeSuccess}`,
          icon: <CheckCircle className={styles.statusIcon} />,
          text: "Success",
        };
      case "error":
        return {
          containerClass: `${styles.statusBadge} ${styles.statusBadgeError}`,
          icon: <XCircle className={styles.statusIcon} />,
          text: "Error",
        };
      case "processing":
        return {
          containerClass: `${styles.statusBadge} ${styles.statusBadgeProcessing}`,
          icon: (
            <Clock
              className={`${styles.statusIcon} ${styles.statusIconProcessing}`}
            />
          ),
          text: "Processing",
        };
      default:
        return {
          containerClass: `${styles.statusBadge} ${styles.statusBadgeUnknown}`,
          icon: <Clock className={styles.statusIcon} />,
          text: "Unknown",
        };
    }
  };

  const { containerClass, icon, text } = getStatusBadgeClasses(status);

  return (
    <span className={containerClass}>
      {icon}
      {text}
    </span>
  );
});

StatusBadge.displayName = "StatusBadge";

export default StatusBadge;
