import { GraphData } from '../../../proto/types';

export interface LayerDefinition {
  layerId: string;
  layerName: string;
  order: number;
  color: string;
  icon: string;
  description: string;
}

export const DEFAULT_LAYERS: LayerDefinition[] = [
  {
    layerId: 'layer_presentation',
    layerName: 'Ingress & Presentation',
    order: 0,
    color: '#fbbf24',
    icon: '⚡',
    description: 'Entry points, CLI commands, HTTP APIs, and Host executables',
  },
  {
    layerId: 'layer_core',
    layerName: 'Application & Domain Core',
    order: 1,
    color: '#c084fc',
    icon: '🏛️',
    description: 'Core orchestration, domain models, and business logic',
  },
  {
    layerId: 'layer_engines',
    layerName: 'Domain Services & Specialized Engines',
    order: 2,
    color: '#38bdf8',
    icon: '⚙️',
    description: 'Parsers, query engines, algorithms, and domain handlers',
  },
  {
    layerId: 'layer_foundation',
    layerName: 'Foundation & Storage',
    order: 3,
    color: '#34d399',
    icon: '🗄️',
    description: 'Shared utilities, database entities, and common abstractions',
  },
  {
    layerId: 'layer_tests',
    layerName: 'Tests & Verification',
    order: 4,
    color: '#94a3b8',
    icon: '🧪',
    description: 'Unit tests, integration suites, benchmarks, and generator tools',
  },
];

export function getLayersFromGraph(graph: GraphData | null): LayerDefinition[] {
  if (graph?.metadata?.layers) {
    try {
      const parsed = JSON.parse(graph.metadata.layers) as LayerDefinition[];
      return parsed.sort((a, b) => a.order - b.order);
    } catch {}
  }
  return DEFAULT_LAYERS;
}
