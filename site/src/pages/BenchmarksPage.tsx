import React from 'react';
import { ROUTES_DATA } from '../generated/routesData';

const BENCHMARKS_KEY = '/benchmarks';

export const BenchmarksPage: React.FC = () => {
  const entry = ROUTES_DATA[BENCHMARKS_KEY];
  return (
    <main dangerouslySetInnerHTML={{ __html: entry.mainHtml }} />
  );
};
