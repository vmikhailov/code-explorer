import React from 'react';
import { renderToString } from 'react-dom/server';
import { App } from './App';
import { ROUTES_DATA } from './generated/routesData';

export function renderRoute(routePath: string): string {
  return renderToString(<App initialPath={routePath} />);
}

export { ROUTES_DATA };
