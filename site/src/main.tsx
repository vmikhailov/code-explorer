import React from 'react';
import { createRoot, hydrateRoot } from 'react-dom/client';
import { App } from './App';
import './styles/site.css';

const ROOT_ID = 'root';

const container = document.getElementById(ROOT_ID);
if (container) {
  const initialPath = window.location.pathname;
  const appElement = <App initialPath={initialPath} />;
  if (container.hasChildNodes()) {
    hydrateRoot(container, appElement);
  } else {
    createRoot(container).render(appElement);
  }
}
