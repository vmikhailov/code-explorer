import React, { useEffect } from 'react';
import { ROUTES_DATA } from '../generated/routesData';
import { initArchitecturesController } from '../generated/architecturesController';

const ARCH_KEY = '/architectures';

export const ArchitecturesPage: React.FC = () => {
  useEffect(() => initArchitecturesController(), []);

  const entry = ROUTES_DATA[ARCH_KEY];
  return (
    <main
      className="arch-page"
      dangerouslySetInnerHTML={{ __html: entry.mainHtml }}
    />
  );
};
