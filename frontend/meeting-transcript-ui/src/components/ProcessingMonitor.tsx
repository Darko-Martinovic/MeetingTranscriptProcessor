import React, { useState, useEffect } from 'react';
import type { ProcessingQueue } from '../services/api';
import { ProcessingStage, processingApi } from '../services/api';
import './ProcessingMonitor.css';

interface ProcessingMonitorProps {
  onProcessingComplete?: (fileName: string) => void;
}

const ProcessingMonitor: React.FC<ProcessingMonitorProps> = ({ onProcessingComplete }) => {
  const [processingQueue, setProcessingQueue] = useState<ProcessingQueue | null>(null);
  const [isVisible, setIsVisible] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [autoCloseTimer, setAutoCloseTimer] = useState<NodeJS.Timeout | null>(null);
  const [completedFiles, setCompletedFiles] = useState<Set<string>>(new Set());
  const [autoCloseCountdown, setAutoCloseCountdown] = useState<number>(0);

  useEffect(() => {
    const fetchProcessingQueue = async () => {
      try {
        const queue = await processingApi.getProcessingQueue();
        setProcessingQueue(prevQueue => {
          // Check for newly completed files
          if (prevQueue) {
            const prevCompleted = new Set(prevQueue.recentlyCompleted.map(item => item.fileName));
            const currentCompleted = new Set(queue.recentlyCompleted.map(item => item.fileName));
            
            // Find files that just completed
            const newlyCompleted = [...currentCompleted].filter(fileName => 
              !prevCompleted.has(fileName) && !completedFiles.has(fileName)
            );
            
            // Notify parent of completed files
            newlyCompleted.forEach(fileName => {
              onProcessingComplete?.(fileName);
              setCompletedFiles(prev => new Set([...prev, fileName]));
            });
          }
          
          return queue;
        });
        setError(null);
        
        // Show monitor if there's active processing or recent activity
        const hasActivity = queue.currentlyProcessing.length > 0 || queue.recentlyCompleted.length > 0;
        setIsVisible(hasActivity);
        
        // Auto-close logic: if no active processing and we have completed items, start timer
        if (queue.currentlyProcessing.length === 0 && queue.recentlyCompleted.length > 0) {
          if (!autoCloseTimer) {
            setAutoCloseCountdown(10); // Start countdown at 10 seconds
            const timer = setTimeout(() => {
              setIsVisible(false);
              setAutoCloseTimer(null);
              setAutoCloseCountdown(0);
            }, 10000); // Auto-close after 10 seconds of no activity
            setAutoCloseTimer(timer);
          }
        } else {
          // Clear timer if processing is active
          if (autoCloseTimer) {
            clearTimeout(autoCloseTimer);
            setAutoCloseTimer(null);
            setAutoCloseCountdown(0);
          }
        }
      } catch (err) {
        setError('Failed to load processing status');
        console.error('Error fetching processing queue:', err);
      }
    };

    // Initial fetch
    fetchProcessingQueue();

    // Poll every 1 second for more responsive updates
    const intervalId = setInterval(fetchProcessingQueue, 1000);

    // Countdown timer for auto-close
    const countdownInterval = setInterval(() => {
      setAutoCloseCountdown(prev => {
        if (prev > 1) {
          return prev - 1;
        }
        return 0;
      });
    }, 1000);

    return () => {
      clearInterval(intervalId);
      clearInterval(countdownInterval);
      if (autoCloseTimer) {
        clearTimeout(autoCloseTimer);
      }
    };
  }, [onProcessingComplete, autoCloseTimer, completedFiles]);

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

  const getDetailedStageMessage = (status: { 
    stage: number; 
    statusMessage?: string; 
    metrics?: { 
      jiraTicketsCreated?: number; 
      actionItemsExtracted?: number; 
      detectedLanguage?: string; 
    } 
  }): string => {
    switch (status.stage) {
      case ProcessingStage.CreatingJiraTickets:
        if (status.metrics?.jiraTicketsCreated && status.metrics?.actionItemsExtracted) {
          return `Creating JIRA tickets... (${status.metrics.jiraTicketsCreated}/${status.metrics.actionItemsExtracted} completed)`;
        }
        return 'Creating JIRA tickets...';
      case ProcessingStage.ExtractingActionItems:
        if (status.metrics?.actionItemsExtracted) {
          return `Extracted ${status.metrics.actionItemsExtracted} action items`;
        }
        return 'Analyzing transcript and extracting action items...';
      case ProcessingStage.ReadingFile:
        return 'Reading and parsing transcript file...';
      case ProcessingStage.SavingMetadata:
        return 'Saving meeting metadata and ticket references...';
      case ProcessingStage.Archiving:
        return 'Moving file to archive folder...';
      default:
        return status.statusMessage || getStageDisplayName(status.stage);
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
          {autoCloseCountdown > 0 && (
            <span className="auto-close-countdown" title="Auto-closing in...">
              🕒 {autoCloseCountdown}s
            </span>
          )}
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
            onClick={() => {
              setIsVisible(false);
              if (autoCloseTimer) {
                clearTimeout(autoCloseTimer);
                setAutoCloseTimer(null);
                setAutoCloseCountdown(0);
              }
            }}
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
                  <div className="processing-message">{getDetailedStageMessage(status)}</div>
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
