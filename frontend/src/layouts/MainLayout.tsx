import React from 'react';
import { Outlet } from 'react-router-dom';
import { Sidebar } from '../components/Sidebar';

export const MainLayout: React.FC = () => {
  return (
    <div className="flex h-screen bg-[#0b0f17] text-gray-100 overflow-hidden">
      <Sidebar />
      <main className="flex-1 flex flex-col h-screen overflow-y-auto bg-[#0b0f17]">
        <Outlet />
      </main>
    </div>
  );
};
