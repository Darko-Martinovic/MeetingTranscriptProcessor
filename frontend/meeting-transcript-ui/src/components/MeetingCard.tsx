import React, { useState } from 'react';
import { Star, CheckCircle, XCircle, Clock, Edit2, Save, X, Archive, Inbox, Trash2 } from 'lucide-react';
import type { MeetingInfo } from '../services/api';

interface MeetingCardProps {
  meeting: MeetingInfo;
  onSelect: (meeting: MeetingInfo) => void;
  onToggleFavorite: (fileName: string) => void;
  isFavorite: boolean;
  onEditTitle?: (fileName: string, newTitle: string) => void;
  onMoveToArchive?: (fileName: string) => void;
  onMoveToIncoming?: (fileName: string) => void;
  onDelete?: (fileName: string) => void;
  currentFolder?: string;
}

const MeetingCard: React.FC<MeetingCardProps> = ({ 
  meeting, 
  onSelect, 
  onToggleFavorite, 
  isFavorite,
  onEditTitle,
  onMoveToArchive,
  onMoveToIncoming,
  onDelete,
  currentFolder
}) => {
  const [isEditing, setIsEditing] = useState(false);
  const [editedTitle, setEditedTitle] = useState(meeting.title);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);

  const getStatusBadge = (status: string) => {
    switch (status.toLowerCase()) {
      case 'success':
        return (
          <span className="inline-flex items-center px-3 py-1 rounded-full text-sm font-medium bg-green-100 text-green-800 border border-green-200">
            <CheckCircle className="w-4 h-4 mr-1" />
            Success
          </span>
        );
      case 'error':
        return (
          <span className="inline-flex items-center px-3 py-1 rounded-full text-sm font-medium bg-red-100 text-red-800 border border-red-200">
            <XCircle className="w-4 h-4 mr-1" />
            Error
          </span>
        );
      case 'processing':
        return (
          <span className="inline-flex items-center px-3 py-1 rounded-full text-sm font-medium bg-yellow-100 text-yellow-800 border border-yellow-200">
            <Clock className="w-4 h-4 mr-1 animate-pulse" />
            Processing
          </span>
        );
      default:
        return (
          <span className="inline-flex items-center px-3 py-1 rounded-full text-sm font-medium bg-gray-100 text-gray-800 border border-gray-200">
            <Clock className="w-4 h-4 mr-1" />
            Unknown
          </span>
        );
    }
  };

  const extractParticipants = (content: string): string[] => {
    const participantMatch = content.match(/Participants?:\s*([^\n]+)/i);
    if (participantMatch) {
      return participantMatch[1]
        .split(',')
        .map(p => p.trim())
        .filter(p => p.length > 0)
        .slice(0, 5);
    }
    return [];
  };

  const formatFileSize = (bytes: number): string => {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  };

  const formatDate = (dateString: string): string => {
    const date = new Date(dateString);
    return date.toLocaleDateString() + ' ' + date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  };

  const participants = extractParticipants(meeting.previewContent);

  const handleSaveTitle = () => {
    if (onEditTitle && editedTitle.trim() !== meeting.title) {
      onEditTitle(meeting.fileName, editedTitle.trim());
    }
    setIsEditing(false);
  };

  const handleCancelEdit = () => {
    setEditedTitle(meeting.title);
    setIsEditing(false);
  };

  const handleDelete = () => {
    if (onDelete) {
      onDelete(meeting.fileName);
    }
    setShowDeleteConfirm(false);
  };

  const handleMoveToArchive = () => {
    if (onMoveToArchive) {
      onMoveToArchive(meeting.fileName);
    }
  };

  const handleMoveToIncoming = () => {
    if (onMoveToIncoming) {
      onMoveToIncoming(meeting.fileName);
    }
  };

  const isInArchive = currentFolder?.toLowerCase() === 'archive';
  const isInIncoming = currentFolder?.toLowerCase() === 'incoming';

  return (
    <div 
      className="bg-white border border-gray-200 rounded-lg p-6 hover:shadow-lg transition-all duration-200 cursor-pointer hover:border-blue-300"
      onClick={() => onSelect(meeting)}
    >
      {/* Header with title and action buttons */}
      <div className="flex items-start justify-between mb-4">
        <div className="flex-1 mr-3">
          {isEditing ? (
            <div className="flex items-center space-x-2">
              <input
                type="text"
                value={editedTitle}
                onChange={(e) => setEditedTitle(e.target.value)}
                className="flex-1 px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                onClick={(e) => e.stopPropagation()}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') handleSaveTitle();
                  if (e.key === 'Escape') handleCancelEdit();
                }}
                autoFocus
              />
              <button
                onClick={(e) => {
                  e.stopPropagation();
                  handleSaveTitle();
                }}
                className="p-2 text-green-600 hover:bg-green-50 rounded-full transition-colors"
                title="Save title"
              >
                <Save className="h-4 w-4" />
              </button>
              <button
                onClick={(e) => {
                  e.stopPropagation();
                  handleCancelEdit();
                }}
                className="p-2 text-gray-600 hover:bg-gray-50 rounded-full transition-colors"
                title="Cancel editing"
              >
                <X className="h-4 w-4" />
              </button>
            </div>
          ) : (
            <div className="flex items-center space-x-2">
              <h3 className="text-lg font-semibold text-gray-900 truncate leading-tight">
                {meeting.title}
              </h3>
              {onEditTitle && (
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    setIsEditing(true);
                  }}
                  className="p-1 text-gray-400 hover:text-gray-600 hover:bg-gray-50 rounded transition-colors"
                  title="Edit title"
                >
                  <Edit2 className="h-4 w-4" />
                </button>
              )}
            </div>
          )}
        </div>
        
        <div className="flex items-center space-x-1 flex-shrink-0">
          {/* Folder move buttons */}
          {isInArchive && onMoveToIncoming && (
            <button
              onClick={(e) => {
                e.stopPropagation();
                handleMoveToIncoming();
              }}
              className="p-2 text-blue-600 hover:bg-blue-50 rounded-full transition-colors"
              title="Move to Incoming"
            >
              <Inbox className="h-4 w-4" />
            </button>
          )}
          
          {isInIncoming && onMoveToArchive && (
            <button
              onClick={(e) => {
                e.stopPropagation();
                handleMoveToArchive();
              }}
              className="p-2 text-green-600 hover:bg-green-50 rounded-full transition-colors"
              title="Move to Archive"
            >
              <Archive className="h-4 w-4" />
            </button>
          )}
          
          {/* Delete button */}
          {onDelete && (
            <button
              onClick={(e) => {
                e.stopPropagation();
                setShowDeleteConfirm(true);
              }}
              className="p-2 text-red-600 hover:bg-red-50 rounded-full transition-colors"
              title="Delete meeting"
            >
              <Trash2 className="h-4 w-4" />
            </button>
          )}
          
          {/* Favorite button */}
          <button
            onClick={(e) => {
              e.stopPropagation();
              onToggleFavorite(meeting.fileName);
            }}
            className="p-2 hover:bg-gray-100 rounded-full transition-colors"
            title={isFavorite ? "Remove from favorites" : "Add to favorites"}
          >
            <Star 
              className={`h-5 w-5 ${
                isFavorite 
                  ? 'text-yellow-500 fill-current' 
                  : 'text-gray-400 hover:text-yellow-400'
              }`} 
            />
          </button>
        </div>
      </div>

      {/* Participants row */}
      {participants.length > 0 && (
        <div className="mb-4">
          <div className="flex items-center mb-2">
            <span className="text-sm font-medium text-gray-700">Participants:</span>
          </div>
          <div className="flex flex-wrap gap-2">
            {participants.map((participant, index) => (
              <span 
                key={index}
                className="inline-flex items-center px-3 py-1 rounded-full text-sm font-medium bg-blue-50 text-blue-700 border border-blue-200"
              >
                {participant}
              </span>
            ))}
          </div>
        </div>
      )}

      {/* Status and metadata row */}
      <div className="flex items-center justify-between pt-4 border-t border-gray-100">
        <div className="flex items-center space-x-4">
          {getStatusBadge(meeting.status)}
          <span className="text-sm text-gray-500 font-medium">{formatFileSize(meeting.size)}</span>
        </div>
        <div className="text-sm text-gray-500">
          {formatDate(meeting.lastModified)}
        </div>
      </div>

      {/* Delete Confirmation Modal */}
      {showDeleteConfirm && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
          <div className="bg-white rounded-lg p-6 max-w-md mx-4 shadow-xl">
            <div className="flex items-center mb-4">
              <Trash2 className="h-6 w-6 text-red-600 mr-3" />
              <h3 className="text-lg font-semibold text-gray-900">Delete Meeting</h3>
            </div>
            <p className="text-gray-600 mb-6">
              Are you sure you want to delete "{meeting.title}"? This action cannot be undone.
            </p>
            <div className="flex justify-end space-x-3">
              <button
                onClick={(e) => {
                  e.stopPropagation();
                  setShowDeleteConfirm(false);
                }}
                className="px-4 py-2 text-gray-600 border border-gray-300 rounded-md hover:bg-gray-50 transition-colors"
              >
                Cancel
              </button>
              <button
                onClick={(e) => {
                  e.stopPropagation();
                  handleDelete();
                }}
                className="px-4 py-2 bg-red-600 text-white rounded-md hover:bg-red-700 transition-colors"
              >
                Delete
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default MeetingCard;
