import { GraphNode, isProjectKind } from '../../../proto/types';

export interface NodePresentation {
  kind: string;
  label: string;
  badgeLabel: string;
  badgeClass: string;
  icon: string;
  isExecutable: boolean;
  isResource: boolean;
  isLibrary: boolean;
  isExternal: boolean;
  c1Category: 'app' | 'service' | 'external' | null;
}

export function getNodePresentation(
  node: GraphNode | { kind: string; name?: string; displayName?: string; properties?: Record<string, string> }
): NodePresentation {
  const kind = node.kind || '';
  const name = node.name || '';
  const props = node.properties || {};
  const kindLower = kind.toLowerCase();
  const roleLower = (props.role || '').toLowerCase();
  const framework = props.framework || '';
  const projectType = (props.project_type || props.language || '').toLowerCase();

  const isLib =
    kindLower === 'library' ||
    kindLower === 'sharedlibrary' ||
    props.is_library === 'true' ||
    (props as any).is_library === true ||
    roleLower === 'sharedlibrary';

  const isDb = kindLower === 'database' || kindLower === 'table' || kindLower === 'query';
  const isTopic = kindLower === 'topic' || kindLower === 'queue' || kindLower === 'broker' || roleLower === 'topic';
  const isPeerMesh = kindLower === 'peermesh';
  const isPkg = kindLower === 'package';
  const isExt = kindLower === 'externalservice' || kindLower === 'cloudservice' || roleLower === 'externalservice';
  const isTest = kindLower === 'testsuite' || kindLower === 'test' || roleLower === 'test';
  const isEp = kindLower === 'endpoint' || kindLower === 'entrypoint';
  const isCli =
    kindLower === 'clitool' ||
    roleLower === 'clitool' ||
    props.entity_type === 'clitool' ||
    props.displayTag === ':CLI';

  // Badge label
  let badgeLabel = 'Project';
  if (kindLower === 'domain') {
    badgeLabel = 'Domain';
  } else if (kindLower === 'boundedcontext') {
    badgeLabel = 'Context';
  } else if (isCli) {
    badgeLabel = 'CLI';
  } else if (isDb) {
    badgeLabel = (props.engine || props.db_type || 'DB').toUpperCase();
  } else if (isTopic) {
    badgeLabel = (props.broker_type || 'QUEUE').toUpperCase();
  } else if (isExt) {
    badgeLabel = props.resource_type
      ? `${props.provider ? `${props.provider} ` : ''}${props.resource_type}`.toUpperCase()
      : (props.service_type || 'EXTERNAL').toUpperCase();
  } else if (isPeerMesh) {
    badgeLabel = 'PEER MESH';
  } else if (isPkg) {
    badgeLabel = (props.package_type || (props as any).pkg_type || 'PACKAGE').toUpperCase();
  } else if (isEp) {
    badgeLabel = (props.http_method || props.method || 'ENDPOINT').toUpperCase();
  } else if (projectType === 'go' || projectType === 'golang') {
    badgeLabel = 'Go';
  } else if (projectType === 'csharp' || projectType === 'cs' || projectType === 'dotnet') {
    badgeLabel = 'C#';
  } else if (projectType === 'typescript' || projectType === 'ts') {
    badgeLabel = 'TypeScript';
  } else if (projectType === 'javascript' || projectType === 'js') {
    badgeLabel = 'JavaScript';
  } else if (projectType === 'python' || projectType === 'py') {
    badgeLabel = 'Python';
  } else if (projectType === 'java') {
    badgeLabel = 'Java';
  } else if (projectType === 'kotlin') {
    badgeLabel = 'Kotlin';
  } else if (projectType === 'sql') {
    badgeLabel = 'SQL';
  } else if (framework) {
    badgeLabel = framework;
  } else if (props.role) {
    badgeLabel = props.role;
  } else if (kind) {
    badgeLabel = kind;
  }

  // Badge CSS class
  let badgeClass = 'badge-default';
  if (kindLower === 'domain') badgeClass = 'badge-domain';
  else if (kindLower === 'boundedcontext') badgeClass = 'badge-context';
  else if (isTest) badgeClass = 'badge-test';
  else if (isCli) badgeClass = 'badge-cli';
  else if (isEp) badgeClass = 'badge-endpoint';
  else if (isDb) badgeClass = 'badge-db';
  else if (isTopic) badgeClass = 'badge-topic';
  else if (isPeerMesh) badgeClass = 'badge-peer-mesh';
  else if (kindLower.includes('worker') || roleLower === 'worker') badgeClass = 'badge-worker';
  else if (isExt) badgeClass = 'badge-external';
  else if (isLib || isPkg) badgeClass = 'badge-library';
  else if (isProjectKind(kind) || kindLower.includes('service') || kindLower.includes('app')) badgeClass = 'badge-service';
  else if (kindLower.includes('type') || kindLower.includes('class')) badgeClass = 'badge-type';
  else if (kindLower.includes('function') || kindLower.includes('method')) badgeClass = 'badge-fn';

  // Icon
  let icon = '🔹';
  switch (kind) {
    case 'Domain':
      icon = '🌐';
      break;
    case 'BoundedContext':
      icon = '🧩';
      break;
    case 'TestSuite':
    case 'Test':
      icon = '🧪';
      break;
    case 'Service':
      icon = '⚙️';
      break;
    case 'App':
    case 'FrontendApp':
      icon = '🌐';
      break;
    case 'Worker':
      icon = '⚡';
      break;
    case 'CliTool':
      icon = '💻';
      break;
    case 'Library':
    case 'SharedLibrary':
      icon = '📚';
      break;
    case 'Package':
      icon = '📦';
      break;
    case 'Procedure':
      icon = '⚙️';
      break;
    case 'DataSet':
      icon = '📊';
      break;
    case 'Project':
      icon = isLib ? '📚' : '📦';
      break;
    case 'Endpoint':
      icon = '🌐';
      break;
    case 'Database':
      icon = '🗄️';
      break;
    case 'Table':
      icon = '📋';
      break;
    case 'Topic':
      icon = '📬';
      break;
    case 'EntryPoint':
      icon = '🚪';
      break;
    case 'ExternalService':
      icon = '☁️';
      break;
    case 'CloudService':
      icon = '⚡';
      break;
    case 'PeerMesh':
      icon = '🔗';
      break;
    case 'ApiInUse':
      icon = '🔌';
      break;
    case 'Query':
      icon = '🔍';
      break;
    case 'Type':
      icon = '🔷';
      break;
    case 'Function':
      icon = '⚡';
      break;
    case 'Member':
      icon = '🔹';
      break;
    case 'File':
      icon = '📄';
      break;
    case 'Folder':
      icon = '📁';
      break;
    case 'GitSettings':
      icon = '🌿';
      break;
    default:
      if (isDb) icon = '🗄️';
      else if (isTopic) icon = '📬';
      else if (isExt) icon = '☁️';
      else if (isLib) icon = '📚';
      else if (isPkg) icon = '📦';
      break;
  }

  // Executable check (runnable workloads eligible for flow inspection)
  const isExecutable =
    !isLib &&
    !isPkg &&
    !isDb &&
    !isTopic &&
    !isExt &&
    !isTest &&
    !isEp &&
    (isProjectKind(kind) ||
      kindLower === 'service' ||
      kindLower === 'app' ||
      kindLower === 'frontendapp' ||
      kindLower === 'worker' ||
      kindLower === 'clitool');

  // C1 Category for system context diagrams
  let c1Category: 'app' | 'service' | 'external' | null = null;
  if (
    !isLib &&
    !isDb &&
    !isTopic &&
    !isPkg &&
    !isTest &&
    !isEp &&
    kindLower !== 'type' &&
    kindLower !== 'function' &&
    kindLower !== 'workspace'
  ) {
    if (isExt) {
      c1Category = 'external';
    } else if (
      kindLower === 'app' ||
      kindLower === 'frontendapp' ||
      kindLower === 'ingress' ||
      roleLower === 'app' ||
      roleLower === 'frontendapp' ||
      roleLower === 'ingress'
    ) {
      c1Category = 'app';
    } else if (
      kindLower === 'service' ||
      kindLower === 'worker' ||
      kindLower === 'clitool' ||
      roleLower === 'service' ||
      roleLower === 'worker' ||
      roleLower === 'clitool' ||
      isProjectKind(kind)
    ) {
      c1Category = 'service';
    }
  }

  return {
    kind,
    label: (node as any).displayName || name,
    badgeLabel,
    badgeClass,
    icon,
    isExecutable,
    isResource: isDb || isTopic || isPeerMesh || kindLower === 'cloudservice',
    isLibrary: isLib || isPkg,
    isExternal: isExt,
    c1Category,
  };
}
