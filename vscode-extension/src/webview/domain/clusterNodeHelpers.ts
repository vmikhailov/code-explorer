import { EntityKind, SelectedNodeDetail } from './types';
import { getEntityBadgeMeta } from './entityBadges';

export function parseServerClusterPreviewMembers(rawJson?: string): SelectedNodeDetail[] | undefined {
  if (!rawJson) return undefined;
  try {
    const parsedMembers = JSON.parse(rawJson);
    if (!Array.isArray(parsedMembers)) return undefined;
    return parsedMembers.map((m: any) => {
      const mKind = (m.kind || 'Service') as EntityKind;
      const mBadge = getEntityBadgeMeta(mKind);
      return {
        id: String(m.id || m.name),
        name: String(m.name || m.id),
        displayName: String(m.name || m.id),
        kind: mKind,
        displayTag: mBadge.code,
        bgColor: mBadge.bgColor,
        borderColor: mBadge.borderColor,
        primaryFilePath: m.filePath ? String(m.filePath) : undefined,
        projects: [],
        inboundCallsCount: 0,
        outboundCallsCount: 0,
        dbCount: 0,
        messagingCount: 0,
      };
    });
  } catch {
    return undefined;
  }
}
