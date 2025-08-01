import React, { useState, useCallback } from "react";
import { Edit2, Save, X } from "lucide-react";
import styles from "./EditableTitle.module.css";

interface EditableTitleProps {
  title: string;
  fileName: string;
  onEditTitle?: (fileName: string, newTitle: string) => void;
}

const EditableTitle: React.FC<EditableTitleProps> = React.memo(
  ({ title, fileName, onEditTitle }) => {
    const [isEditing, setIsEditing] = useState(false);
    const [editedTitle, setEditedTitle] = useState(title);

    const handleStartEdit = useCallback((e: React.MouseEvent) => {
      e.stopPropagation();
      setIsEditing(true);
    }, []);

    const handleSaveTitle = useCallback(() => {
      const trimmedTitle = editedTitle.trim();

      // Validate title length
      if (trimmedTitle.length > 200) {
        // Automatically truncate if too long
        const truncatedTitle = trimmedTitle.substring(0, 200);
        setEditedTitle(truncatedTitle);

        if (onEditTitle && truncatedTitle !== title) {
          onEditTitle(fileName, truncatedTitle);
        }
      } else if (onEditTitle && trimmedTitle !== title && trimmedTitle) {
        onEditTitle(fileName, trimmedTitle);
      }

      setIsEditing(false);
    }, [onEditTitle, editedTitle, title, fileName]);

    const handleCancelEdit = useCallback(() => {
      setEditedTitle(title);
      setIsEditing(false);
    }, [title]);

    const handleTitleInputChange = useCallback(
      (e: React.ChangeEvent<HTMLInputElement>) => {
        setEditedTitle(e.target.value);
      },
      []
    );

    const handleTitleKeyDown = useCallback(
      (e: React.KeyboardEvent) => {
        if (e.key === "Enter") {
          handleSaveTitle();
        } else if (e.key === "Escape") {
          handleCancelEdit();
        }
      },
      [handleSaveTitle, handleCancelEdit]
    );

    if (isEditing) {
      return (
        <div className={styles.titleEditContainer}>
          <input
            type="text"
            value={editedTitle}
            onChange={handleTitleInputChange}
            className={styles.titleInput}
            onClick={(e) => e.stopPropagation()}
            onKeyDown={handleTitleKeyDown}
            autoFocus
          />
          <button
            onClick={(e) => {
              e.stopPropagation();
              handleSaveTitle();
            }}
            className={`${styles.iconButton} ${styles.saveButton}`}
            title="Save title"
          >
            <Save className="h-4 w-4" />
          </button>
          <button
            onClick={(e) => {
              e.stopPropagation();
              handleCancelEdit();
            }}
            className={`${styles.iconButton} ${styles.cancelButton}`}
            title="Cancel editing"
          >
            <X className="h-4 w-4" />
          </button>
        </div>
      );
    }

    return (
      <div className={styles.titleDisplay}>
        <h3 className={styles.title}>{title}</h3>
        {onEditTitle && (
          <button
            onClick={handleStartEdit}
            className={`${styles.iconButtonSmall} ${styles.editButton}`}
            title="Edit title"
          >
            <Edit2 className="h-4 w-4" />
          </button>
        )}
      </div>
    );
  }
);

EditableTitle.displayName = "EditableTitle";

export default EditableTitle;
