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
    layerId: 'layer_ingress',
    layerName: 'Ingress',
    order: 0,
    color: '#fbbf24',
    icon: '⚡',
    description: 'Entry points, HTTP APIs, CLI commands, UI/Web, BFF, and Gateways',
  },
  {
    layerId: 'layer_components',
    layerName: 'Components',
    order: 1,
    color: '#c084fc',
    icon: '🧩',
    description: 'Domain services, core business logic, engines, schedulers, and processors',
  },
  {
    layerId: 'layer_egress',
    layerName: 'Egress',
    order: 2,
    color: '#38bdf8',
    icon: '📤',
    description: 'External clients, outbound adapters, notifiers, publishers, and integrations',
  },
  {
    layerId: 'layer_foundation',
    layerName: 'Storage & Foundation',
    order: 3,
    color: '#34d399',
    icon: '🗄️',
    description: 'Databases, persistence models, shared utilities, and common infrastructure',
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
