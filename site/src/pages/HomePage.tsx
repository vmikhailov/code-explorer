import React, { useEffect } from 'react';
import { ROUTES_DATA } from '../generated/routesData';
import { initHomeController } from '../generated/homeController';

const HOME_KEY = '/';

export const HomePage: React.FC = () => {
  useEffect(() => initHomeController(), []);

  const entry = ROUTES_DATA[HOME_KEY];
  return (
    <main
      className="page-home"
      dangerouslySetInnerHTML={{ __html: entry.mainHtml }}
    />
  );
};

export const HomeOverlays: React.FC = () => (
  <>
    <div
      className="lightbox-modal"
      id="lightboxModal"
      role="dialog"
      aria-modal="true"
      aria-label="Architecture Cockpit screenshot preview"
    >
      <img
        id="lightboxImg"
        src="all_services.png"
        alt="Enlarged CodeExplorer Architecture Cockpit view"
      />
    </div>
    <div className="toast" id="toast" role="status" aria-live="polite">
      Copied to clipboard!
    </div>
  </>
);
