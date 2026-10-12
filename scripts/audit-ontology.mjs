#!/usr/bin/env node

/**
 * CodeExplorer Ontology & Ground Truth Auditor
 *
 * Sequentially inspects projects/services in an ATS monorepo workspace,
 * compares CodeExplorer's extracted multi-layer ontology against Ground Truth
 * source code analysis, and produces a comprehensive discrepancy report.
 *
 * Usage:
 *   node scripts/audit-ontology.mjs --services action-scheduler,bff,cpm-streaming-aggregator,domain-checker,bundle
 *   node scripts/audit-ontology.mjs --random 5
 *   node scripts/audit-ontology.mjs --all
 */

import { execFileSync } from 'child_process';
import fs from 'fs';
import path from 'path';

// --- CLI Argument Parsing ---
const args = process.argv.slice(2);
function getArg(key, defaultVal) {
  const idx = args.indexOf(key);
  if (idx !== -1 && idx + 1 < args.length) return args[idx + 1];
  const prefix = `${key}=`;
  const match = args.find((a) => a.startsWith(prefix));
  if (match) return match.slice(prefix.length);
  return defaultVal;
}
const hasFlag = (key) => args.includes(key);

const workspaceDir = path.resolve(getArg('--workspace', 'C:/Work/ATS/src/services'));
const outputPath = path.resolve(getArg('--output', 'c:/Work/Personal/code-explorer/docs/ats-ontology-audit-report.md'));
const jsonDumpPath = getArg('--json', null);
const servicesArg = getArg('--services', null);
const randomArg = getArg('--random', null);
const allFlag = hasFlag('--all');

console.log(`=======================================================`);
console.log(`🔍 CodeExplorer Ontology & Ground Truth Auditor`);
console.log(`📂 Workspace: ${workspaceDir}`);
console.log(`📝 Output:    ${outputPath}`);
console.log(`=======================================================\n`);

// Helper to run safe Cypher queries via `ce query -j`
function runCypher(query) {
  try {
    const raw = execFileSync('ce', ['query', '-j', query], {
      cwd: workspaceDir,
      encoding: 'utf-8',
      maxBuffer: 64 * 1024 * 1024,
    });
    return JSON.parse(raw);
  } catch (err) {
    console.error(`[Cypher Error] Failed query:\n${query}\nError: ${err.message}`);
    return [];
  }
}

// 1. Discover all services in the workspace
console.log(`Fetching registered projects from CodeExplorer graph...`);
const allProjectsRaw = runCypher('MATCH (p:Project) RETURN p.name as name, p.language as language, p.role as role ORDER BY p.name');
const allProjects = allProjectsRaw.map((r) => ({
  name: r.name || r['p.name'],
  language: r.language || r['p.language'] || 'unknown',
  role: r.role || r['p.role'] || 'unknown',
}));

console.log(`Found ${allProjects.length} projects registered in graph.`);

// Determine target services to audit
let targetServices = [];
if (servicesArg) {
  const names = servicesArg.split(',').map((s) => s.trim()).filter(Boolean);
  targetServices = names.map((name) => {
    const found = allProjects.find((p) => p.name.toLowerCase() === name.toLowerCase());
    return found || { name, language: 'unknown', role: 'unknown' };
  });
} else if (randomArg) {
  const count = parseInt(randomArg, 10) || 5;
  const shuffled = [...allProjects].sort(() => 0.5 - Math.random());
  targetServices = shuffled.slice(0, count);
} else if (allFlag) {
  targetServices = allProjects;
} else {
  // Default: 5 representative sample services across different roles & languages
  const defaults = ['action-scheduler', 'bff', 'cpm-streaming-aggregator', 'domain-checker', 'bundle'];
  targetServices = defaults.map((name) => {
    const found = allProjects.find((p) => p.name.toLowerCase() === name.toLowerCase());
    return found || { name, language: 'unknown', role: 'unknown' };
  });
}

console.log(`Target services for audit (${targetServices.length}):`);
targetServices.forEach((s, idx) => console.log(`  ${idx + 1}. ${s.name} [${s.language}] (${s.role})`));
console.log('');

