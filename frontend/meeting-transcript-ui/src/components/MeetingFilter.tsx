import React, { useState, useEffect } from 'react';
import { Search, Filter, X, Calendar, Users, CheckSquare, Languages } from 'lucide-react';
import type { MeetingFilter, MeetingInfo } from '../services/api';

interface MeetingFilterProps {
  onFilterChange: (filter: MeetingFilter) => void;
  meetings: MeetingInfo[];
  isVisible: boolean;
  onToggleVisibility: () => void;
}

const MeetingFilterComponent: React.FC<MeetingFilterProps> = ({
  onFilterChange,
  meetings,
  isVisible,
  onToggleVisibility
}) => {
  const [filter, setFilter] = useState<MeetingFilter>({
    searchText: '',
    status: [],
    language: [],
    participants: [],
    dateFrom: '',
    dateTo: '',
    hasJiraTickets: undefined,
    sortBy: 'date',
    sortOrder: 'desc'
  });

  const [availableFilters, setAvailableFilters] = useState({
    statuses: [] as string[],
    languages: [] as string[],
    participants: [] as string[]
  });

  useEffect(() => {
    // Extract unique values for filter options
    const statuses = [...new Set(meetings.map(m => m.status).filter(s => s && s !== 'unknown'))];
    const languages = [...new Set(meetings.map(m => m.language).filter(l => l && l !== 'unknown'))];
    const participants = [...new Set(meetings.flatMap(m => m.participants || []))];

    setAvailableFilters({
      statuses: statuses.sort(),
      languages: languages.filter(l => l !== undefined).sort(),
      participants: participants.sort()
    });
  }, [meetings]);

  useEffect(() => {
    onFilterChange(filter);
  }, [filter, onFilterChange]);

  const updateFilter = <K extends keyof MeetingFilter>(key: K, value: MeetingFilter[K]) => {
    setFilter(prev => ({ ...prev, [key]: value }));
  };

  const clearFilters = () => {
    setFilter({
      searchText: '',
      status: [],
      language: [],
      participants: [],
      dateFrom: '',
      dateTo: '',
      hasJiraTickets: undefined,
      sortBy: 'date',
      sortOrder: 'desc'
    });
  };

  const toggleArrayFilter = <T extends string>(key: 'status' | 'language' | 'participants', value: T) => {
    const currentArray = filter[key] as T[];
    const newArray = currentArray.includes(value)
      ? currentArray.filter(item => item !== value)
      : [...currentArray, value];
    updateFilter(key, newArray);
  };

  const hasActiveFilters = () => {
    return filter.searchText || 
           (filter.status && filter.status.length > 0) || 
           (filter.language && filter.language.length > 0) ||
           (filter.participants && filter.participants.length > 0) ||
           filter.dateFrom ||
           filter.dateTo ||
           filter.hasJiraTickets !== undefined;
  };

  return (
    <div className="border-b border-gray-200 bg-gray-50">
      {/* Filter Toggle Button */}
      <div className="flex items-center justify-between p-4">
        <button
          onClick={onToggleVisibility}
          className="flex items-center space-x-2 px-4 py-2 bg-blue-500 text-white rounded-lg hover:bg-blue-600 transition-colors"
        >
          <Filter size={16} />
          <span>Filters</span>
          {hasActiveFilters() && (
            <span className="bg-blue-700 text-white rounded-full w-5 h-5 text-xs flex items-center justify-center">
              !
            </span>
          )}
        </button>
        
        {hasActiveFilters() && (
          <button
            onClick={clearFilters}
            className="flex items-center space-x-2 px-3 py-1 text-gray-600 hover:text-gray-800 border border-gray-300 rounded"
          >
            <X size={14} />
            <span>Clear All</span>
          </button>
        )}
      </div>

      {/* Filter Panel */}
      {isVisible && (
        <div className="p-4 space-y-4 border-t border-gray-200">
          {/* Search */}
          <div className="relative">
            <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400" size={16} />
            <input
              type="text"
              placeholder="Search meetings, participants, content..."
              value={filter.searchText}
              onChange={(e) => updateFilter('searchText', e.target.value)}
              className="w-full pl-10 pr-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
            />
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            {/* Status Filter */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">Status</label>
              <div className="space-y-1 max-h-32 overflow-y-auto">
                {availableFilters.statuses.map((status) => (
                  <label key={status} className="flex items-center space-x-2">
                    <input
                      type="checkbox"
                      checked={filter.status?.includes(status) || false}
                      onChange={() => toggleArrayFilter('status', status)}
                      className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                    />
                    <span className="text-sm capitalize">{status}</span>
                  </label>
                ))}
              </div>
            </div>

            {/* Language Filter */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2 flex items-center space-x-1">
                <Languages size={16} />
                <span>Language</span>
              </label>
              <div className="space-y-1 max-h-32 overflow-y-auto">
                {availableFilters.languages.map((language) => (
                  <label key={language} className="flex items-center space-x-2">
                    <input
                      type="checkbox"
                      checked={filter.language?.includes(language) || false}
                      onChange={() => toggleArrayFilter('language', language)}
                      className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                    />
                    <span className="text-sm capitalize">{language}</span>
                  </label>
                ))}
              </div>
            </div>

            {/* Participants Filter */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2 flex items-center space-x-1">
                <Users size={16} />
                <span>Participants</span>
              </label>
              <div className="space-y-1 max-h-32 overflow-y-auto">
                {availableFilters.participants.slice(0, 10).map((participant) => (
                  <label key={participant} className="flex items-center space-x-2">
                    <input
                      type="checkbox"
                      checked={filter.participants?.includes(participant) || false}
                      onChange={() => toggleArrayFilter('participants', participant)}
                      className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                    />
                    <span className="text-sm">{participant}</span>
                  </label>
                ))}
                {availableFilters.participants.length > 10 && (
                  <div className="text-xs text-gray-500">
                    +{availableFilters.participants.length - 10} more participants
                  </div>
                )}
              </div>
            </div>

            {/* Date Range and Special Filters */}
            <div className="space-y-4">
              {/* Date Range */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2 flex items-center space-x-1">
                  <Calendar size={16} />
                  <span>Date Range</span>
                </label>
                <div className="space-y-2">
                  <input
                    type="date"
                    value={filter.dateFrom}
                    onChange={(e) => updateFilter('dateFrom', e.target.value)}
                    className="w-full px-3 py-1 border border-gray-300 rounded text-sm"
                    placeholder="From"
                  />
                  <input
                    type="date"
                    value={filter.dateTo}
                    onChange={(e) => updateFilter('dateTo', e.target.value)}
                    className="w-full px-3 py-1 border border-gray-300 rounded text-sm"
                    placeholder="To"
                  />
                </div>
              </div>

              {/* Jira Tickets Filter */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2 flex items-center space-x-1">
                  <CheckSquare size={16} />
                  <span>Jira Tickets</span>
                </label>
                <select
                  value={filter.hasJiraTickets === undefined ? '' : filter.hasJiraTickets.toString()}
                  onChange={(e) => updateFilter('hasJiraTickets', 
                    e.target.value === '' ? undefined : e.target.value === 'true')}
                  className="w-full px-3 py-1 border border-gray-300 rounded text-sm"
                >
                  <option value="">All</option>
                  <option value="true">Has Jira Tickets</option>
                  <option value="false">No Jira Tickets</option>
                </select>
              </div>
            </div>
          </div>

          {/* Sorting */}
          <div className="flex items-center space-x-4 pt-4 border-t border-gray-200">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Sort By</label>
              <select
                value={filter.sortBy}
                onChange={(e) => updateFilter('sortBy', e.target.value as 'date' | 'title' | 'size' | 'status' | 'language' | 'participants')}
                className="px-3 py-1 border border-gray-300 rounded text-sm"
              >
                <option value="date">Date</option>
                <option value="title">Title</option>
                <option value="size">Size</option>
                <option value="status">Status</option>
                <option value="language">Language</option>
                <option value="participants">Participants Count</option>
              </select>
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Order</label>
              <select
                value={filter.sortOrder}
                onChange={(e) => updateFilter('sortOrder', e.target.value as 'asc' | 'desc')}
                className="px-3 py-1 border border-gray-300 rounded text-sm"
              >
                <option value="desc">Descending</option>
                <option value="asc">Ascending</option>
              </select>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default MeetingFilterComponent;
