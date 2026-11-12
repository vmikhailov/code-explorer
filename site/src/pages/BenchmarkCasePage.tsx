import React from 'react';
import { ROUTES_DATA } from '../generated/routesData';

export interface BenchmarkCasePageProps {
  routePath: string;
}

const FALLBACK_KEY = '/benchmarks/01-agent-audit-ce-vs-cbm-on-twenty';

export const BenchmarkCasePage: React.FC<BenchmarkCasePageProps> = ({ routePath }) => {
  const entry = ROUTES_DATA[routePath] || ROUTES_DATA[FALLBACK_KEY];
  return (
    <main dangerouslySetInnerHTML={{ __html: entry.mainHtml }} />
  );
};
