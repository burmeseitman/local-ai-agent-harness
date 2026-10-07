import React from 'react';
import { NavLink } from 'react-router-dom';
import {
  LayoutDashboard,
  Terminal,
  Wrench,
  Database,
  PlaySquare,
  Activity,
  BarChart3,
  Settings,
  ShieldCheck,
  Cpu
} from 'lucide-react';

export const Sidebar: React.FC = () => {
  const links = [
    { to: '/', label: 'Dashboard', icon: LayoutDashboard },
    { to: '/console', label: 'Agent Console', icon: Terminal },
    { to: '/tools', label: 'Tools', icon: Wrench },
    { to: '/memory', label: 'Memory', icon: Database },
    { to: '/runs', label: 'Runs', icon: PlaySquare },
    { to: '/traces', label: 'Traces', icon: Activity },
    { to: '/metrics', label: 'Metrics', icon: BarChart3 },
    { to: '/settings', label: 'Settings', icon: Settings },
  ];

  return (
    <aside className="w-64 bg-[#0e1420] border-r border-gray-800 flex flex-col flex-shrink-0 h-screen">
      {/* Brand Header */}
      <div className="p-4 border-b border-gray-800 flex items-center justify-between">
        <div className="flex items-center space-x-3">
          <div className="w-9 h-9 rounded-lg bg-blue-600/20 border border-blue-500/40 flex items-center justify-center text-blue-400">
            <ShieldCheck className="w-5 h-5" />
          </div>
          <div>
            <h1 className="text-sm font-semibold text-gray-100 tracking-wide">SECURE HARNESS</h1>
            <p className="text-xs text-gray-400 font-mono">v1.0.0 • Local AI</p>
          </div>
        </div>
      </div>

      {/* Nav Links */}
      <nav className="flex-1 px-3 py-4 space-y-1 overflow-y-auto">
        {links.map(({ to, label, icon: Icon }) => (
          <NavLink
            key={to}
            to={to}
            className={({ isActive }) =>
              `flex items-center px-3 py-2 text-sm font-medium rounded-lg transition-colors ${
                isActive
                  ? 'bg-blue-600/15 text-blue-400 border border-blue-500/30'
                  : 'text-gray-400 hover:text-gray-200 hover:bg-gray-800/60'
              }`
            }
          >
            <Icon className="w-4 h-4 mr-3" />
            {label}
          </NavLink>
        ))}
      </nav>

      {/* Local Node Health Widget */}
      <div className="p-4 border-t border-gray-800">
        <div className="p-3 bg-gray-900/80 border border-gray-800 rounded-lg">
          <div className="flex items-center justify-between mb-2">
            <span className="text-xs text-gray-400 font-medium">RUNTIME STATUS</span>
            <span className="flex h-2 w-2 relative">
              <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75"></span>
              <span className="relative inline-flex rounded-full h-2 w-2 bg-emerald-500"></span>
            </span>
          </div>
          <div className="flex items-center text-xs text-gray-300 font-mono space-x-2">
            <Cpu className="w-3.5 h-3.5 text-blue-400" />
            <span>Ollama / Local Agent</span>
          </div>
        </div>
      </div>
    </aside>
  );
};
