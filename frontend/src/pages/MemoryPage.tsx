import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api } from '../services/api';
import { MemoryType, MemoryItem } from '../types';
import { Database, Search, Plus, Trash2, Tag, Star, HelpCircle } from 'lucide-react';

export const MemoryPage: React.FC = () => {
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedType, setSelectedType] = useState<string>('');
  const [isCreating, setIsCreating] = useState(false);
  const [newContent, setNewContent] = useState('');
  const [newType, setNewType] = useState<MemoryType>('LongTerm');
  const [newImportance, setNewImportance] = useState(3.5);
  const [newTags, setNewTags] = useState('config, preference');

  const { data: memories, isLoading, refetch } = useQuery({
    queryKey: ['memories', selectedType],
    queryFn: () => api.getMemories(selectedType || undefined),
  });

  const handleSearch = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!searchQuery.trim()) {
      refetch();
      return;
    }
    const results = await api.searchMemory(searchQuery);
    // Overwrite or update
  };

  const handleCreateMemory = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newContent.trim()) return;

    await api.createMemory({
      type: newType,
      content: newContent,
      importance: Number(newImportance),
      tags: newTags.split(',').map(t => t.trim()).filter(Boolean),
      source: 'UserDashboard'
    });

    setNewContent('');
    setIsCreating(false);
    refetch();
  };

  const handleDelete = async (id: string) => {
    if (confirm('Delete this memory item?')) {
      await api.deleteMemory(id);
      refetch();
    }
  };

  return (
    <div className="p-6 max-w-7xl mx-auto w-full space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-white flex items-center space-x-2">
            <Database className="w-5 h-5 text-cyan-400" />
            <span>Memory Engineering Store</span>
          </h1>
          <p className="text-sm text-gray-400 mt-1">
            Retained agent memory (ShortTerm, LongTerm, Episodic, Semantic). Note: <span className="text-cyan-300 font-mono">Memory ≠ Context</span>.
          </p>
        </div>
        <button
          onClick={() => setIsCreating(!isCreating)}
          className="flex items-center px-3.5 py-2 bg-blue-600 hover:bg-blue-500 text-white rounded-lg text-xs font-medium transition"
        >
          <Plus className="w-4 h-4 mr-1.5" />
          Add Memory
        </button>
      </div>

      {/* Creation Modal / Card */}
      {isCreating && (
        <form onSubmit={handleCreateMemory} className="p-4 bg-[#111827] border border-blue-500/40 rounded-xl space-y-3">
          <h2 className="text-sm font-semibold text-white">Create New Retained Memory</h2>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
            <div>
              <label className="text-xs text-gray-400">Memory Type</label>
              <select
                value={newType}
                onChange={(e) => setNewType(e.target.value as MemoryType)}
                className="w-full mt-1 bg-gray-900 border border-gray-700 rounded p-2 text-xs text-gray-200"
              >
                <option value="LongTerm">LongTerm</option>
                <option value="ShortTerm">ShortTerm</option>
                <option value="Episodic">Episodic</option>
                <option value="Semantic">Semantic</option>
              </select>
            </div>
            <div>
              <label className="text-xs text-gray-400">Importance (1.0 - 5.0)</label>
              <input
                type="number"
                step="0.5"
                min="1.0"
                max="5.0"
                value={newImportance}
                onChange={(e) => setNewImportance(parseFloat(e.target.value))}
                className="w-full mt-1 bg-gray-900 border border-gray-700 rounded p-2 text-xs text-gray-200"
              />
            </div>
            <div>
              <label className="text-xs text-gray-400">Tags (comma separated)</label>
              <input
                type="text"
                value={newTags}
                onChange={(e) => setNewTags(e.target.value)}
                className="w-full mt-1 bg-gray-900 border border-gray-700 rounded p-2 text-xs text-gray-200"
              />
            </div>
          </div>
          <div>
            <label className="text-xs text-gray-400">Content</label>
            <textarea
              rows={2}
              value={newContent}
              onChange={(e) => setNewContent(e.target.value)}
              placeholder="e.g., User instructed that cluster backups run nightly at 03:00 UTC."
              className="w-full mt-1 bg-gray-900 border border-gray-700 rounded p-2 text-xs text-gray-200"
            />
          </div>
          <div className="flex justify-end space-x-2">
            <button
              type="button"
              onClick={() => setIsCreating(false)}
              className="px-3 py-1.5 bg-gray-800 text-gray-300 rounded text-xs"
            >
              Cancel
            </button>
            <button
              type="submit"
              className="px-4 py-1.5 bg-blue-600 hover:bg-blue-500 text-white rounded text-xs font-medium"
            >
              Save Memory
            </button>
          </div>
        </form>
      )}

      {/* Filter and Search Bar */}
      <div className="flex flex-col md:flex-row gap-3">
        <div className="flex-1 relative">
          <Search className="w-4 h-4 text-gray-400 absolute left-3 top-2.5" />
          <input
            type="text"
            placeholder="Search retained memories by keyword..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="w-full bg-[#111827] border border-gray-800 rounded-lg pl-9 pr-3 py-2 text-xs text-gray-200 placeholder-gray-500 focus:outline-none focus:border-blue-500"
          />
        </div>
        <div className="flex space-x-2">
          {['', 'ShortTerm', 'LongTerm', 'Episodic', 'Semantic'].map((t) => (
            <button
              key={t}
              onClick={() => setSelectedType(t)}
              className={`px-3 py-2 rounded-lg text-xs font-medium transition ${
                selectedType === t
                  ? 'bg-blue-600/20 text-blue-400 border border-blue-500/40'
                  : 'bg-[#111827] text-gray-400 border border-gray-800 hover:text-gray-200'
              }`}
            >
              {t || 'All Types'}
            </button>
          ))}
        </div>
      </div>

      {/* Memories Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        {isLoading ? (
          <div className="text-gray-400 text-xs p-4">Loading memory items...</div>
        ) : memories?.length === 0 ? (
          <div className="col-span-full p-8 text-center text-gray-500 text-xs">No memories found.</div>
        ) : (
          memories?.map((mem) => (
            <div key={mem.id} className="bg-[#111827] border border-gray-800 rounded-xl p-4 flex flex-col justify-between space-y-3">
              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <span className={`text-[10px] font-mono px-2 py-0.5 rounded font-bold ${
                    mem.type === 'LongTerm' ? 'bg-purple-950/60 text-purple-300 border border-purple-800/60' :
                    mem.type === 'Episodic' ? 'bg-amber-950/60 text-amber-300 border border-amber-800/60' :
                    mem.type === 'Semantic' ? 'bg-blue-950/60 text-blue-300 border border-blue-800/60' :
                    'bg-gray-800 text-gray-300 border border-gray-700'
                  }`}>
                    {mem.type}
                  </span>
                  <div className="flex items-center space-x-2">
                    <span className="flex items-center text-xs text-amber-400 font-mono">
                      <Star className="w-3 h-3 mr-0.5 fill-amber-400 text-amber-400" />
                      {mem.importance}
                    </span>
                    <button
                      onClick={() => handleDelete(mem.id)}
                      className="text-gray-500 hover:text-rose-400 transition"
                      title="Delete memory"
                    >
                      <Trash2 className="w-3.5 h-3.5" />
                    </button>
                  </div>
                </div>

                <p className="text-xs text-gray-200 leading-relaxed">{mem.content}</p>
              </div>

              <div className="pt-2 border-t border-gray-800/60 space-y-2">
                <div className="flex flex-wrap gap-1">
                  {mem.tags.map((tag) => (
                    <span key={tag} className="text-[10px] bg-gray-900 text-gray-400 px-1.5 py-0.5 rounded border border-gray-800 flex items-center">
                      <Tag className="w-2.5 h-2.5 mr-1 text-gray-500" />
                      {tag}
                    </span>
                  ))}
                </div>
                <div className="flex items-center justify-between text-[10px] text-gray-500 font-mono">
                  <span>Src: {mem.source}</span>
                  <span>{new Date(mem.createdAt).toLocaleDateString()}</span>
                </div>
              </div>
            </div>
          ))
        )}
      </div>
    </div>
  );
};
