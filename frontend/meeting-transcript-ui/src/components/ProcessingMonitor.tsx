import React, { useState, useEffect } from 'react';
import type { ProcessingQueue } from '../services/api';
import { ProcessingStage, processingApi } from '../services/api';
import './ProcessingMonitor.css';

const ProcessingMonitor: React.FC = () => {
  const [processingQueue, setProcessingQueue] = useState<ProcessingQueue | null>(null);
  const [isVisible, setIsVisible] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchProcessingQueue = async () => {
      try {
        const queue = await processingApi.getProcessingQueue();
        setProcessingQueue(queue);
        setError(null);
        
        // Show monitor if there's active processing or recent activity
        const hasActivity = queue.currentlyProcessing.length > 0 || queue.recentlyCompleted.length > 0;
        setIsVisible(hasActivity);
      } catch (err) {
        setError('Failed to load processing status');
        console.error('Error fetching processing queue:', err);
      }
    };

    // Initial fetch
    fetchProcessingQueue();

    // Poll every 2 seconds for updates
    const intervalId = setInterval(fetchProcessingQueue, 2000);

    return () => {
      clearInterval(intervalId);
    };
  }, []);

  const getStageDisplayName = (stage: number): string => {
    switch (stage) {
      case ProcessingStage.Queued: return 'Queued';
      case ProcessingStage.Starting: return 'Starting';
      case ProcessingStage.ReadingFile: return 'Reading File';
      case ProcessingStage.ExtractingActionItems: return 'Extracting Actions';
      case ProcessingStage.CreatingJiraTickets: return 'Creating JIRA Tickets';
      case ProcessingStage.SavingMetadata: return 'Saving Metadata';
      case ProcessingStage.Archiving: return 'Archiving';
      case ProcessingStage.Completed: return 'Completed';
      case ProcessingStage.Failed: return 'Failed';
      default: return 'Unknown';
    }
  };

  const getStageColor = (stage: number): string => {
    switch (stage) {
      case ProcessingStage.Queued: return '#fbbf24'; // amber
      case ProcessingStage.Starting: return '#60a5fa'; // blue
      case ProcessingStage.ReadingFile: return '#34d399'; // emerald
      case ProcessingStage.ExtractingActionItems: return '#a78bfa'; // violet
      case ProcessingStage.CreatingJiraTickets: return '#fb7185'; // rose
      case ProcessingStage.SavingMetadata: return '#fbbf24'; // amber
      case ProcessingStage.Archiving: return '#94a3b8'; // slate
      case ProcessingStage.Completed: return '#10b981'; // emerald
      case ProcessingStage.Failed: return '#ef4444'; // red
      default: return '#6b7280'; // gray
    }
  };

  const formatDuration = (startTime: string, endTime?: string): string => {
    const start = new Date(startTime);
    const end = endTime ? new Date(endTime) : new Date();
    const diffMs = end.getTime() - start.getTime();
    const diffSecs = Math.floor(diffMs / 1000);
    
    if (diffSecs < 60) return `${diffSecs}s`;
    const diffMins = Math.floor(diffSecs / 60);
    const remainingSecs = diffSecs % 60;
    return `${diffMins}m ${remainingSecs}s`;
  };

  const handleClearCompleted = async () => {
    try {
      await processingApi.clearCompleted();
      // Refresh the queue
      const queue = await processingApi.getProcessingQueue();
      setProcessingQueue(queue);
    } catch (err) {
      setError('Failed to clear completed history');
      console.error('Error clearing completed:', err);
    }
  };

  if (!isVisible && !error) {
    return null;
  }

  return (
    <div className="processing-monitor">
      <div className="processing-header">
        <h3>
          <span className="processing-icon">⚡</span>
          Processing Activity
        </h3>
        <div className="processing-controls">
          {processingQueue && processingQueue.recentlyCompleted.length > 0 && (
            <button 
              onClick={handleClearCompleted}
              className="clear-button"
              title="Clear completed history"
            >
              🧹 Clear
            </button>
          )}
          <button 
            onClick={() => setIsVisible(false)}
            className="close-button"
            title="Hide processing monitor"
          >
            ✕
          </button>
        </div>
      </div>

      {error && (
        <div className="processing-error">
          <span className="error-icon">⚠️</span>
          {error}
        </div>
      )}

      {processingQueue && (
        <div className="processing-content">
          {/* Currently Processing */}
          {processingQueue.currentlyProcessing.length > 0 && (
            <div className="processing-section">
              <h4>Currently Processing ({processingQueue.currentlyProcessing.length})</h4>
              {processingQueue.currentlyProcessing.map((status) => (
                <div key={status.id} className="processing-item active">
                  <div className="processing-item-header">
                    <span className="processing-file-name">{status.fileName}</span>
                    <span className="processing-id">ID: {status.id}</span>
                  </div>
                  <div className="processing-item-details">
                    <div className="processing-stage">
                      <span 
                        className="stage-indicator"
                        style={{ backgroundColor: getStageColor(status.stage) }}
                      />
                      {getStageDisplayName(status.stage)}
                    </div>
                    <div className="processing-progress">
                      <div className="progress-bar">
                        <div 
                          className="progress-fill"
                          style={{ width: `${status.progressPercentage}%` }}
                        />
                      </div>
                      <span className="progress-text">{status.progressPercentage}%</span>
                    </div>
                    <div className="processing-duration">
                      {formatDuration(status.startedAt)}
                    </div>
                  </div>
                  <div className="processing-message">{status.statusMessage}</div>
                  {status.hasError && status.errorMessage && (
                    <div className="processing-error-message">
                      <span className="error-icon">❌</span>
                      {status.errorMessage}
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}

          {/* Recently Completed */}
          {processingQueue.recentlyCompleted.length > 0 && (
            <div className="processing-section">
              <h4>Recently Completed ({processingQueue.recentlyCompleted.length})</h4>
              {processingQueue.recentlyCompleted.map((status) => (
                <div key={status.id} className={`processing-item completed ${status.hasError ? 'error' : 'success'}`}>
                  <div className="processing-item-header">
                    <span className="processing-file-name">{status.fileName}</span>
                    <span className="processing-id">ID: {status.id}</span>
                  </div>
                  <div className="processing-item-details">
                    <div className="processing-stage">
                      <span 
                        className="stage-indicator"
                        style={{ backgroundColor: getStageColor(status.stage) }}
                      />
                      {getStageDisplayName(status.stage)}
                    </div>
                    <div className="processing-metrics">
                      {status.metrics && (
                        <>
                          <span className="metric">📋 {status.metrics.actionItemsExtracted} actions</span>
                          <span className="metric">🎫 {status.metrics.jiraTicketsCreated} tickets</span>
                          {status.metrics.detectedLanguage && (
                            <span className="metric">🌐 {status.metrics.detectedLanguage}</span>
                          )}
                        </>
                      )}
                    </div>
                    <div className="processing-duration">
                      {status.completedAt && formatDuration(status.startedAt, status.completedAt)}
                    </div>
                  </div>
                  {status.hasError && status.errorMessage && (
                    <div className="processing-error-message">
                      <span className="error-icon">❌</span>
                      {status.errorMessage}
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}

          {processingQueue.currentlyProcessing.length === 0 && processingQueue.recentlyCompleted.length === 0 && (
            <div className="processing-empty">
              <span className="empty-icon">💤</span>
              No recent processing activity
            </div>
          )}
        </div>
      )}
    </div>
  );
};

export default ProcessingMonitor;
