import React, { useState, useEffect, useCallback } from 'react';
import { 
  FolderOpen, 
  File, 
  Settings, 
  Upload, 
  RefreshCw, 
  Star, 
  Clock,
  Archive,
  Inbox,
  Clock3,
  Folder
} from 'lucide-react';
import { 
  meetingApi, 
  configurationApi, 
  localStorageService, 
  FolderType,
  type FolderInfo, 
  type MeetingInfo, 
  type MeetingTranscript, 
  type SystemStatusDto,
  type ConfigurationDto,
  type MeetingFilter
} from './services/api';
import MeetingFilterComponent from './components/MeetingFilter';
import './App.css';

const App: React.FC = () => {
  const [folders, setFolders] = useState<FolderInfo[]>([]);
  const [selectedFolder, setSelectedFolder] = useState<FolderInfo | null>(null);
  const [meetings, setMeetings] = useState<MeetingInfo[]>([]);
  const [selectedMeeting, setSelectedMeeting] = useState<MeetingTranscript | null>(null);
  const [systemStatus, setSystemStatus] = useState<SystemStatusDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [showSettings, setShowSettings] = useState(false);
  const [showUpload, setShowUpload] = useState(false);
  const [favorites, setFavorites] = useState<string[]>([]);
  const [recentMeetings, setRecentMeetings] = useState<string[]>([]);
  const [currentFilter, setCurrentFilter] = useState<MeetingFilter>({});
  const [showFilters, setShowFilters] = useState(false);

  const loadSystemStatus = useCallback(async () => {
    try {
      const status = await configurationApi.getSystemStatus();
      setSystemStatus(status);
    } catch (err) {
      console.error('Failed to load system status:', err);
    }
  }, []);

  const loadMeetingsInFolder = useCallback(async (folder: FolderInfo, filter?: MeetingFilter) => {
    try {
      setLoading(true);
      const meetingsData = await meetingApi.getMeetingsInFolder(folder.type, filter);
      setMeetings(meetingsData);
      setSelectedFolder(folder);
      setSelectedMeeting(null);
    } catch (err) {
      setError('Failed to load meetings');
      console.error(err);
    } finally {
      setLoading(false);
    }
  }, []);

  const handleFilterChange = useCallback((filter: MeetingFilter) => {
    setCurrentFilter(filter);
    if (selectedFolder) {
      loadMeetingsInFolder(selectedFolder, filter);
    }
  }, [selectedFolder, loadMeetingsInFolder]);

  const handleFolderSelect = useCallback(async (folder: FolderInfo) => {
    // Reset filters when switching folders
    const defaultFilter: MeetingFilter = {
      sortBy: 'date',
      sortOrder: 'desc'
    };
    setCurrentFilter(defaultFilter);
    setShowFilters(false);
    await loadMeetingsInFolder(folder, defaultFilter);
  }, [loadMeetingsInFolder]);

  useEffect(() => {
    const loadFolders = async () => {
      try {
        setLoading(true);
        const foldersData = await meetingApi.getFolders();
        setFolders(foldersData);
      } catch (err) {
        setError('Failed to load folders');
        console.error(err);
      } finally {
        setLoading(false);
      }
    };

    loadFolders();
    loadSystemStatus();
    setFavorites(localStorageService.getFavorites());
    setRecentMeetings(localStorageService.getRecentMeetings());

    // Auto refresh every 30 seconds
    const interval = setInterval(() => {
      if (selectedFolder) {
        loadMeetingsInFolder(selectedFolder, currentFilter);
      }
      loadSystemStatus();
    }, 30000);

    return () => clearInterval(interval);
  }, [selectedFolder, loadMeetingsInFolder, loadSystemStatus, currentFilter]);

  const loadFolders = useCallback(async () => {
    try {
      setLoading(true);
      const foldersData = await meetingApi.getFolders();
      setFolders(foldersData);
    } catch (err) {
      setError('Failed to load folders');
      console.error(err);
    } finally {
      setLoading(false);
    }
  }, []);

  const loadMeeting = async (meetingInfo: MeetingInfo) => {
    try {
      setLoading(true);
      const meeting = await meetingApi.getMeeting(meetingInfo.fileName);
      setSelectedMeeting(meeting);
      localStorageService.addRecentMeeting(meetingInfo.fileName);
      setRecentMeetings(localStorageService.getRecentMeetings());
    } catch (err) {
      setError('Failed to load meeting details');
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  const handleFileUpload = async (file: File) => {
    try {
      setLoading(true);
      await meetingApi.uploadMeeting(file);
      setShowUpload(false);
      // Refresh the incoming folder if it's selected
      const incomingFolder = folders.find(f => f.type === FolderType.Incoming);
      if (incomingFolder && selectedFolder?.type === FolderType.Incoming) {
        await loadMeetingsInFolder(incomingFolder);
      }
    } catch (err) {
      setError('Failed to upload file');
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  const handleMultipleFileUpload = async (files: File[]) => {
    try {
      setLoading(true);
      const uploadPromises = files.map(file => meetingApi.uploadMeeting(file));
      await Promise.all(uploadPromises);
      setShowUpload(false);
      // Refresh the incoming folder if it's selected
      const incomingFolder = folders.find(f => f.type === FolderType.Incoming);
      if (incomingFolder && selectedFolder?.type === FolderType.Incoming) {
        await loadMeetingsInFolder(incomingFolder);
      }
    } catch (err) {
      setError('Failed to upload one or more files');
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  const toggleFavorite = (fileName: string) => {
    const isFavorite = favorites.includes(fileName);
    if (isFavorite) {
      localStorageService.removeFavorite(fileName);
    } else {
      localStorageService.addFavorite(fileName);
    }
    setFavorites(localStorageService.getFavorites());
  };

  const formatFileSize = (bytes: number): string => {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  };

  const formatDate = (dateString: string): string => {
    return new Date(dateString).toLocaleString();
  };

  const getStatusColor = (status: string): string => {
    switch (status.toLowerCase()) {
      case 'success': return 'text-green-600';
      case 'error': return 'text-red-600';
      case 'processing': return 'text-yellow-600';
      default: return 'text-gray-600';
    }
  };

  const getFolderIcon = (folderName: string) => {
    const iconClass = "h-4 w-4 mr-2";
    switch (folderName.toLowerCase()) {
      case 'archive':
        return <Archive className={`${iconClass} text-emerald-600`} />;
      case 'incoming':
        return <Inbox className={`${iconClass} text-blue-600`} />;
      case 'processing':
        return <Clock3 className={`${iconClass} text-amber-600`} />;
      case 'recent':
        return <Clock className={`${iconClass} text-purple-600`} />;
      default:
        return <Folder className={`${iconClass} text-gray-600`} />;
    }
  };

  const getFolderHeaderIcon = (folderName: string) => {
    const iconClass = "h-8 w-8";
    switch (folderName.toLowerCase()) {
      case 'archive':
        return <Archive className={`${iconClass} text-emerald-600`} />;
      case 'incoming':
        return <Inbox className={`${iconClass} text-blue-600`} />;
      case 'processing':
        return <Clock3 className={`${iconClass} text-amber-600`} />;
      case 'recent':
        return <Clock className={`${iconClass} text-purple-600`} />;
      default:
        return <FolderOpen className={`${iconClass} text-blue-600`} />;
    }
  };

  const getFolderColor = (folderName: string): string => {
    switch (folderName.toLowerCase()) {
      case 'archive':
        return 'bg-emerald-50 text-emerald-700 border-emerald-200';
      case 'incoming':
        return 'bg-blue-50 text-blue-700 border-blue-200';
      case 'processing':
        return 'bg-amber-50 text-amber-700 border-amber-200';
      case 'recent':
        return 'bg-purple-50 text-purple-700 border-purple-200';
      default:
        return 'bg-gray-50 text-gray-700 border-gray-200';
    }
  };

  return (
    <div className="min-h-screen bg-gray-50">
      {/* Header */}
      <header className="bg-white shadow-sm border-b">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex justify-between items-center h-16">
            <div className="flex items-center">
              {selectedFolder ? getFolderHeaderIcon(selectedFolder.name) : <FolderOpen className="h-8 w-8 text-blue-600" />}
              <h1 className="ml-3 text-xl font-semibold text-gray-900">
                Meeting Transcript Processor
              </h1>
            </div>
            <div className="flex items-center space-x-4">
              {systemStatus && (
                <div className="flex items-center space-x-2 text-sm">
                  <div className={`h-2 w-2 rounded-full ${systemStatus.isRunning ? 'bg-green-500' : 'bg-red-500'}`}></div>
                  <span className="text-gray-600">
                    {systemStatus.isRunning ? 'Running' : 'Offline'}
                  </span>
                </div>
              )}
              <button
                onClick={() => setShowUpload(true)}
                className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-md flex items-center space-x-2"
              >
                <Upload className="h-4 w-4" />
                <span>Upload</span>
              </button>
              <button
                onClick={() => setShowSettings(true)}
                className="p-2 text-gray-400 hover:text-gray-600 rounded-md"
              >
                <Settings className="h-5 w-5" />
              </button>
              <button
                onClick={loadFolders}
                className="p-2 text-gray-400 hover:text-gray-600 rounded-md"
                disabled={loading}
              >
                <RefreshCw className={`h-5 w-5 ${loading ? 'animate-spin' : ''}`} />
              </button>
            </div>
          </div>
        </div>
      </header>

      {/* Main Content */}
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        {error && (
          <div className="mb-4 bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-md">
            {error}
            <button 
              onClick={() => setError(null)}
              className="float-right text-red-500 hover:text-red-700"
            >
              ×
            </button>
          </div>
        )}

        <div className="grid grid-cols-1 lg:grid-cols-4 gap-6">
          {/* Sidebar - Folders */}
          <div className="lg:col-span-1">
            <div className="bg-white rounded-lg shadow p-6">
              <h2 className="text-lg font-medium text-gray-900 mb-4">Folders</h2>
              <div className="space-y-2">
                {folders.map((folder) => (
                  <button
                    key={folder.name}
                    onClick={() => handleFolderSelect(folder)}
                    className={`w-full text-left p-3 rounded-md transition-colors border ${
                      selectedFolder?.name === folder.name
                        ? getFolderColor(folder.name)
                        : 'hover:bg-gray-50 text-gray-700 border-gray-200'
                    }`}
                  >
                    <div className="flex items-center justify-between">
                      <div className="flex items-center">
                        {getFolderIcon(folder.name)}
                        <span className="font-medium">{folder.name}</span>
                      </div>
                      <span className="text-sm text-gray-500">
                        {folder.meetingCount}
                      </span>
                    </div>
                  </button>
                ))}
              </div>

              {/* Recent Meetings */}
              {recentMeetings.length > 0 && (
                <div className="mt-6">
                  <h3 className="text-md font-semibold text-gray-900 mb-3 flex items-center">
                    <Clock className="h-4 w-4 mr-2 text-purple-600" />
                    <span className="text-purple-700">Recent</span>
                  </h3>
                  <div className="space-y-1">
                    {recentMeetings.slice(0, 5).map((fileName) => (
                      <div 
                        key={fileName}
                        className="text-sm text-gray-600 p-3 hover:bg-purple-50 hover:text-purple-700 rounded-lg cursor-pointer truncate transition-colors border border-transparent hover:border-purple-200"
                        title={fileName}
                      >
                        <div className="flex items-center">
                          <File className="h-3 w-3 mr-2 text-purple-500" />
                          {fileName}
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>
          </div>

          {/* Main Content Area */}
          <div className="lg:col-span-3">
            {!selectedFolder ? (
              <div className="bg-white rounded-lg shadow p-8 text-center">
                <FolderOpen className="h-16 w-16 text-gray-300 mx-auto mb-4" />
                <h3 className="text-lg font-medium text-gray-900 mb-2">
                  Select a folder to view meetings
                </h3>
                <p className="text-gray-500">
                  Choose a folder from the sidebar to see all processed meetings.
                </p>
              </div>
            ) : !selectedMeeting ? (
              <div className="bg-white rounded-lg shadow">
                <div className="p-6 border-b border-gray-200">
                  <h2 className="text-lg font-medium text-gray-900">
                    {selectedFolder.name} ({meetings.length} meetings)
                  </h2>
                </div>
                
                {/* Show filter component only for Archive folder */}
                {selectedFolder.type === FolderType.Archive && (
                  <MeetingFilterComponent
                    onFilterChange={handleFilterChange}
                    meetings={meetings}
                    isVisible={showFilters}
                    onToggleVisibility={() => setShowFilters(!showFilters)}
                  />
                )}
                
                <div className="divide-y divide-gray-200">
                  {meetings.length === 0 ? (
                    <div className="p-8 text-center">
                      <File className="h-16 w-16 text-gray-300 mx-auto mb-4" />
                      <h3 className="text-lg font-medium text-gray-900 mb-2">
                        No meetings found
                      </h3>
                      <p className="text-gray-500">
                        This folder doesn't contain any meeting files yet.
                      </p>
                    </div>
                  ) : (
                    meetings.map((meeting) => (
                      <div
                        key={meeting.fileName}
                        className="p-4 hover:bg-gray-50 cursor-pointer"
                        onClick={() => loadMeeting(meeting)}
                      >
                        <div className="flex items-center justify-between">
                          <div className="flex-1 min-w-0">
                            <div className="flex items-center">
                              <File className="h-4 w-4 text-gray-400 mr-2 flex-shrink-0" />
                              <p className="text-sm font-medium text-gray-900 truncate">
                                {meeting.title}
                              </p>
                              <button
                                onClick={(e) => {
                                  e.stopPropagation();
                                  toggleFavorite(meeting.fileName);
                                }}
                                className="ml-2 p-1 hover:bg-gray-200 rounded"
                              >
                                <Star 
                                  className={`h-4 w-4 ${
                                    favorites.includes(meeting.fileName) 
                                      ? 'text-yellow-500 fill-current' 
                                      : 'text-gray-400'
                                  }`} 
                                />
                              </button>
                            </div>
                            <p className="text-sm text-gray-500 truncate mt-1">
                              {meeting.previewContent}
                            </p>
                            <div className="flex items-center mt-2 text-xs text-gray-400 space-x-4">
                              <span>{formatFileSize(meeting.size)}</span>
                              <span>{formatDate(meeting.lastModified)}</span>
                              <span className={getStatusColor(meeting.status)}>
                                {meeting.status}
                              </span>
                            </div>
                          </div>
                        </div>
                      </div>
                    ))
                  )}
                </div>
              </div>
            ) : (
              <MeetingDetails 
                meeting={selectedMeeting} 
                onBack={() => setSelectedMeeting(null)}
                onToggleFavorite={() => toggleFavorite(selectedMeeting.fileName)}
                isFavorite={favorites.includes(selectedMeeting.fileName)}
              />
            )}
          </div>
        </div>
      </div>

      {/* Upload Modal */}
      {showUpload && (
        <UploadModal 
          onClose={() => setShowUpload(false)}
          onUpload={handleFileUpload}
          onMultipleUpload={handleMultipleFileUpload}
          loading={loading}
        />
      )}

      {/* Settings Modal */}
      {showSettings && (
        <SettingsModal 
          onClose={() => setShowSettings(false)}
        />
      )}
    </div>
  );
};

// Meeting Details Component
const MeetingDetails: React.FC<{
  meeting: MeetingTranscript;
  onBack: () => void;
  onToggleFavorite: () => void;
  isFavorite: boolean;
}> = ({ meeting, onBack, onToggleFavorite, isFavorite }) => {
  const formatDate = (dateString: string): string => {
    return new Date(dateString).toLocaleString();
  };

  return (
    <div className="bg-white rounded-lg shadow">
      <div className="p-6 border-b border-gray-200">
        <div className="flex items-center justify-between">
          <div className="flex items-center">
            <button
              onClick={onBack}
              className="mr-4 p-2 hover:bg-gray-100 rounded-md"
            >
              ←
            </button>
            <h2 className="text-lg font-medium text-gray-900">{meeting.title}</h2>
          </div>
          <button
            onClick={onToggleFavorite}
            className="p-2 hover:bg-gray-100 rounded-md"
          >
            <Star 
              className={`h-5 w-5 ${
                isFavorite ? 'text-yellow-500 fill-current' : 'text-gray-400'
              }`} 
            />
          </button>
        </div>
        <div className="mt-2 text-sm text-gray-500 space-x-4">
          <span>Meeting Date: {formatDate(meeting.meetingDate)}</span>
          <span>Processed: {formatDate(meeting.processedAt)}</span>
          <span>Language: {meeting.detectedLanguage}</span>
        </div>
      </div>

      <div className="p-6 space-y-6">
        {/* Participants */}
        {meeting.participants.length > 0 && (
          <div>
            <h3 className="text-md font-medium text-gray-900 mb-2">Participants</h3>
            <div className="flex flex-wrap gap-2">
              {meeting.participants.map((participant, index) => (
                <span 
                  key={index}
                  className="px-2 py-1 bg-blue-100 text-blue-800 text-sm rounded-md"
                >
                  {participant}
                </span>
              ))}
            </div>
          </div>
        )}

        {/* Action Items */}
        {meeting.actionItems.length > 0 && (
          <div>
            <h3 className="text-md font-medium text-gray-900 mb-3">
              Action Items ({meeting.actionItems.length})
            </h3>
            <div className="space-y-3">
              {meeting.actionItems.map((item) => (
                <div key={item.id} className="border border-gray-200 rounded-md p-4">
                  <div className="flex items-start justify-between">
                    <div className="flex-1">
                      <p className="text-sm text-gray-900">{item.description}</p>
                      <div className="mt-2 text-xs text-gray-500 space-x-4">
                        {item.assignee && <span>Assignee: {item.assignee}</span>}
                        {item.dueDate && <span>Due: {formatDate(item.dueDate)}</span>}
                        <span>Priority: {item.priority}</span>
                        <span>Status: {item.status}</span>
                      </div>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Content */}
        <div>
          <h3 className="text-md font-medium text-gray-900 mb-3">Meeting Content</h3>
          <div className="bg-gray-50 rounded-md p-4 max-h-96 overflow-y-auto">
            <pre className="text-sm text-gray-700 whitespace-pre-wrap">
              {meeting.content}
            </pre>
          </div>
        </div>
      </div>
    </div>
  );
};

// Upload Modal Component
const UploadModal: React.FC<{
  onClose: () => void;
  onUpload: (file: File) => Promise<void>;
  onMultipleUpload: (files: File[]) => Promise<void>;
  loading: boolean;
}> = ({ onClose, onUpload, onMultipleUpload, loading }) => {
  const [dragOver, setDragOver] = useState(false);
  const [selectedFiles, setSelectedFiles] = useState<File[]>([]);

  const handleFileSelect = (files: File[] | File) => {
    const allowedTypes = ['.txt', '.md', '.json', '.xml', '.docx', '.pdf'];
    const filesToProcess = Array.isArray(files) ? files : [files];
    
    const validFiles = filesToProcess.filter(file => {
      const fileExtension = '.' + file.name.split('.').pop()?.toLowerCase();
      return allowedTypes.includes(fileExtension);
    });

    if (validFiles.length !== filesToProcess.length) {
      alert('Some files were skipped. Please select only supported file types: ' + allowedTypes.join(', '));
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
    setSelectedFiles(files => files.filter((_, i) => i !== index));
  };

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
      <div className="bg-white rounded-lg p-6 w-full max-w-lg">
        <div className="flex justify-between items-center mb-4">
          <h3 className="text-lg font-medium">Upload Meeting Files</h3>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600"
          >
            ×
          </button>
        </div>

        <div
          className={`border-2 border-dashed rounded-lg p-8 text-center transition-colors ${
            dragOver ? 'border-blue-400 bg-blue-50' : 'border-gray-300'
          }`}
          onDragOver={(e) => { e.preventDefault(); setDragOver(true); }}
          onDragLeave={() => setDragOver(false)}
          onDrop={handleDrop}
        >
          <Upload className="h-12 w-12 text-gray-400 mx-auto mb-4" />
          <p className="text-gray-600 mb-2">
            Drag and drop files here, or click to select
          </p>
          <p className="text-sm text-gray-500 mb-4">
            Supported: .txt, .md, .json, .xml, .docx, .pdf (Multiple files allowed)
          </p>
          <input
            type="file"
            accept=".txt,.md,.json,.xml,.docx,.pdf"
            onChange={handleFileInput}
            className="hidden"
            id="file-input"
            disabled={loading}
            multiple
          />
          <label
            htmlFor="file-input"
            className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-md cursor-pointer inline-block"
          >
            {loading ? 'Uploading...' : 'Select Files'}
          </label>
        </div>

        {/* Selected Files List */}
        {selectedFiles.length > 0 && (
          <div className="mt-4">
            <h4 className="text-sm font-medium text-gray-700 mb-2">
              Selected Files ({selectedFiles.length}):
            </h4>
            <div className="max-h-40 overflow-y-auto space-y-2">
              {selectedFiles.map((file, index) => (
                <div key={index} className="flex items-center justify-between bg-gray-50 p-2 rounded">
                  <span className="text-sm text-gray-700 truncate">{file.name}</span>
                  <button
                    onClick={() => removeFile(index)}
                    className="text-red-500 hover:text-red-700 ml-2"
                    disabled={loading}
                  >
                    ×
                  </button>
                </div>
              ))}
            </div>
            <div className="mt-4 flex space-x-2">
              <button
                onClick={handleUpload}
                disabled={loading || selectedFiles.length === 0}
                className="bg-green-600 hover:bg-green-700 disabled:bg-gray-400 text-white px-4 py-2 rounded-md"
              >
                {loading ? 'Uploading...' : `Upload ${selectedFiles.length} File${selectedFiles.length > 1 ? 's' : ''}`}
              </button>
              <button
                onClick={() => setSelectedFiles([])}
                disabled={loading}
                className="bg-gray-500 hover:bg-gray-600 disabled:bg-gray-400 text-white px-4 py-2 rounded-md"
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

// Settings Modal Component
const SettingsModal: React.FC<{
  onClose: () => void;
}> = ({ onClose }) => {
  const [activeTab, setActiveTab] = useState('azure');
  const [loading, setLoading] = useState(false);
  const [config, setConfig] = useState<ConfigurationDto | null>(null);

  useEffect(() => {
    loadConfiguration();
  }, []);

  const loadConfiguration = async () => {
    try {
      const configData = await configurationApi.getConfiguration();
      setConfig(configData);
    } catch (err) {
      console.error('Failed to load configuration:', err);
    }
  };

  const updateAzureOpenAI = async (formData: FormData) => {
    try {
      setLoading(true);
      const endpoint = formData.get('endpoint') as string;
      const apiKey = formData.get('apiKey') as string;
      const deploymentName = formData.get('deploymentName') as string;
      
      await configurationApi.updateAzureOpenAI({ endpoint, apiKey, deploymentName });
      await loadConfiguration();
      alert('Azure OpenAI configuration updated successfully!');
    } catch {
      alert('Failed to update Azure OpenAI configuration');
    } finally {
      setLoading(false);
    }
  };

  const updateExtraction = async (formData: FormData) => {
    try {
      setLoading(true);
      const maxConcurrentFiles = parseInt(formData.get('maxConcurrentFiles') as string);
      const validationConfidenceThreshold = parseFloat(formData.get('validationConfidenceThreshold') as string);
      const enableValidation = formData.get('enableValidation') === 'on';
      const enableHallucinationDetection = formData.get('enableHallucinationDetection') === 'on';
      const enableConsistencyManagement = formData.get('enableConsistencyManagement') === 'on';
      
      await configurationApi.updateExtraction({
        maxConcurrentFiles,
        validationConfidenceThreshold,
        enableValidation,
        enableHallucinationDetection,
        enableConsistencyManagement
      });
      await loadConfiguration();
      alert('Extraction configuration updated successfully!');
    } catch {
      alert('Failed to update extraction configuration');
    } finally {
      setLoading(false);
    }
  };

  const updateJira = async (formData: FormData) => {
    try {
      setLoading(true);
      const url = formData.get('url') as string;
      const email = formData.get('email') as string;
      const apiToken = formData.get('apiToken') as string;
      const defaultProject = formData.get('defaultProject') as string;
      
      await configurationApi.updateJira({ url, email, apiToken, defaultProject });
      await loadConfiguration();
      alert('Jira configuration updated successfully!');
    } catch {
      alert('Failed to update Jira configuration');
    } finally {
      setLoading(false);
    }
  };

  if (!config) return null;

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
      <div className="bg-white rounded-lg w-full max-w-2xl max-h-screen overflow-y-auto">
        <div className="p-6 border-b border-gray-200">
          <div className="flex justify-between items-center">
            <h3 className="text-lg font-medium">Configuration Settings</h3>
            <button
              onClick={onClose}
              className="text-gray-400 hover:text-gray-600"
            >
              ×
            </button>
          </div>

          <div className="mt-4 flex space-x-4">
            <button
              onClick={() => setActiveTab('azure')}
              className={`px-4 py-2 rounded-md ${
                activeTab === 'azure' ? 'bg-blue-100 text-blue-700' : 'text-gray-600 hover:text-gray-800'
              }`}
            >
              Azure OpenAI
            </button>
            <button
              onClick={() => setActiveTab('extraction')}
              className={`px-4 py-2 rounded-md ${
                activeTab === 'extraction' ? 'bg-blue-100 text-blue-700' : 'text-gray-600 hover:text-gray-800'
              }`}
            >
              Extraction
            </button>
            <button
              onClick={() => setActiveTab('jira')}
              className={`px-4 py-2 rounded-md ${
                activeTab === 'jira' ? 'bg-blue-100 text-blue-700' : 'text-gray-600 hover:text-gray-800'
              }`}
            >
              Jira
            </button>
          </div>
        </div>

        <div className="p-6">
          {activeTab === 'azure' && (
            <form onSubmit={(e) => { e.preventDefault(); updateAzureOpenAI(new FormData(e.currentTarget)); }}>
              <div className="space-y-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Endpoint
                  </label>
                  <input
                    type="url"
                    name="endpoint"
                    defaultValue={config.azureOpenAI.endpoint}
                    className="w-full border border-gray-300 rounded-md px-3 py-2"
                    placeholder="https://your-resource.openai.azure.com/"
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    API Key
                  </label>
                  <input
                    type="password"
                    name="apiKey"
                    className="w-full border border-gray-300 rounded-md px-3 py-2"
                    placeholder="Enter your API key"
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Deployment Name
                  </label>
                  <input
                    type="text"
                    name="deploymentName"
                    defaultValue={config.azureOpenAI.deploymentName}
                    className="w-full border border-gray-300 rounded-md px-3 py-2"
                    placeholder="gpt-4"
                  />
                </div>
                <button
                  type="submit"
                  disabled={loading}
                  className="w-full bg-blue-600 hover:bg-blue-700 text-white py-2 rounded-md disabled:opacity-50"
                >
                  {loading ? 'Updating...' : 'Update Azure OpenAI Settings'}
                </button>
              </div>
            </form>
          )}

          {activeTab === 'extraction' && (
            <form onSubmit={(e) => { e.preventDefault(); updateExtraction(new FormData(e.currentTarget)); }}>
              <div className="space-y-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Max Concurrent Files
                  </label>
                  <input
                    type="number"
                    name="maxConcurrentFiles"
                    min="1"
                    max="10"
                    defaultValue={config.extraction.maxConcurrentFiles}
                    className="w-full border border-gray-300 rounded-md px-3 py-2"
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Validation Confidence Threshold
                  </label>
                  <input
                    type="number"
                    name="validationConfidenceThreshold"
                    min="0"
                    max="1"
                    step="0.1"
                    defaultValue={config.extraction.validationConfidenceThreshold}
                    className="w-full border border-gray-300 rounded-md px-3 py-2"
                  />
                </div>
                <div className="space-y-2">
                  <label className="flex items-center">
                    <input
                      type="checkbox"
                      name="enableValidation"
                      defaultChecked={config.extraction.enableValidation}
                      className="mr-2"
                    />
                    Enable Validation
                  </label>
                  <label className="flex items-center">
                    <input
                      type="checkbox"
                      name="enableHallucinationDetection"
                      defaultChecked={config.extraction.enableHallucinationDetection}
                      className="mr-2"
                    />
                    Enable Hallucination Detection
                  </label>
                  <label className="flex items-center">
                    <input
                      type="checkbox"
                      name="enableConsistencyManagement"
                      defaultChecked={config.extraction.enableConsistencyManagement}
                      className="mr-2"
                    />
                    Enable Consistency Management
                  </label>
                </div>
                <button
                  type="submit"
                  disabled={loading}
                  className="w-full bg-blue-600 hover:bg-blue-700 text-white py-2 rounded-md disabled:opacity-50"
                >
                  {loading ? 'Updating...' : 'Update Extraction Settings'}
                </button>
              </div>
            </form>
          )}

          {activeTab === 'jira' && (
            <form onSubmit={(e) => { e.preventDefault(); updateJira(new FormData(e.currentTarget)); }}>
              <div className="space-y-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Jira URL
                  </label>
                  <input
                    type="url"
                    name="url"
                    defaultValue={config.environment.jiraUrl}
                    className="w-full border border-gray-300 rounded-md px-3 py-2"
                    placeholder="https://your-domain.atlassian.net"
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Email
                  </label>
                  <input
                    type="email"
                    name="email"
                    defaultValue={config.environment.jiraEmail}
                    className="w-full border border-gray-300 rounded-md px-3 py-2"
                    placeholder="your-email@example.com"
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    API Token
                  </label>
                  <input
                    type="password"
                    name="apiToken"
                    className="w-full border border-gray-300 rounded-md px-3 py-2"
                    placeholder="Enter your Jira API token"
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Default Project
                  </label>
                  <input
                    type="text"
                    name="defaultProject"
                    defaultValue={config.environment.jiraDefaultProject}
                    className="w-full border border-gray-300 rounded-md px-3 py-2"
                    placeholder="TASK"
                  />
                </div>
                <button
                  type="submit"
                  disabled={loading}
                  className="w-full bg-blue-600 hover:bg-blue-700 text-white py-2 rounded-md disabled:opacity-50"
                >
                  {loading ? 'Updating...' : 'Update Jira Settings'}
                </button>
              </div>
            </form>
          )}
        </div>
      </div>
    </div>
  );
};

export default App;
