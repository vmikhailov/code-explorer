import React from 'react';
import { ROUTES_DATA } from '../generated/routesData';

const PRIVACY_KEY = '/privacy';

export const PrivacyPage: React.FC = () => {
  const entry = ROUTES_DATA[PRIVACY_KEY];
  return (
    <main
      className="page-privacy"
      dangerouslySetInnerHTML={{ __html: entry.mainHtml }}
    />
  );
};
