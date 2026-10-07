import React from 'react';

interface StatusBadgeProps {
  status: string;
  size?: 'sm' | 'md';
}

export const StatusBadge: React.FC<StatusBadgeProps> = ({ status, size = 'md' }) => {
  const s = status.toUpperCase();

  let colorClasses = 'bg-gray-800 text-gray-300 border-gray-700';

  if (s === 'COMPLETED' || s === 'SUCCESS' || s === 'HEALTHY' || s === 'LOW' || s === 'ALLOWED') {
    colorClasses = 'bg-emerald-950/60 text-emerald-400 border-emerald-800/60';
  } else if (s === 'RUNNING' || s === 'PENDING' || s === 'MEDIUM') {
    colorClasses = 'bg-amber-950/60 text-amber-400 border-amber-800/60';
  } else if (s === 'FAILED' || s === 'BLOCKED' || s === 'CRITICAL' || s === 'HIGH' || s === 'FATAL') {
    colorClasses = 'bg-rose-950/60 text-rose-400 border-rose-800/60';
  }

  const sizeClasses = size === 'sm' ? 'px-1.5 py-0.5 text-xs' : 'px-2.5 py-1 text-xs';

  return (
    <span className={`inline-flex items-center font-mono font-medium rounded-md border ${sizeClasses} ${colorClasses}`}>
      <span className="w-1.5 h-1.5 rounded-full mr-1.5 bg-current opacity-75" />
      {status}
    </span>
  );
};
