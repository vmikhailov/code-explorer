import React, { useEffect } from 'react';
import { ROUTES_DATA } from '../generated/routesData';
import { initOntologyController } from '../generated/ontologyController';

const ONTOLOGY_KEY = '/ontology';

export const OntologyPage: React.FC = () => {
  useEffect(() => initOntologyController(), []);

  const entry = ROUTES_DATA[ONTOLOGY_KEY];
  return (
    <main
      className="ontology-page"
      dangerouslySetInnerHTML={{ __html: entry.mainHtml }}
    />
  );
};
