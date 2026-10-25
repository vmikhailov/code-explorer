import React from 'react';
import ReactDOM from 'react-dom/client';
import { App } from './App';
import { InspectorSidebarView } from './components/InspectorSidebarView';

const container = document.getElementById('root');
if (container) {
  const root = ReactDOM.createRoot(container);
  const isInspectorMode = (window as any).__CE_CONFIG__?.viewMode === 'inspector';
  root.render(isInspectorMode ? <InspectorSidebarView /> : <App />);
}
