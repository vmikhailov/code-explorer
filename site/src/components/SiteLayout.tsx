import React from 'react';
import { SlabHeader } from './SlabHeader';
import { MastFooter } from './MastFooter';

export interface SiteLayoutProps {
  currentRoute: string;
  children: React.ReactNode;
  afterFooter?: React.ReactNode;
}

export const SiteLayout: React.FC<SiteLayoutProps> = ({
  currentRoute,
  children,
  afterFooter,
}) => (
  <>
    <SlabHeader currentRoute={currentRoute} />
    {children}
    <MastFooter />
    {afterFooter}
  </>
);
