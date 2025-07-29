import React, { useState } from "react";
import { Upload } from "lucide-react";
import styles from "./UploadModal.module.css";

interface UploadModalProps {
  onClose: () => void;
  onUpload: (file: File) => Promise<void>;
  onMultipleUpload: (files: File[]) => Promise<void>;
  loading: boolean;
}

const UploadModal: React.FC<UploadModalProps> = ({
  onClose,
  onUpload,
  onMultipleUpload,
  loading,
}) => {
  const [dragOver, setDragOver] = useState(false);
  const [selectedFiles, setSelectedFiles] = useState<File[]>([]);

  const handleFileSelect = (files: File[] | File) => {
    const allowedTypes = [".txt", ".md", ".json", ".xml", ".docx", ".pdf"];
    const filesToProcess = Array.isArray(files) ? files : [files];

    const validFiles = filesToProcess.filter((file) => {
      const fileExtension = "." + file.name.split(".").pop()?.toLowerCase();
      return allowedTypes.includes(fileExtension);
    });

    if (validFiles.length !== filesToProcess.length) {
      alert(
        "Some files were skipped. Please select only supported file types: " +
          allowedTypes.join(", ")
      );
    }

    if (validFiles.length === 0) {
      return;
    }

    setSelectedFiles(validFiles);
  };

  const handleUpload = async () => {
    if (selectedFiles.length === 0) return;

    if (selectedFiles.length === 1) {
      await onUpload(selectedFiles[0]);
    } else {
      await onMultipleUpload(selectedFiles);
    }
  };

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    setDragOver(false);
    const files = Array.from(e.dataTransfer.files);
    if (files.length > 0) handleFileSelect(files);
  };

  const handleFileInput = (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files ? Array.from(e.target.files) : [];
    if (files.length > 0) handleFileSelect(files);
  };

  const removeFile = (index: number) => {
    setSelectedFiles((files) => files.filter((_, i) => i !== index));
  };

  return (
    <div className={styles.modal}>
      <div className={styles.modalContent}>
        <div className={styles.modalHeader}>
          <h3 className={styles.modalTitle}>Upload Meeting Files</h3>
          <button onClick={onClose} className={styles.modalClose}>
            ×
          </button>
        </div>

        <div
          className={`${styles.uploadArea} ${
            dragOver ? styles.uploadAreaDragOver : ""
          }`}
          onDragOver={(e) => {
            e.preventDefault();
            setDragOver(true);
          }}
          onDragLeave={() => setDragOver(false)}
          onDrop={handleDrop}
        >
          <Upload className={styles.uploadIcon} />
          <p className={styles.uploadText}>
            Drag and drop files here, or click to select
          </p>
          <p className={styles.uploadSubtext}>
            Supported: .txt, .md, .json, .xml, .docx, .pdf (Multiple files
            allowed)
          </p>
          <input
            type="file"
            accept=".txt,.md,.json,.xml,.docx,.pdf"
            onChange={handleFileInput}
            className={styles.uploadInput}
            id="file-input"
            disabled={loading}
            multiple
          />
          <label htmlFor="file-input" className={styles.uploadButton}>
            {loading ? "Uploading..." : "Select Files"}
          </label>
        </div>

        {/* Selected Files List */}
        {selectedFiles.length > 0 && (
          <div className={styles.selectedFiles}>
            <h4 className={styles.selectedFilesTitle}>
              Selected Files ({selectedFiles.length}):
            </h4>
            <div className={styles.filesList}>
              {selectedFiles.map((file, index) => (
                <div key={index} className={styles.fileItem}>
                  <span className={styles.fileName}>{file.name}</span>
                  <button
                    onClick={() => removeFile(index)}
                    className={styles.removeFileButton}
                    disabled={loading}
                  >
                    ×
                  </button>
                </div>
              ))}
            </div>
            <div className={styles.modalButtons}>
              <button
                onClick={handleUpload}
                disabled={loading || selectedFiles.length === 0}
                className={styles.uploadConfirmButton}
              >
                {loading
                  ? "Uploading..."
                  : `Upload ${selectedFiles.length} File${
                      selectedFiles.length > 1 ? "s" : ""
                    }`}
              </button>
              <button
                onClick={() => setSelectedFiles([])}
                disabled={loading}
                className={styles.clearButton}
              >
                Clear All
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};

export default UploadModal;
