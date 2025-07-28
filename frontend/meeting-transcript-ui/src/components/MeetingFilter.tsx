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
    <div className="border-b border-gray-200 bg-white">
      {/* Filter Toggle Button */}
      <div className="flex items-center justify-between p-6 bg-slate-100">
        <button
          onClick={onToggleVisibility}
          className={
            isVisible 
              ? 'bg-red-600 hover:bg-red-700 text-white px-4 py-2 rounded-md flex items-center space-x-2'
              : hasActiveFilters()
                ? 'bg-green-600 hover:bg-green-700 text-white px-4 py-2 rounded-md flex items-center space-x-2'
                : 'bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-md flex items-center space-x-2'
          }
        >
          <Filter className="h-4 w-4" />
          <span>
            {isVisible ? 'Hide Filters' : hasActiveFilters() ? 'Filters Active' : 'Show Filters'}
          </span>
          {hasActiveFilters() && !isVisible && (
            <span className="bg-orange-400 text-white rounded-full w-5 h-5 text-xs flex items-center justify-center font-bold ml-2">
              {/* Count active filters */}
              {(filter.status?.length || 0) + (filter.language?.length || 0) + (filter.participants?.length || 0) + 
               (filter.searchText ? 1 : 0) + (filter.dateFrom ? 1 : 0) + (filter.dateTo ? 1 : 0) + 
               (filter.hasJiraTickets !== undefined ? 1 : 0)}
            </span>
          )}
        </button>
        
        {hasActiveFilters() && (
          <button
            onClick={clearFilters}
            className="bg-red-600 hover:bg-red-700 text-white px-4 py-2 rounded-md flex items-center space-x-2"
          >
            <X className="h-4 w-4" />
            <span>Clear All</span>
          </button>
        )}
      </div>

      {/* Filter Panel */}
      {isVisible && (
        <div className="p-6 space-y-6 border-t border-slate-200 bg-gradient-to-br from-slate-50 to-blue-50">
          {/* Search */}
          <div className="relative">
            <Search className="absolute left-4 top-1/2 transform -translate-y-1/2 text-slate-400" size={20} />
            <input
              type="text"
              placeholder="Search meetings, participants, content..."
              value={filter.searchText}
              onChange={(e) => updateFilter('searchText', e.target.value)}
              className="w-full pl-12 pr-4 py-3 border-2 border-slate-200 rounded-xl focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 transition-all duration-200 bg-white shadow-sm text-slate-700 placeholder-slate-400"
            />
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
            {/* Status Filter */}
            <div className="bg-white rounded-xl p-4 shadow-sm border border-slate-200">
              <label className="block text-sm font-semibold text-slate-700 mb-3 flex items-center space-x-2">
                <div className="w-3 h-3 bg-amber-400 rounded-full"></div>
                <span>Status</span>
              </label>
              <div className="space-y-2 max-h-32 overflow-y-auto">
                {availableFilters.statuses.map((status) => (
                  <label key={status} className="flex items-center space-x-3 p-2 rounded-lg hover:bg-amber-50 transition-colors cursor-pointer">
                    <input
                      type="checkbox"
                      checked={filter.status?.includes(status) || false}
                      onChange={() => toggleArrayFilter('status', status)}
                      className="rounded border-slate-300 text-amber-500 focus:ring-amber-500 focus:ring-2"
                    />
                    <span className="text-sm text-slate-700 capitalize font-medium">{status}</span>
                  </label>
                ))}
              </div>
            </div>

            {/* Language Filter */}
            <div className="bg-white rounded-xl p-4 shadow-sm border border-slate-200">
              <label className="block text-sm font-semibold text-slate-700 mb-3 flex items-center space-x-2">
                <Languages size={16} className="text-purple-500" />
                <span>Language</span>
              </label>
              <div className="space-y-2 max-h-32 overflow-y-auto">
                {availableFilters.languages.map((language) => (
                  <label key={language} className="flex items-center space-x-3 p-2 rounded-lg hover:bg-purple-50 transition-colors cursor-pointer">
                    <input
                      type="checkbox"
                      checked={filter.language?.includes(language) || false}
                      onChange={() => toggleArrayFilter('language', language)}
                      className="rounded border-slate-300 text-purple-500 focus:ring-purple-500 focus:ring-2"
                    />
                    <span className="text-sm text-slate-700 capitalize font-medium">{language}</span>
                  </label>
                ))}
              </div>
            </div>

            {/* Participants Filter */}
            <div className="bg-white rounded-xl p-4 shadow-sm border border-slate-200">
              <label className="block text-sm font-semibold text-slate-700 mb-3 flex items-center space-x-2">
                <Users size={16} className="text-emerald-500" />
                <span>Participants</span>
              </label>
              <div className="space-y-2 max-h-32 overflow-y-auto">
                {availableFilters.participants.slice(0, 10).map((participant) => (
                  <label key={participant} className="flex items-center space-x-3 p-2 rounded-lg hover:bg-emerald-50 transition-colors cursor-pointer">
                    <input
                      type="checkbox"
                      checked={filter.participants?.includes(participant) || false}
                      onChange={() => toggleArrayFilter('participants', participant)}
                      className="rounded border-slate-300 text-emerald-500 focus:ring-emerald-500 focus:ring-2"
                    />
                    <span className="text-sm text-slate-700 font-medium">{participant}</span>
                  </label>
                ))}
                {availableFilters.participants.length > 10 && (
                  <div className="text-xs text-slate-500 italic px-2">
                    +{availableFilters.participants.length - 10} more participants
                  </div>
                )}
              </div>
            </div>

            {/* Date Range and Special Filters */}
            <div className="bg-white rounded-xl p-4 shadow-sm border border-slate-200 space-y-4">
              {/* Date Range */}
              <div>
                <label className="block text-sm font-semibold text-slate-700 mb-3 flex items-center space-x-2">
                  <Calendar size={16} className="text-blue-500" />
                  <span>Date Range</span>
                </label>
                <div className="space-y-3">
                  <div>
                    <label className="block text-xs font-medium text-slate-500 mb-1">From</label>
                    <input
                      type="date"
                      value={filter.dateFrom}
                      onChange={(e) => updateFilter('dateFrom', e.target.value)}
                      className="w-full px-3 py-2 border-2 border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-all duration-200"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-slate-500 mb-1">To</label>
                    <input
                      type="date"
                      value={filter.dateTo}
                      onChange={(e) => updateFilter('dateTo', e.target.value)}
                      className="w-full px-3 py-2 border-2 border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-all duration-200"
                    />
                  </div>
                </div>
              </div>

              {/* Jira Tickets Filter */}
              <div>
                <label className="block text-sm font-semibold text-slate-700 mb-3 flex items-center space-x-2">
                  <CheckSquare size={16} className="text-green-500" />
                  <span>Jira Tickets</span>
                </label>
                <select
                  value={filter.hasJiraTickets === undefined ? '' : filter.hasJiraTickets.toString()}
                  onChange={(e) => updateFilter('hasJiraTickets', 
                    e.target.value === '' ? undefined : e.target.value === 'true')}
                  className="w-full px-3 py-2 border-2 border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-green-500 focus:border-green-500 transition-all duration-200 bg-white"
                >
                  <option value="">All Meetings</option>
                  <option value="true">✅ Has Jira Tickets</option>
                  <option value="false">❌ No Jira Tickets</option>
                </select>
              </div>
            </div>
          </div>

          {/* Sorting */}
          <div className="flex items-center justify-center space-x-6 pt-4 border-t-2 border-slate-200 bg-white rounded-xl p-4 shadow-sm">
            <div className="flex items-center space-x-3">
              <label className="block text-sm font-semibold text-slate-700">🔄 Sort By</label>
              <select
                value={filter.sortBy}
                onChange={(e) => updateFilter('sortBy', e.target.value as 'date' | 'title' | 'size' | 'status' | 'language' | 'participants')}
                className="px-4 py-2 border-2 border-slate-200 rounded-lg text-sm font-medium focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 transition-all duration-200 bg-white"
              >
                <option value="date">📅 Date</option>
                <option value="title">📝 Title</option>
                <option value="size">📏 Size</option>
                <option value="status">⚡ Status</option>
                <option value="language">🌐 Language</option>
                <option value="participants">👥 Participants Count</option>
              </select>
            </div>
            <div className="flex items-center space-x-3">
              <label className="block text-sm font-semibold text-slate-700">📊 Order</label>
              <select
                value={filter.sortOrder}
                onChange={(e) => updateFilter('sortOrder', e.target.value as 'asc' | 'desc')}
                className="px-4 py-2 border-2 border-slate-200 rounded-lg text-sm font-medium focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 transition-all duration-200 bg-white"
              >
                <option value="desc">⬇️ Descending</option>
                <option value="asc">⬆️ Ascending</option>
              </select>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default MeetingFilterComponent;
