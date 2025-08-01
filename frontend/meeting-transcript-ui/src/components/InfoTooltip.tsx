import React from "react";
import { Info } from "lucide-react";
import styles from "./InfoTooltip.module.css";

interface InfoTooltipProps {
  id: string;
  content: string;
  isVisible: boolean;
  onToggle: () => void;
}

const InfoTooltip: React.FC<InfoTooltipProps> = React.memo(
  ({ id, content, isVisible, onToggle }) => {
    return (
      <div className={styles.container}>
        <button
          type="button"
          onClick={onToggle}
          className={styles.infoButton}
          aria-label={`Information about ${id}`}
        >
          <Info className="h-4 w-4" />
        </button>
        {isVisible && <div className={styles.tooltip}>{content}</div>}
      </div>
    );
  }
);

InfoTooltip.displayName = "InfoTooltip";

export default InfoTooltip;