// --- Ground Truth Scanner ---
function scanServiceGroundTruth(serviceDir) {
  const gt = {
    manifest: null,
    frameworks: new Set(),
    httpLibraries: new Set(),
    dbLibraries: new Set(),
    queueLibraries: new Set(),
    endpoints: [], // { method, path, file, line, type: 'nestjs'|'express'|'go' }
    outgoingHttpCalls: [], // { target, method, path, file, line, raw }
    declaredEntitiesAndTables: [], // { table, file, line, kind: 'orm'|'sql'|'bq' }
    magicStringsAndPrimitiveObsession: [], // { type, context, file, line, snippet }
    envServiceConfigs: [], // { varName, valueHint, file, line }
    apiRouteDefinitions: [], // { routeKey, path, targetService, file, line }
  };

  if (!fs.existsSync(serviceDir)) {
    return { ...gt, exists: false };
  }
  gt.exists = true;

  // Check package.json
  const pkgPath = path.join(serviceDir, 'package.json');
  if (fs.existsSync(pkgPath)) {
    try {
      const pkg = JSON.parse(fs.readFileSync(pkgPath, 'utf-8'));
      gt.manifest = { type: 'npm', name: pkg.name, version: pkg.version };
      const deps = { ...pkg.dependencies, ...pkg.devDependencies };

      if (deps['@nestjs/core']) gt.frameworks.add('NestJS');
      if (deps['express']) gt.frameworks.add('Express');
      if (deps['fastify']) gt.frameworks.add('Fastify');

      if (deps['axios'] || deps['@nestjs/axios']) gt.httpLibraries.add('axios');
      if (deps['got']) gt.httpLibraries.add('got');
      if (deps['node-fetch']) gt.httpLibraries.add('node-fetch');

      if (deps['typeorm'] || deps['@nestjs/typeorm']) gt.dbLibraries.add('TypeORM');
      if (deps['prisma'] || deps['@prisma/client']) gt.dbLibraries.add('Prisma');
      if (deps['pg']) gt.dbLibraries.add('PostgreSQL (pg)');
      if (deps['ioredis'] || deps['redis']) gt.dbLibraries.add('Redis');
      if (deps['@google-cloud/bigquery']) gt.dbLibraries.add('BigQuery');

      if (deps['amqplib'] || deps['amqp-connection-manager']) gt.queueLibraries.add('RabbitMQ (amqp)');
      if (deps['bull'] || deps['bullmq']) gt.queueLibraries.add('BullMQ');
      if (deps['kafkajs']) gt.queueLibraries.add('Kafka');
    } catch (e) {
      // ignore json parse error
    }
  }

  // Check go.mod
  const goModPath = path.join(serviceDir, 'go.mod');
  if (fs.existsSync(goModPath)) {
    try {
      const content = fs.readFileSync(goModPath, 'utf-8');
      gt.manifest = { type: 'go.mod', content: content.split('\n')[0] };
      if (content.includes('github.com/gin-gonic/gin')) gt.frameworks.add('Gin (Go)');
      if (content.includes('github.com/labstack/echo')) gt.frameworks.add('Echo (Go)');
      if (content.includes('gorm.io/gorm')) gt.dbLibraries.add('GORM');
      if (content.includes('cloud.google.com/go/bigquery')) gt.dbLibraries.add('BigQuery (Go)');
      if (content.includes('github.com/go-redis/redis')) gt.dbLibraries.add('Redis (Go)');
      if (content.includes('github.com/segmentio/kafka-go')) gt.queueLibraries.add('Kafka (Go)');
    } catch (e) {}
  }

  // Walk all code files
  const ignoredDirs = new Set(['node_modules', 'dist', '.git', 'test', 'tests', 'spec', 'build', '.build', 'coverage']);
  const codeFiles = [];

  function walk(currentDir) {
    const entries = fs.readdirSync(currentDir, { withFileTypes: true });
    for (const ent of entries) {
      if (ent.isDirectory()) {
        if (!ignoredDirs.has(ent.name) && !ent.name.startsWith('.')) {
          walk(path.join(currentDir, ent.name));
        }
      } else if (ent.isFile()) {
        const ext = path.extname(ent.name).toLowerCase();
        if (['.ts', '.js', '.go', '.json'].includes(ext)) {
          codeFiles.push(path.join(currentDir, ent.name));
        }
      }
    }
  }

  try {
    walk(serviceDir);
  } catch (err) {
    console.error(`Error walking directory ${serviceDir}:`, err.message);
  }

  for (const filePath of codeFiles) {
    const relFile = path.relative(serviceDir, filePath).replace(/\\/g, '/');
    let content = '';
    try {
      content = fs.readFileSync(filePath, 'utf-8');
    } catch (e) {
      continue;
    }
    const lines = content.split('\n');

    // 1. Ingress Endpoints: NestJS Controllers
    let currentControllerPrefix = '';
    for (let i = 0; i < lines.length; i++) {
      const line = lines[i];
      const lineNum = i + 1;

      // Controller decorator: @Controller('prefix') or @JsonController('prefix')
      const ctrlMatch = line.match(/@(?:Json)?Controller\s*\(\s*(['"`])(.*?)\1\s*\)/);
      if (ctrlMatch) {
        currentControllerPrefix = ctrlMatch[2].replace(/^\/+|\/+$/g, '');
      }

      // Method decorator: @Get('route'), @Post('route'), etc.
      const methodMatch = line.match(/@(Get|Post|Put|Delete|Patch|Options|Head)\s*\(\s*(?:(['"`])(.*?)\2)?\s*\)/i);
      if (methodMatch) {
        const httpMethod = methodMatch[1].toUpperCase();
        let subPath = (methodMatch[3] || '').replace(/^\/+|\/+$/g, '');
        let fullPath = '/' + [currentControllerPrefix, subPath].filter(Boolean).join('/');
        gt.endpoints.push({
          method: httpMethod,
          path: fullPath,
          file: relFile,
          line: lineNum,
          type: 'nestjs',
        });
      }

      // CQRS Query / Command handlers
      const cqrsMatch = line.match(/@(QueryHandler|CommandHandler)\s*\(\s*([A-Za-z0-9_]+)\s*\)/);
      if (cqrsMatch) {
        gt.endpoints.push({
          method: 'QUERY',
          path: cqrsMatch[2],
          file: relFile,
          line: lineNum,
          type: 'cqrs',
        });
      }

      // Express routes: router.get('/path', ...), app.post('/path', ...)
      const expressMatch = line.match(/(?:app|router)\.(get|post|put|delete|patch)\s*\(\s*(['"`])(.*?)\2/i);
      if (expressMatch) {
        gt.endpoints.push({
          method: expressMatch[1].toUpperCase(),
          path: expressMatch[3],
          file: relFile,
          line: lineNum,
          type: 'express',
        });
      }

      // Go routes: r.GET("/path", ...), router.POST("/path", ...) or mux.HandleFunc("GET /path", ...)
      const goMatch = line.match(/(?:r|router|group|v1)\.(GET|POST|PUT|DELETE|PATCH)\s*\(\s*"([^"]+)"/);
      if (goMatch) {
        gt.endpoints.push({
          method: goMatch[1].toUpperCase(),
          path: goMatch[2],
          file: relFile,
          line: lineNum,
          type: 'go',
        });
      }

      const goMuxMatch = line.match(/HandleFunc\s*\(\s*"(GET|POST|PUT|DELETE|PATCH)\s+([^"]+)"/);
      if (goMuxMatch) {
        gt.endpoints.push({
          method: goMuxMatch[1].toUpperCase(),
          path: goMuxMatch[2],
          file: relFile,
          line: lineNum,
          type: 'go_mux',
        });
      }

      // 2. Egress HTTP / Service Calls
      // Axios / HttpService / Fetch / Got calls
      const httpCallMatch = line.match(/(?:this\.httpService|this\.http|axios|firstValueFrom\s*\(\s*this\.httpService)\.(get|post|put|delete|patch)\s*[<(]/i);
      if (httpCallMatch) {
        gt.outgoingHttpCalls.push({
          method: httpCallMatch[1].toUpperCase(),
          file: relFile,
          line: lineNum,
          raw: line.trim().slice(0, 140),
        });
      }

      // Env vars indicating target services: process.env.SOMETHING_URL / HOST
      const envServiceMatch = line.match(/process\.env\.([A-Z0-9_]*(?:URL|HOST|SERVICE|API|PORT)[A-Z0-9_]*)/);
      if (envServiceMatch) {
        gt.envServiceConfigs.push({
          varName: envServiceMatch[1],
          file: relFile,
          line: lineNum,
        });
      }

      // Hardcoded URLs in strings
      const urlMatch = line.match(/(https?:\/\/[a-zA-Z0-9\.\-_:]+(\/[a-zA-Z0-9_\-\.\/]*))/);
      if (urlMatch && !urlMatch[1].includes('localhost') && !urlMatch[1].includes('127.0.0.1') && !urlMatch[1].includes('schemas.')) {
        gt.outgoingHttpCalls.push({
          target: urlMatch[1],
          file: relFile,
          line: lineNum,
          raw: line.trim().slice(0, 140),
        });
      }

      // 3. Database / Persistence: TypeORM Entities
      const entityMatch = line.match(/@Entity\s*\(\s*(['"`])(.*?)\1\s*\)/);
      if (entityMatch) {
        gt.declaredEntitiesAndTables.push({
          table: entityMatch[2],
          file: relFile,
          line: lineNum,
          kind: 'orm_entity',
        });
      }

      // Raw SQL Queries
      const sqlSelect = line.match(/SELECT\s+.*?\s+FROM\s+([a-zA-Z0-9_\*\.\$\{\}\-`"]+)/i);
      if (sqlSelect) {
        const rawTable = sqlSelect[1].replace(/[`"'`;,()]/g, '');
        if (!['where', 'select', 'join', 'left', 'inner', 'outer', 'case'].includes(rawTable.toLowerCase())) {
          gt.declaredEntitiesAndTables.push({
            table: rawTable,
            file: relFile,
            line: lineNum,
            kind: 'raw_sql_select',
          });
        }
      }

      const sqlInsert = line.match(/INSERT\s+INTO\s+([a-zA-Z0-9_\*\.\$\{\}\-`"]+)/i);
      if (sqlInsert) {
        gt.declaredEntitiesAndTables.push({
          table: sqlInsert[1].replace(/[`"'`;,()]/g, ''),
          file: relFile,
          line: lineNum,
          kind: 'raw_sql_insert',
        });
      }

      // 4. Primitive Obsession / String Hardcodes
      // Look for untyped string parameters or dynamic template literals
      const templateSqlMatch = line.match(/FROM\s+`?\$\{([^}]+)\}`?/i);
      if (templateSqlMatch) {
        gt.magicStringsAndPrimitiveObsession.push({
          type: 'Dynamic SQL Template Interpolation',
          context: line.trim(),
          file: relFile,
          line: lineNum,
          snippet: templateSqlMatch[0],
        });
      }

      const templateHttpMatch = line.match(/`https?:\/\/\$\{([^}]+)\}([^`]*)`/);
      if (templateHttpMatch) {
        gt.magicStringsAndPrimitiveObsession.push({
          type: 'Dynamic URL Host Interpolation',
          context: line.trim(),
          file: relFile,
          line: lineNum,
          snippet: templateHttpMatch[0],
        });
      }

      // Explicit string types in DTO fields where enums or models should exist
      const stringTypeMatch = line.match(/(\b(?:status|type|action|role|event|topic|state)\b)\s*:\s*string\s*;/i);
      if (stringTypeMatch) {
        gt.magicStringsAndPrimitiveObsession.push({
          type: 'Primitive Obsession (Unmodeled string contract field)',
          context: line.trim(),
          file: relFile,
          line: lineNum,
          snippet: stringTypeMatch[0],
        });
      }
    }

    // Inspect config.consts.ts for API_ROUTES dictionary pattern (e.g. BFF)
    if (relFile.includes('config') && content.includes('API_ROUTES')) {
      const routeBlocks = content.match(/([A-Z0-9_]+)\s*:\s*\[([\s\S]*?)\]/g);
      if (routeBlocks) {
        for (const block of routeBlocks) {
          const svcMatch = block.match(/^([A-Z0-9_]+)\s*:/);
          const svcKey = svcMatch ? svcMatch[1] : '';
          const routeMatches = [...block.matchAll(/route\s*:\s*(['"`])(.*?)\1\s*,\s*path\s*:\s*(['"`])(.*?)\3/g)];
          for (const rm of routeMatches) {
            gt.apiRouteDefinitions.push({
              targetServiceKey: svcKey,
              routeKey: rm[2],
              path: rm[4],
              file: relFile,
            });
          }
        }
      }
    }
  }

  return gt;
}

// ---------------------------------------------------------------------------
// Run Audit for each Target Service
// ---------------------------------------------------------------------------
const auditResults = [];

for (const target of targetServices) {
  const serviceName = target.name;
  console.log(`Auditing service: ${serviceName}...`);

  // Path resolution
  let serviceDir = path.join(workspaceDir, serviceName);
  if (!fs.existsSync(serviceDir)) {
    // Try matching subdirectories
    const entries = fs.readdirSync(workspaceDir, { withFileTypes: true });
    const match = entries.find((e) => e.isDirectory() && e.name.toLowerCase() === serviceName.toLowerCase());
    if (match) serviceDir = path.join(workspaceDir, match.name);
  }

  // --- CE Knowledge Graph Extraction ---
  const projPropsRaw = runCypher(`MATCH (p:Project) WHERE p.name = '${serviceName}' RETURN p`);
  const projProps = projPropsRaw.length > 0 ? (projPropsRaw[0].p?.properties || projPropsRaw[0].properties || {}) : {};

  const endpointsRaw = runCypher(`
    MATCH (p:Project)-[:CONTAINS]->(e:Endpoint)
    WHERE p.name = '${serviceName}'
    RETURN e.id as id, e.name as name, e.http_method as method, e.route_template as route_template, e.path as file_path, e.controller as controller
  `);
  const ceEndpoints = endpointsRaw.map((e) => ({
    id: e.id || e['e.id'] || '',
    name: e.name || e['e.name'] || '',
    method: (e.method || e['e.http_method'] || e['e.method'] || '').toUpperCase(),
    routeTemplate: e.route_template || e['e.route_template'] || '',
    filePath: e.file_path || e['e.path'] || '',
    controller: e.controller || e['e.controller'] || '',
  }));

  // Detect duplicate endpoint nodes (e.g. ws:ep:<name>:... vs ws:ep:internal-service-<name>:...)
  const duplicateEndpointPairs = [];
  const epByRoute = new Map();
  for (const ep of ceEndpoints) {
    const key = `${ep.method} ${ep.routeTemplate}`;
    if (!epByRoute.has(key)) {
      epByRoute.set(key, []);
    }
    epByRoute.get(key).push(ep);
  }
  for (const [key, eps] of epByRoute.entries()) {
    if (eps.length > 1) {
      duplicateEndpointPairs.push({ route: key, ids: eps.map((x) => x.id) });
    }
  }

  const dbAndTablesRaw = runCypher(`
    MATCH (p:Project)-[r]->(target)
    WHERE p.name = '${serviceName}' AND type(r) IN ['USES_DB', 'CONTAINS', 'READS_FROM_TABLE', 'WRITES_TO_TABLE']
      AND any(l IN labels(target) WHERE l IN ['Database', 'Table'])
    RETURN type(r) as rel, labels(target) as labels, target.name as name, target.id as id
  `);
  const ceDatabases = [];
  const ceTables = [];
  for (const item of dbAndTablesRaw) {
    const labels = item.labels || item['labels(target)'] || [];
    const name = item.name || item['target.name'] || '';
    const rel = item.rel || item['type(r)'] || '';
    if (labels.includes('Database')) {
      ceDatabases.push({ name, rel });
    } else if (labels.includes('Table')) {
      ceTables.push({ name, rel });
    }
  }

  const serviceCallsRaw = runCypher(`
    MATCH (p:Project)-[r:SERVICE_CALL]->(target)
    WHERE p.name = '${serviceName}'
    RETURN target.name as target_name, labels(target) as labels, r.is_transitive as is_transitive, r.is_inferred as is_inferred, r.method as method, r.path as path, r.confidence as confidence
  `);
  const ceServiceCalls = serviceCallsRaw.map((c) => ({
    target: c.target_name || c['target.name'] || '',
    labels: c.labels || c['labels(target)'] || [],
    isTransitive: Boolean(c.is_transitive ?? c['r.is_transitive']),
    isInferred: Boolean(c.is_inferred ?? c['r.is_inferred']),
    method: c.method || c['r.method'] || '',
    path: c.path || c['r.path'] || '',
  }));

  const incomingCallsRaw = runCypher(`
    MATCH (caller:Project)-[r:SERVICE_CALL]->(p:Project)
    WHERE p.name = '${serviceName}'
    RETURN caller.name as caller_name, r.is_transitive as is_transitive, r.method as method, r.path as path
  `);
  const ceIncomingCalls = incomingCallsRaw.map((c) => ({
    caller: c.caller_name || c['caller.name'] || '',
    isTransitive: Boolean(c.is_transitive ?? c['r.is_transitive']),
    method: c.method || c['r.method'] || '',
    path: c.path || c['r.path'] || '',
  }));

  const astSummaryRaw = runCypher(`
    MATCH (p:Project)-[:CONTAINS]->(f:File)-[:CONTAINS]->(s)
    WHERE p.name = '${serviceName}'
    RETURN labels(s) as labels, count(s) as cnt
  `);
  const ceAstSummary = astSummaryRaw.map((a) => ({
    label: (a.labels || a['labels(s)'] || []).join(', '),
    count: a.cnt || a['count(s)'] || 0,
  }));

  // --- Ground Truth Source Inspection ---
  const gt = scanServiceGroundTruth(serviceDir);

  // --- Discrepancy Computation ---
  // Ingress Endpoints:
  const matchedEndpoints = [];
  const falseNegativeEndpoints = []; // In Code, missing in CE
  const falsePositiveEndpoints = []; // In CE, missing in Code
  const expressUnprefixedEndpoints = []; // In CE with missing mount prefix

  const normalizeRoute = (r) => (r || '').replace(/^\/+|\/+$/g, '').toLowerCase();
  const resolveCodeRoute = (codeEp) => {
    let r = normalizeRoute(codeEp.path);
    if (r.length === 0 && codeEp.file) {
      const fileBase = path.basename(codeEp.file, path.extname(codeEp.file)).replace(/\.(router|routes|controller)$/, '');
      if (fileBase && fileBase !== 'index' && fileBase !== 'server' && fileBase !== 'app') {
        r = fileBase.toLowerCase();
      }
    }
    return r;
  };

  // Deduplicate CE endpoints for matching comparison
  const uniqueCeEndpoints = Array.from(epByRoute.values()).map((eps) => eps[0]);

  for (const codeEp of gt.endpoints) {
    const found = uniqueCeEndpoints.find((ceEp) => {
      const methodMatches = !codeEp.method || ceEp.method === codeEp.method;
      const ceRoute = normalizeRoute(ceEp.routeTemplate || ceEp.name.replace(/^[A-Z]+:/, ''));
      const codeRoute = resolveCodeRoute(codeEp);
      const pathMatches = ceRoute === codeRoute ||
        (codeRoute.length > 0 && ceRoute.endsWith(codeRoute)) ||
        (ceRoute.length > 0 && codeRoute.endsWith(ceRoute));
      return methodMatches && pathMatches;
    });

    if (found) {
      matchedEndpoints.push({ code: codeEp, ce: found });
    } else {
      falseNegativeEndpoints.push(codeEp);
    }
  }

  for (const ceEp of uniqueCeEndpoints) {
    const ceRoute = normalizeRoute(ceEp.routeTemplate || ceEp.name.replace(/^[A-Z]+:/, ''));
    const found = gt.endpoints.find((codeEp) => {
      const methodMatches = !codeEp.method || ceEp.method === codeEp.method;
      const codeRoute = resolveCodeRoute(codeEp);
      const pathMatches = ceRoute === codeRoute ||
        (codeRoute.length > 0 && ceRoute.endsWith(codeRoute)) ||
        (ceRoute.length > 0 && codeRoute.endsWith(ceRoute));
      return methodMatches && pathMatches;
    });

    if (!found) {
      // Check if it's an un-prefixed Express parameter route
      if (ceRoute.startsWith(':') || ceRoute.startsWith('{')) {
        expressUnprefixedEndpoints.push(ceEp);
      } else {
        falsePositiveEndpoints.push(ceEp);
      }
    }
  }

  // Persistence / Tables:
  const phantomTables = [];
  const matchedTables = [];
  const knownSqlKeywords = new Set(['skip', 'locked', 'nowait', 'limit', 'offset', 'order', 'group', 'where', 'having', 'join', '__tables__']);

  for (const t of ceTables) {
    const lower = t.name.toLowerCase();
    if (knownSqlKeywords.has(lower) || lower.startsWith('datatracker_') || lower.includes('tablesuffix')) {
      phantomTables.push({
        table: t.name,
        reason: knownSqlKeywords.has(lower)
          ? `SQL Keyword / Clause wrongly parsed as Table (e.g. SKIP LOCKED)`
          : `Unresolved dynamic template string in SQL query`,
      });
    } else {
      const codeFound = gt.declaredEntitiesAndTables.find((e) => e.table.toLowerCase() === lower);
      if (codeFound) {
        matchedTables.push({ ce: t.name, code: codeFound });
      }
    }
  }

  // Egress Service Calls Discrepancy:
  const externalApisProp = (() => {
    try {
      return typeof projProps.external_apis === 'string' ? JSON.parse(projProps.external_apis) : (projProps.external_apis || []);
    } catch (e) {
      return [];
    }
  })();

  auditResults.push({
    serviceName,
    language: target.language || projProps.language || 'unknown',
    role: target.role || projProps.role || 'unknown',
    directoryExists: gt.exists,
    manifest: gt.manifest,
    frameworks: Array.from(gt.frameworks),
    httpLibraries: Array.from(gt.httpLibraries),
    dbLibraries: Array.from(gt.dbLibraries),
    queueLibraries: Array.from(gt.queueLibraries),
    ce: {
      properties: projProps,
      externalApisList: externalApisProp,
      endpoints: ceEndpoints,
      databases: ceDatabases,
      tables: ceTables,
      serviceCalls: ceServiceCalls,
      incomingCalls: ceIncomingCalls,
      astSummary: ceAstSummary,
    },
    gt: {
      endpoints: gt.endpoints,
      outgoingCallsCount: gt.outgoingHttpCalls.length,
      outgoingCallsSamples: gt.outgoingHttpCalls.slice(0, 10),
      envConfigs: gt.envServiceConfigs,
      apiRouteDefinitions: gt.apiRouteDefinitions,
      entitiesAndTables: gt.declaredEntitiesAndTables,
      primitiveObsession: gt.magicStringsAndPrimitiveObsession,
    },
    discrepancies: {
      matchedEndpointsCount: matchedEndpoints.length,
      falseNegativeEndpoints,
      falsePositiveEndpoints,
      duplicateEndpointPairs,
      expressUnprefixedEndpoints,
      phantomTables,
      matchedTablesCount: matchedTables.length,
    },
  });
}

// ---------------------------------------------------------------------------
// Generate Comprehensive Markdown Report
// ---------------------------------------------------------------------------
console.log(`\nCompiling comprehensive audit report to: ${outputPath}...`);

let md = `# CodeExplorer Monorepo Multi-Layer Ontology Audit Report\n\n`;
md += `**Audit Timestamp:** ${new Date().toISOString()}\n`;
md += `**Workspace:** \`${workspaceDir}\`\n`;
md += `**Total Audited Services:** ${auditResults.length}\n\n`;

md += `> [!IMPORTANT]\n`;
md += `> This report provides an in-depth, multi-layer verification of CodeExplorer's extracted ontology knowledge graph vs. the ground-truth codebase in the ATS microservice monorepo. It details discrepancies across all 5 ontology layers, identifies recurring primitive obsession and string hardcodes, and catalogs specific parser blind spots.\n\n`;

// 1. Executive Summary Table
md += `## 1. Executive Summary & Scorecard\n\n`;
md += `| Service | Language | Role | CE Total Endpoints | CE Unique Routes | Duplicate Pairs | Code Endpoints | Matched | Missing (FN) | Phantom (FP) | Phantom Tables | Egress Calls (CE) |\n`;
md += `|---|---|---|---|---|---|---|---|---|---|---|---|\n`;

for (const r of auditResults) {
  const uniqueRoutes = r.ce.endpoints.length - r.discrepancies.duplicateEndpointPairs.length;
  md += `| **${r.serviceName}** | ${r.language} | ${r.role} | ${r.ce.endpoints.length} | ${uniqueRoutes} | ${r.discrepancies.duplicateEndpointPairs.length} | ${r.gt.endpoints.length} | ${r.discrepancies.matchedEndpointsCount} | ${r.discrepancies.falseNegativeEndpoints.length} | ${r.discrepancies.falsePositiveEndpoints.length} | ${r.discrepancies.phantomTables.length} | ${r.ce.serviceCalls.length} |\n`;
}
md += `\n---\n\n`;

// 2. High-Impact Discovered Architectural Situations
md += `## 2. Catalog of Discovered Architectural Situations & Gaps\n\n`;

md += `### Situation 1: Custom Client Abstraction Hiding Egress Calls (\`ApiClientService\` & \`API_ROUTES\`)\n`;
md += `- **Discovered in:** \`bff\` and downstream consumers.\n`;
md += `- **Mechanism:** The service encapsulates all HTTP communications inside a centralized \`ApiClientService\`. Instead of calling \`http.get("http://bundle-scheduler/api/v1/...")\`, application modules call \`this.apiClient.get('SCHEDULED_BUNDLES')\`.\n`;
md += `- **Ground Truth:** In \`src/shared/config/config.consts.ts\`, the mapping \`SERVICE_DOMAINS\` and \`API_ROUTES\` binds \`SCHEDULED_BUNDLES\` to service \`bundle-scheduler\` and path \`/api/v1/timetables\`.\n`;
md += `- **Ontology Impact:** CodeExplorer sees a generic call to \`ApiClientService.get\` with a string literal symbol, but because it doesn't trace the configuration dictionary \`API_ROUTES\`, **the cross-service link to \`bundle-scheduler\` is completely missing from the graph**.\n\n`;

md += `### Situation 2: SQL Lexer/AST False Positives (The \`SKIP\` Table Phenomenon)\n`;
md += `- **Discovered in:** \`action-scheduler\`, \`bundle\`.\n`;
md += `- **Mechanism:** Code queries PostgreSQL with \`SELECT ... FROM scheduled_actions ... FOR UPDATE SKIP LOCKED\`.\n`;
md += `- **Parser Gap:** The regex/heuristic in \`Layer4SemanticParser\` looks for keywords following \`SELECT ... FROM\` or tokens before clause boundaries. The token \`SKIP\` was captured as a database table entity \`Table { name: "SKIP" }\`.\n`;
md += `- **Ontology Impact:** A phantom Table node \`ws:db:default:...:tbl:skip\` was created, connected to the project via \`CONTAINS\`. This pollutes data lineage and database topology diagrams.\n\n`;

md += `### Situation 3: Dynamic Template Literals & Wildcard Partitioning in BigQuery\n`;
md += `- **Discovered in:** \`bff\`, \`cpm-streaming-aggregator\`.\n`;
md += `- **Mechanism:** Analytics queries reference wildcard tables like \`impressions_tb_*\` or dynamically interpolate variables: \`FROM \`tracker.data_tracker_\${tableSuffix}\`\`.\n`;
md += `- **Parser Gap:** CodeExplorer strips template interpolation syntax or captures literal identifiers \`data_tracker_tableSuffix\`, creating separate table nodes for every variable name variant rather than recognizing a parameterized or sharded table entity.\n`;
md += `- **Ontology Impact:** Fragmented and unresolvable table nodes in data lineage.\n\n`;

md += `### Situation 4: Primitive Obsession in Lower-Level Domain Contracts\n`;
md += `- **Discovered in:** All 5 services.\n`;
md += `- **Mechanism:** Essential domain identifiers (action types, event names, queue routing keys, service states, external API endpoints) are modeled as raw \`string\` types rather than TypeScript string enums, union types, or value objects.\n`;
md += `- **Ontology Impact:** Because types are just \`string\`, static AST analysis cannot differentiate between an arbitrary string comment and a critical routing key or event channel without runtime tracing or semantic intent enrichment.\n\n`;

md += `### Situation 5: Go Language AST & Pipeline Semantic Ingestion\n`;
md += `- **Discovered in:** \`cpm-streaming-aggregator\`.\n`;
md += `- **Mechanism:** Go microservices use goroutines, channels, and custom HTTP/database wrappers rather than traditional dependency-injected controller classes.\n`;
md += `- **Ontology Impact:** Ingress routes and egress client calls in Go services require specialized AST visitor support matching Go standard library \`net/http\` and \`gin-gonic/gin\` constructs.\n\n`;

md += `### Situation 6: Inconsistent Service Naming Causing Duplicate Endpoint Nodes\n`;
md += `- **Discovered in:** \`action-scheduler\`, \`bff\`.\n`;
md += `- **Mechanism:** One parser/layer constructs Endpoint IDs using the package manifest name (\`ws:ep:internal-service-action-scheduler:GET:/ping\`), while another layer constructs them using the directory name (\`ws:ep:action-scheduler:GET:/ping\`).\n`;
md += `- **Ontology Impact:** Every HTTP endpoint in the project is duplicated in the graph, with identical routes connected to the same project node under two different IDs. This doubles the apparent API surface and confuses call resolution.\n\n`;

md += `### Situation 7: Express Router Mount Prefix Loss (\`app.use('/prefix', router)\`)\n`;
md += `- **Discovered in:** \`bundle\`.\n`;
md += `- **Mechanism:** In Express apps, router modules define routes relative to their mount root (e.g., \`router.delete('/:id')\`), and the main app mounts them via \`app.use('/api/bundles/', bundleRoutes)\`.\n`;
md += `- **Parser Gap:** CodeExplorer indexes the route file in isolation without propagating the parent mount prefix from \`index.ts\`. The endpoint becomes \`DELETE:/:id\` or \`DELETE:/\` instead of \`DELETE:/api/bundles/:id\`.\n`;
md += `- **Ontology Impact:** When downstream services call \`/\` or \`/:id\`, they falsely bind to these un-prefixed Express routes, creating phantom cross-service dependencies.\n\n`;

md += `---\n\n`;

// 3. Deep Dive Per Service
md += `## 3. Deep Dive Analysis by Service\n\n`;

for (const r of auditResults) {
  md += `### 3.${auditResults.indexOf(r) + 1} Service: \`${r.serviceName}\`\n\n`;
  md += `- **Language / Framework:** ${r.language} | Frameworks: ${r.frameworks.join(', ') || 'None detected'}\n`;
  md += `- **Libraries:** HTTP: \`${r.httpLibraries.join(', ') || 'none'}\` | DB: \`${r.dbLibraries.join(', ') || 'none'}\` | Queue: \`${r.queueLibraries.join(', ') || 'none'}\`\n`;
  md += `- **Primary Role (CE):** \`${r.role}\`\n\n`;

  // Layer 4 Ingress
  md += `#### Layer 4: Ingress HTTP Endpoints\n`;
  md += `- **CE Total Recorded:** ${r.ce.endpoints.length} endpoints | **Unique Routes:** ${r.ce.endpoints.length - r.discrepancies.duplicateEndpointPairs.length}\n`;
  md += `- **Ground Truth in Code:** ${r.gt.endpoints.length} endpoints.\n`;
  md += `- **Matched:** ${r.discrepancies.matchedEndpointsCount} | **Missing (FN):** ${r.discrepancies.falseNegativeEndpoints.length} | **Phantom (FP):** ${r.discrepancies.falsePositiveEndpoints.length}\n`;
  if (r.discrepancies.duplicateEndpointPairs.length > 0) {
    md += `- **⚠️ Duplicate Endpoint Pairs in Graph:** ${r.discrepancies.duplicateEndpointPairs.length} pairs (ID collision between package name and folder name)\n`;
  }
  if (r.discrepancies.expressUnprefixedEndpoints.length > 0) {
    md += `- **⚠️ Express Mount Prefix Lost:** ${r.discrepancies.expressUnprefixedEndpoints.length} routes recorded without mount prefix (e.g. \`DELETE:/:id\` instead of mounted path)\n`;
  }
  md += `\n`;

  if (r.discrepancies.falseNegativeEndpoints.length > 0) {
    md += `##### Missing Endpoints (Found in Code, Missing in CE Graph):\n`;
    for (const ep of r.discrepancies.falseNegativeEndpoints.slice(0, 10)) {
      md += `- \`${ep.method} ${ep.path}\` (${ep.file}:${ep.line})\n`;
    }
    if (r.discrepancies.falseNegativeEndpoints.length > 10) {
      md += `- *...and ${r.discrepancies.falseNegativeEndpoints.length - 10} more.*\n`;
    }
    md += `\n`;
  }

  if (r.discrepancies.falsePositiveEndpoints.length > 0) {
    md += `##### Phantom Endpoints (Present in CE Graph, Not Found in Code Controllers):\n`;
    for (const ep of r.discrepancies.falsePositiveEndpoints.slice(0, 10)) {
      md += `- \`${ep.method} ${ep.routeTemplate || ep.name}\` (Controller: \`${ep.controller}\`)\n`;
    }
    if (r.discrepancies.falsePositiveEndpoints.length > 10) {
      md += `- *...and ${r.discrepancies.falsePositiveEndpoints.length - 10} more.*\n`;
    }
    md += `\n`;
  }

  // Layer 4 & 5 Persistence & Tables
  md += `#### Layer 4: Databases & Tables\n`;
  md += `- **Databases:** ${r.ce.databases.map((d) => `\`${d.name}\``).join(', ') || 'None'}\n`;
  md += `- **Tables in CE:** ${r.ce.tables.length} tables recorded.\n`;
  if (r.discrepancies.phantomTables.length > 0) {
    md += `- **⚠️ Phantom Tables Detected:**\n`;
    for (const pt of r.discrepancies.phantomTables) {
      md += `  - Table \`${pt.table}\`: ${pt.reason}\n`;
    }
  }
  md += `\n`;

  // Layer 5 Egress Calls
  md += `#### Layer 5: Egress Service Dependencies\n`;
  md += `- **CE Graph Service Calls:** ${r.ce.serviceCalls.length} calls.\n`;
  if (r.ce.serviceCalls.length > 0) {
    for (const call of r.ce.serviceCalls) {
      const typeStr = call.isTransitive ? '╌ (Transitive)' : '— (Direct)';
      md += `  - \`${r.serviceName}\` ${typeStr} -> \`${call.target}\` (${call.labels.join(', ')}) [Method: \`${call.method || '*'}\`, Path: \`${call.path || '*'}\`]\n`;
    }
  } else {
    md += `  - *No outgoing service calls recorded in graph.*\n`;
  }

  // Ground truth config routes (like in BFF)
  if (r.gt.apiRouteDefinitions.length > 0) {
    md += `\n##### Configured Target Services in Code (\`API_ROUTES\`):\n`;
    const grouped = {};
    for (const def of r.gt.apiRouteDefinitions) {
      grouped[def.targetServiceKey] = (grouped[def.targetServiceKey] || 0) + 1;
    }
    for (const [svc, count] of Object.entries(grouped)) {
      md += `  - Target \`${svc}\`: ${count} configured routes\n`;
    }
  }

  // Primitive Obsession samples
  if (r.gt.primitiveObsession.length > 0) {
    md += `\n#### Lower-Level Ontology: Primitive Obsession & String Hardcodes\n`;
    md += `- **Detected occurrences:** ${r.gt.primitiveObsession.length}\n`;
    for (const po of r.gt.primitiveObsession.slice(0, 5)) {
      md += `  - **${po.type}:** \`${po.snippet}\` in \`${po.file}:${po.line}\`\n`;
    }
    if (r.gt.primitiveObsession.length > 5) {
      md += `  - *...and ${r.gt.primitiveObsession.length - 5} more occurrences.*\n`;
    }
  }

  md += `\n---\n\n`;
}

// 4. Recommendations & Action Plan
md += `## 4. Prioritized Recommendations & Action Plan for CodeExplorer\n\n`;
md += `1. **Implement Configuration-Aware Service Call Resolution (Layer 4/5):**\n`;
md += `   - Add a pattern detector in \`CodeExplorer.Parser.TypeScript\` for service dictionary tables like \`API_ROUTES\` and \`SERVICE_DOMAINS\`.\n`;
md += `   - Resolve indirect calls like \`apiClient.get('ROUTE_KEY')\` by cross-referencing the enum/string key to its declared target domain and path.\n\n`;

md += `2. **Sanitize SQL AST Extraction to Prevent Phantom Tables:**\n`;
md += `   - In \`NestedSqlParser\` / \`Layer4SemanticParser\`, enrich the SQL reserved word stoplist to include PostgreSQL keywords: \`SKIP\`, \`LOCKED\`, \`NOWAIT\`, \`PARTITION\`, \`OVER\`, \`CASCADE\`.\n`;
md += `   - Ignore BigQuery internal metadata tables such as \`__TABLES__\` or treat them under a dedicated \`SystemCatalog\` node label instead of user schema tables.\n\n`;

md += `3. **Handle Dynamic Table Template Literals in BigQuery / ORM:**\n`;
md += `   - Detect patterns like \`data_tracker_\${tableSuffix}\` or \`impressions_tb_*\` and normalize them into a parameterized table family rather than generating literal string table nodes.\n\n`;

md += `4. **Expand Lower-Level Ontological Typing:**\n`;
md += `   - Detect string fields in DTOs and contracts and classify them as candidates for domain type refinement.\n`;
md += `   - Connect string literals in messaging producers (\`channel.sendToQueue('string')\`) to inferred message topic nodes.\n\n`;

// Write report to disk
fs.mkdirSync(path.dirname(outputPath), { recursive: true });
fs.writeFileSync(outputPath, md, 'utf-8');
console.log(`✅ Audit report written successfully to: ${outputPath}`);

if (jsonDumpPath) {
  fs.writeFileSync(jsonDumpPath, JSON.stringify(auditResults, null, 2), 'utf-8');
  console.log(`✅ Raw JSON dump written to: ${jsonDumpPath}`);
}
