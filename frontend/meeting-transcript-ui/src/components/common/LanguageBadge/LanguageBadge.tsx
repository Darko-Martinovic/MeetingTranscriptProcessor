import React from "react";
import { Globe } from "lucide-react";
import styles from "./LanguageBadge.module.css";

interface LanguageBadgeProps {
  language?: string;
  size?: "small" | "medium";
}

const LanguageBadge: React.FC<LanguageBadgeProps> = ({
  language,
  size = "small",
}) => {
  if (!language) return null;

  // Map language codes to display names and colors
  const getLanguageInfo = (lang: string) => {
    const normalized = lang.toLowerCase();

    switch (normalized) {
      case "en":
      case "english":
        return { display: "EN", name: "English", colorClass: styles.english };
      case "fr":
      case "french":
      case "français":
        return { display: "FR", name: "French", colorClass: styles.french };
      case "nl":
      case "dutch":
      case "nederlands":
        return { display: "NL", name: "Dutch", colorClass: styles.dutch };
      case "de":
      case "german":
      case "deutsch":
        return { display: "DE", name: "German", colorClass: styles.german };
      case "es":
      case "spanish":
      case "español":
        return { display: "ES", name: "Spanish", colorClass: styles.spanish };
      case "it":
      case "italian":
      case "italiano":
        return { display: "IT", name: "Italian", colorClass: styles.italian };
      default:
        return {
          display: lang.substring(0, 2).toUpperCase(),
          name: lang,
          colorClass: styles.default,
        };
    }
  };

  const { display, name, colorClass } = getLanguageInfo(language);
  const sizeClass = size === "medium" ? styles.medium : styles.small;

  return (
    <div
      className={`${styles.badge} ${colorClass} ${sizeClass}`}
      title={`Language: ${name}`}
      role="img"
      aria-label={`Language: ${name}`}
    >
      <Globe className={styles.icon} />
      <span className={styles.text}>{display}</span>
    </div>
  );
};

export default LanguageBadge;
