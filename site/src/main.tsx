import React from 'react';
import { createRoot, hydrateRoot } from 'react-dom/client';
import { App } from './App';
import { normalizeRoute } from './router';
import './styles/site.css';

const ROOT_ID = 'root';
const HOME_ROUTE = '/';

const container = document.getElementById(ROOT_ID);
if (container) {
  const initialPath = window.location.pathname;
  const normalizedInitial = normalizeRoute(initialPath);
  const appElement = <App initialPath={initialPath} />;
  if (container.hasChildNodes() && normalizedInitial === HOME_ROUTE) {
    hydrateRoot(container, appElement);
  } else {
    container.innerHTML = '';
    createRoot(container).render(appElement);
  }
}
