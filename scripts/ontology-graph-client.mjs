/**
 * Inline interactive Canvas + Inspector runtime for site/ontology.html.
 */
function renderLayoutPart(safeJsonInline) {
  return `  <script>
    (function () {
      var SCHEMA = ${safeJsonInline};
      var viewMode = "graph";
      var selectedKind = "Endpoint";
      var focusedKind = null;
      var inspectorExpanded = false;
      var hoveredKind = null;
      var hoveredCapsule = null;
      var searchQuery = "";
      var activeCats = {
        control_flow: true,
        data_flow: true,
        type_dependency: true,
        structural: true
      };
      var activeLayers = {
        5: true,
        4: true,
        3: true,
        2: true,
        1: true
      };
      var expandedHierarchies = {};
      for (var hi = 0; hi < SCHEMA.nodes.length; hi++) {
        if (SCHEMA.nodes[hi].subtypes && SCHEMA.nodes[hi].subtypes.length > 0) {
          expandedHierarchies[SCHEMA.nodes[hi].kind] = true;
        }
      }
      var lastVisKey = "";

      function cssToken(name) {
        return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
      }

      var PAL = {
        canvasBg: cssToken("--ont-canvas-bg"),
        gridDot: cssToken("--ont-grid-dot"),
        logical: cssToken("--ont-c-logical"),
        module: cssToken("--ont-c-module"),
        workload: cssToken("--ont-c-workload"),
        contract: cssToken("--ont-c-contract"),
        data: cssToken("--ont-c-data"),
        cloud: cssToken("--ont-c-cloud"),
        syntax: cssToken("--ont-c-syntax"),
        slicing: cssToken("--ont-c-slicing"),
        project: cssToken("--ont-c-project"),
        physical: cssToken("--ont-c-physical"),
        capOuterFill: cssToken("--ont-capsule-outer-fill"),
        capOuterBorder: cssToken("--ont-capsule-outer-border"),
        capInnerFill: cssToken("--ont-capsule-inner-fill"),
        capInnerBorder: cssToken("--ont-capsule-inner-border"),
        capHoverBorder: cssToken("--ont-capsule-hover-border"),
        capHeadBg: cssToken("--ont-capsule-head-bg"),
        capHeadHoverBg: cssToken("--ont-capsule-head-hover-bg"),
        abstractRing: cssToken("--ont-abstract-ring"),
        badgeBg: cssToken("--ont-badge-bg"),
        badgeBorder: cssToken("--ont-badge-border"),
        pillBg: cssToken("--ont-pill-bg"),
        pillBorder: cssToken("--ont-pill-border"),
        pillBorderActive: cssToken("--ont-pill-border-active"),
        edgeIdle: cssToken("--ont-edge-idle"),
        edgeDim: cssToken("--ont-edge-dim"),
        edgeControl: cssToken("--ont-edge-control"),
        edgeData: cssToken("--ont-edge-data"),
        edgeType: cssToken("--ont-edge-type"),
        edgeStruct: cssToken("--ont-edge-struct"),
        nodeRing: cssToken("--ont-node-ring"),
        nodeHalo: cssToken("--ont-node-halo"),
        nodeHover: cssToken("--ont-node-hover"),
        inkBright: cssToken("--ont-ink-bright"),
        inkSoft: cssToken("--ont-ink-soft"),
        inkDim: cssToken("--ont-ink-dim"),
        inkZero: cssToken("--ont-ink-zero")
      };

      var STYLES = {
        Domain: { fill: PAL.logical, glyph: "globe", cluster: 8 },
        BoundedContext: { fill: PAL.logical, glyph: "layers", cluster: 8 },
        Module: { fill: PAL.module, glyph: "package", cluster: 0 },
        Workload: { fill: PAL.workload, glyph: "layers", cluster: 0 },
        App: { fill: PAL.workload, glyph: "browser", cluster: 0 },
        Service: { fill: PAL.workload, glyph: "server", cluster: 0 },
        Worker: { fill: PAL.workload, glyph: "gear", cluster: 0 },
        CliTool: { fill: PAL.workload, glyph: "terminal", cluster: 0 },
        Library: { fill: PAL.module, glyph: "library", cluster: 0 },
        TestSuite: { fill: PAL.module, glyph: "beaker", cluster: 0 },
        Endpoint: { fill: PAL.contract, glyph: "endpoint", cluster: 1 },
        Topic: { fill: PAL.contract, glyph: "topic", cluster: 1 },
        PeerMesh: { fill: PAL.contract, glyph: "mesh", cluster: 1 },
        Database: { fill: PAL.data, glyph: "database", cluster: 2 },
        DbSchema: { fill: PAL.data, glyph: "schema", cluster: 2 },
        Table: { fill: PAL.data, glyph: "table", cluster: 2 },
        Query: { fill: PAL.data, glyph: "query", cluster: 2 },
        Procedure: { fill: PAL.data, glyph: "procedure", cluster: 2 },
        ExternalService: { fill: PAL.cloud, glyph: "cloud", cluster: 3 },
        Cloud: { fill: PAL.cloud, glyph: "cloudUp", cluster: 3 },
        CloudResource: { fill: PAL.cloud, glyph: "server", cluster: 3 },
        Type: { fill: PAL.syntax, glyph: "type", cluster: 4 },
        Function: { fill: PAL.syntax, glyph: "function", cluster: 4 },
        Member: { fill: PAL.syntax, glyph: "member", cluster: 4 },
        CatchClause: { fill: PAL.syntax, glyph: "catch", cluster: 4 },
        Parameter: { fill: PAL.slicing, glyph: "param", cluster: 5 },
        CallArgument: { fill: PAL.slicing, glyph: "arg", cluster: 5 },
        CallLiteral: { fill: PAL.slicing, glyph: "literal", cluster: 5 },
        Project: { fill: PAL.project, glyph: "project", cluster: 6 },
        EntryPoint: { fill: PAL.project, glyph: "zap", cluster: 6 },
        Package: { fill: PAL.project, glyph: "package", cluster: 6 },
        File: { fill: PAL.physical, glyph: "file", cluster: 7 },
        Folder: { fill: PAL.physical, glyph: "folder", cluster: 7 },
        GitSettings: { fill: PAL.physical, glyph: "git", cluster: 7 }
      };

      var ANCHORS = [
        { x: 255, y: 235, rx: 145, ry: 105 },
        { x: 635, y: 255, rx: 145, ry: 85 },
        { x: 1125, y: 285, rx: 135, ry: 110 },
        { x: 925, y: 155, rx: 105, ry: 60 },
        { x: 705, y: 495, rx: 165, ry: 115 },
        { x: 1065, y: 575, rx: 125, ry: 95 },
        { x: 305, y: 605, rx: 130, ry: 90 },
        { x: 645, y: 695, rx: 155, ry: 55 },
        { x: 595, y: 115, rx: 85, ry: 24 }
      ];

      var TREE_ANCHORS = {
        App: { x: 115, y: 145 },
        CliTool: { x: 115, y: 265 },
        Workload: { x: 265, y: 215 },
        Service: { x: 405, y: 150 },
        Worker: { x: 405, y: 270 },
        Module: { x: 295, y: 385 },
        Library: { x: 135, y: 385 },
        TestSuite: { x: 155, y: 495 }
      };

      var SUB_HUBS = { App: 1, Worker: 1, CliTool: 1, Library: 1, TestSuite: 1 };
      var ACTIVE_EDGE_COLORS = {
        control_flow: PAL.edgeControl,
        data_flow: PAL.edgeData,
        type_dependency: PAL.edgeType,
        structural: PAL.edgeStruct
      };

      var nodeMap = {};
      for (var i = 0; i < SCHEMA.nodes.length; i++) {
        nodeMap[SCHEMA.nodes[i].kind] = SCHEMA.nodes[i];
      }

      var relMap = {};
      for (var j = 0; j < SCHEMA.relationships.length; j++) {
        relMap[SCHEMA.relationships[j].rel] = SCHEMA.relationships[j];
      }

      function collectDescendants(kind, visited) {
        var nd = nodeMap[kind];
        if (!nd || !nd.subtypes || nd.subtypes.length === 0) return [];
        var acc = [];
        for (var i = 0; i < nd.subtypes.length; i++) {
          var sub = nd.subtypes[i];
          if (visited[sub]) continue;
          visited[sub] = true;
          acc.push(sub);
          var deep = collectDescendants(sub, visited);
          for (var d = 0; d < deep.length; d++) acc.push(deep[d]);
        }
        return acc;
      }

      var hierarchies = [];
      for (var hc = 0; hc < SCHEMA.nodes.length; hc++) {
        var hNode = SCHEMA.nodes[hc];
        if (hNode.subtypes && hNode.subtypes.length > 0) {
          hierarchies.push({
            kind: hNode.kind,
            depth: hNode.ancestors ? hNode.ancestors.length : 0,
            allDescendants: collectDescendants(hNode.kind, {})
          });
        }
      }
      hierarchies.sort(function (a, b) {
        return a.depth - b.depth || (a.kind < b.kind ? -1 : 1);
      });

      function resolveHierarchy(filteredDtos) {
        var filteredSet = {};
        for (var i = 0; i < filteredDtos.length; i++) {
          filteredSet[filteredDtos[i].kind] = true;
        }
        var hiddenKinds = {};
        var redirectMap = {};
        var collapsedCounts = {};

        for (var h = 0; h < hierarchies.length; h++) {
          var hd = hierarchies[h];
          if (expandedHierarchies[hd.kind]) continue;
          if (!filteredSet[hd.kind] || hiddenKinds[hd.kind]) continue;
          var cnt = 0;
          for (var d = 0; d < hd.allDescendants.length; d++) {
            var desc = hd.allDescendants[d];
            if (filteredSet[desc] && !hiddenKinds[desc]) {
              hiddenKinds[desc] = true;
              cnt++;
            }
            if (!redirectMap[desc]) redirectMap[desc] = hd.kind;
          }
          if (cnt > 0) collapsedCounts[hd.kind] = cnt;
        }

        var visibleDtos = [];
        for (var v = 0; v < filteredDtos.length; v++) {
          if (!hiddenKinds[filteredDtos[v].kind]) visibleDtos.push(filteredDtos[v]);
        }
        return {
          visibleDtos: visibleDtos,
          redirectMap: redirectMap,
          collapsedCounts: collapsedCounts
        };
      }

      function escHtml(s) {
        return String(s || "")
          .replace(/&/g, "&amp;")
          .replace(/</g, "&lt;")
          .replace(/>/g, "&gt;")
          .replace(/"/g, "&quot;");
      }

      function matchesQuery(node, q) {
        if (!q) return true;
        if (
          node.kind.toLowerCase().indexOf(q) !== -1 ||
          node.pluralLabel.toLowerCase().indexOf(q) !== -1 ||
          node.purpose.toLowerCase().indexOf(q) !== -1 ||
          node.idScheme.toLowerCase().indexOf(q) !== -1 ||
          (node.extends && node.extends.toLowerCase().indexOf(q) !== -1)
        ) {
          return true;
        }
        if (node.subtypes) {
          for (var st = 0; st < node.subtypes.length; st++) {
            if (node.subtypes[st].toLowerCase().indexOf(q) !== -1) return true;
          }
        }
        for (var i = 0; i < node.properties.length; i++) {
          var p = node.properties[i];
          if (p.name.toLowerCase().indexOf(q) !== -1 || p.description.toLowerCase().indexOf(q) !== -1) return true;
        }
        var outs = node.inheritedOutEdges ? node.outEdges.concat(node.inheritedOutEdges) : node.outEdges;
        for (var o = 0; o < outs.length; o++) {
          var oe = outs[o];
          if (oe.rel.toLowerCase().indexOf(q) !== -1 || oe.toKind.toLowerCase().indexOf(q) !== -1) return true;
        }
        var ins = node.inheritedInEdges ? node.inEdges.concat(node.inheritedInEdges) : node.inEdges;
        for (var k = 0; k < ins.length; k++) {
          var ie = ins[k];
          if (ie.rel.toLowerCase().indexOf(q) !== -1 || ie.fromKind.toLowerCase().indexOf(q) !== -1) return true;
        }
        return false;
      }

      var WORLD_CX = 690;
      var WORLD_CY = 440;
      var ALPHA_DECAY = 0.0228;
      var ALPHA_MIN = 0.001;
      var VEL_RETENTION = 0.6;
      var simBodies = [];
      var simByKind = {};
      var simSprings = [];
      var spacingFactor = 1.0;
      var hubFactor = 1.0;
      var simAlpha = 0;
      var simAlphaTarget = 0;
      var simFrozen = false;
      var simRafId = null;
      var lastSimSig = "";

      try {
        var savedAir = localStorage.getItem("ce_ontology_air");
        if (savedAir !== null) {
          var parsedAir = parseFloat(savedAir);
          if (!isNaN(parsedAir) && parsedAir >= 0.1 && parsedAir <= 2.4) {
            spacingFactor = parsedAir;
          }
        }
        var savedHub = localStorage.getItem("ce_ontology_hub");
        if (savedHub !== null) {
          var parsedHub = parseFloat(savedHub);
          if (!isNaN(parsedHub) && parsedHub >= 0.0 && parsedHub <= 5.0) {
            hubFactor = parsedHub;
          }
        }
      } catch (_err) {}

      function computeSeedMap(visibleDtos) {
        var buckets = {};
        for (var i = 0; i < visibleDtos.length; i++) {
          var n = visibleDtos[i];
          var st = STYLES[n.kind] || { fill: PAL.physical, glyph: "file", cluster: 7 };
          if (!buckets[st.cluster]) buckets[st.cluster] = [];
          buckets[st.cluster].push(n);
        }
        var seeds = {};
        for (var cid in buckets) {
          var list = buckets[cid];
          var anc = ANCHORS[cid] || ANCHORS[0];
          for (var idx = 0; idx < list.length; idx++) {
            var dto = list[idx];
            var style = STYLES[dto.kind] || { fill: PAL.physical, glyph: "file", cluster: 7 };
            var treePos = Number(cid) === 0 && list.length > 1 ? TREE_ANCHORS[dto.kind] : null;
            var ang = list.length === 1 ? 0 : (idx / list.length) * Math.PI * 2 - Math.PI / 2;
            var ax = treePos ? treePos.x : anc.x + (list.length === 1 ? 0 : Math.cos(ang) * anc.rx);
            var ay = treePos ? treePos.y : anc.y + (list.length === 1 ? 0 : Math.sin(ang) * anc.ry);
            seeds[dto.kind] = {
              ax: ax,
              ay: ay,
              style: style,
              anchorStrength: treePos ? 0.072 : 0.048
            };
          }
        }
        return seeds;
      }

      function buildSimSprings(byKindMap, redirectMap) {
        var rawPairs = [];
        var seen = {};
        var counts = {};

        function addPair(fk, tk, isExt) {
          if (fk === tk) return;
          var ba = byKindMap[fk];
          var bb = byKindMap[tk];
          if (!ba || !bb) return;
          var minK = fk < tk ? fk : tk;
          var maxK = fk < tk ? tk : fk;
          var pk = minK + "::" + maxK;
          if (seen[pk]) return;
          seen[pk] = true;
          rawPairs.push({ a: ba, b: bb, isExt: isExt });
          counts[fk] = (counts[fk] || 0) + 1;
          counts[tk] = (counts[tk] || 0) + 1;
        }

        for (var ex = 0; ex < SCHEMA.nodes.length; ex++) {
          var exNode = SCHEMA.nodes[ex];
          if (exNode.extends) addPair(exNode.kind, exNode.extends, true);
        }
        for (var r = 0; r < SCHEMA.relationships.length; r++) {
          var rd = SCHEMA.relationships[r];
          if (!activeCats[rd.category]) continue;
          for (var p = 0; p < rd.pairs.length; p++) {
            var pr = rd.pairs[p];
            var ef = (redirectMap && redirectMap[pr.fromKind]) || pr.fromKind;
            var et = (redirectMap && redirectMap[pr.toKind]) || pr.toKind;
            addPair(ef, et, false);
          }
        }

        for (var bk in byKindMap) {
          byKindMap[bk].degree = Math.max(1, counts[bk] || 1);
        }

        var springs = [];
        for (var s = 0; s < rawPairs.length; s++) {
          var rp = rawPairs[s];
          var ca = rp.a.degree || 1;
          var cb = rp.b.degree || 1;
          var bias = Math.max(0.22, Math.min(0.78, ca / (ca + cb)));
          var minDeg = Math.max(1, Math.min(ca, cb));
          var baseStr = rp.isExt ? 0.54 : 0.48 / Math.sqrt(minDeg);
          var str = Math.max(0.16, Math.min(0.56, baseStr));
          var sameCl = rp.a.style.cluster === rp.b.style.cluster;
          var dist = rp.isExt ? 126 : (sameCl ? 144 : 212);
          springs.push({ source: rp.a, target: rp.b, distance: dist, strength: str, bias: bias });
        }
        return springs;
      }

      function stepSimBodies(bodies, springs, alpha, spFactor, hbFactor) {
        var hf = typeof hbFactor === "number" ? hbFactor : 1.0;
        var maxChargeD2 = Math.pow(540 * spFactor * (1 + hf * 0.25), 2);
        var charge = -620 * spFactor * alpha;

        for (var a = 0; a < bodies.length; a++) {
          var b1 = bodies[a];
          var d1 = b1.degree || 1;
          var w1 = 1 + hf * 0.24 * Math.max(0, d1 - 2);
          for (var c = a + 1; c < bodies.length; c++) {
            var b2 = bodies[c];
            var d2Deg = b2.degree || 1;
            var dx = b2.x - b1.x;
            var dy = b2.y - b1.y;
            var d2 = dx * dx + dy * dy;
            if (d2 > maxChargeD2) continue;
            if (d2 < 1) {
              dx = a % 2 === 0 ? 1 : -1;
              dy = c % 2 === 0 ? 0.5 : -0.5;
              d2 = 1.25;
            }
            var w2 = 1 + hf * 0.24 * Math.max(0, d2Deg - 2);
            var pairHubMult = 0.5 * (w1 + w2) * (1 + hf * 0.08 * Math.min(d1, d2Deg));
            var w = (charge * pairHubMult) / Math.max(d2, 900);
            if (b1.fx === null) { b1.vx += dx * w; b1.vy += dy * w; }
            if (b2.fx === null) { b2.vx -= dx * w; b2.vy -= dy * w; }
          }
        }

        for (var l = 0; l < springs.length; l++) {
          var sp = springs[l];
          var ldx = sp.target.x + sp.target.vx - sp.source.x - sp.source.vx;
          var ldy = sp.target.y + sp.target.vy - sp.source.y - sp.source.vy;
          var ldist = Math.sqrt(ldx * ldx + ldy * ldy) || 1;
          var degEx = Math.max(0, (sp.source.degree || 1) + (sp.target.degree || 1) - 4);
          var hubDistM = 1 + hf * 0.042 * degEx;
          var hubStrM = 1 / (1 + hf * 0.055 * degEx);
          var tDist = sp.distance * spFactor * hubDistM;
          var k = ((ldist - tDist) / ldist) * alpha * sp.strength * hubStrM;
          ldx *= k;
          ldy *= k;
          if (sp.target.fx === null) {
            sp.target.vx -= ldx * sp.bias;
            sp.target.vy -= ldy * sp.bias;
          }
          if (sp.source.fx === null) {
            sp.source.vx += ldx * (1 - sp.bias);
            sp.source.vy += ldy * (1 - sp.bias);
          }
        }

        for (var u = 0; u < bodies.length; u++) {
          var bd = bodies[u];
          if (bd.fx !== null) continue;
          var tax = WORLD_CX + (bd.ax - WORLD_CX) * spFactor;
          var tay = WORLD_CY + (bd.ay - WORLD_CY) * spFactor;
          var effAnc = bd.anchorStrength / (1 + hf * 0.35 + hf * 0.06 * Math.max(0, (bd.degree || 1) - 2));
          bd.vx += (tax - bd.x) * effAnc * alpha;
          bd.vy += (tay - bd.y) * effAnc * alpha;
        }

        var baseHalfRad = 26 * Math.min(spFactor, 2.2);
        for (var pass = 0; pass < 2; pass++) {
          for (var i = 0; i < bodies.length; i++) {
            var ci = bodies[i];
            var ri = baseHalfRad * (1 + hf * 0.035 * Math.max(0, (ci.degree || 1) - 2));
            for (var j = i + 1; j < bodies.length; j++) {
              var cj = bodies[j];
              var rj = baseHalfRad * (1 + hf * 0.035 * Math.max(0, (cj.degree || 1) - 2));
              var minSep = ri + rj;
              var cdx = (cj.x + cj.vx) - (ci.x + ci.vx);
              var cdy = (cj.y + cj.vy) - (ci.y + ci.vy);
              var cd2 = cdx * cdx + cdy * cdy;
              if (cd2 >= minSep * minSep) continue;
              if (cd2 < 0.01) { cdx = 0.5; cdy = 0.5; cd2 = 0.5; }
              var cd = Math.sqrt(cd2);
              var ov = ((minSep - cd) / cd) * 0.42;
              var ox = cdx * ov;
              var oy = cdy * ov;
              if (ci.fx === null && cj.fx === null) {
                ci.vx -= ox; ci.vy -= oy;
                cj.vx += ox; cj.vy += oy;
              } else if (ci.fx !== null && cj.fx === null) {
                cj.vx += ox * 2; cj.vy += oy * 2;
              } else if (cj.fx !== null && ci.fx === null) {
                ci.vx -= ox * 2; ci.vy -= oy * 2;
              }
            }
          }
        }

        for (var m = 0; m < bodies.length; m++) {
          var mb = bodies[m];
          if (mb.fx !== null && mb.fy !== null) {
            mb.x = mb.fx;
            mb.y = mb.fy;
            mb.vx = 0;
            mb.vy = 0;
            continue;
          }
          mb.vx *= VEL_RETENTION;
          mb.vy *= VEL_RETENTION;
          var msp = Math.sqrt(mb.vx * mb.vx + mb.vy * mb.vy);
          if (msp > 28) { mb.vx = (mb.vx / msp) * 28; mb.vy = (mb.vy / msp) * 28; }
          mb.x += mb.vx;
          mb.y += mb.vy;
        }
      }

      function syncGraphNodesFromBodies(collapsedCounts) {
        graphNodes = [];
        graphNodeMap = {};
        for (var m = 0; m < simBodies.length; m++) {
          var item = simBodies[m];
          var hasExpSub = false;
          if (item.dto.subtypes) {
            for (var sIdx = 0; sIdx < item.dto.subtypes.length; sIdx++) {
              if (simByKind[item.dto.subtypes[sIdx]]) { hasExpSub = true; break; }
            }
          }
          var gn = {
            kind: item.dto.kind,
            x: item.x,
            y: item.y,
            ax: item.ax,
            ay: item.ay,
            radius: 25,
            fill: item.style.fill,
            glyph: item.style.glyph,
            isAbstract: Boolean(item.dto.isAbstract),
            extendsKind: item.dto.extends || null,
            collapsedCount: collapsedCounts && collapsedCounts[item.dto.kind] ? collapsedCounts[item.dto.kind] : 0,
            hasExpandedSubtypes: hasExpSub
          };
          graphNodes.push(gn);
          graphNodeMap[gn.kind] = gn;
        }
      }

      var lastCollapsedCounts = {};

      function syncSimulation(visibleDtos, redirectMap, collapsedCounts) {
        lastCollapsedCounts = collapsedCounts || {};
        var isInitial = simBodies.length === 0;
        var prevByKind = simByKind;
        var seeds = computeSeedMap(visibleDtos);
        var nextBodies = [];
        var nextByKind = {};
        var kindList = [];

        for (var i = 0; i < visibleDtos.length; i++) {
          var dto = visibleDtos[i];
          kindList.push(dto.kind);
          var seed = seeds[dto.kind];
          var existing = prevByKind[dto.kind];
          if (existing) {
            existing.dto = dto;
            existing.style = seed.style;
            existing.ax = seed.ax;
            existing.ay = seed.ay;
            existing.anchorStrength = seed.anchorStrength;
            nextBodies.push(existing);
            nextByKind[dto.kind] = existing;
            continue;
          }
          var sax = WORLD_CX + (seed.ax - WORLD_CX) * spacingFactor;
          var say = WORLD_CY + (seed.ay - WORLD_CY) * spacingFactor;
          var sx = sax;
          var sy = say;
          if (!isInitial && dto.extends) {
            var pBody = prevByKind[dto.extends] || nextByKind[dto.extends];
            if (pBody) {
              sx = pBody.x + (sax - pBody.x) * 0.32;
              sy = pBody.y + (say - pBody.y) * 0.32;
            }
          }
          var nb = {
            dto: dto,
            style: seed.style,
            x: sx,
            y: sy,
            ax: seed.ax,
            ay: seed.ay,
            vx: 0,
            vy: 0,
            fx: null,
            fy: null,
            anchorStrength: seed.anchorStrength
          };
          nextBodies.push(nb);
          nextByKind[dto.kind] = nb;
        }

        simBodies = nextBodies;
        simByKind = nextByKind;
        simSprings = buildSimSprings(nextByKind, redirectMap);

        var catParts = [];
        for (var ck in activeCats) {
          if (activeCats[ck]) catParts.push(ck);
        }
        var sig = kindList.join(",") + "|" + catParts.join(",");

        if (isInitial) {
          var warmA = 1.0;
          for (var step = 0; step < 220; step++) {
            warmA += (0 - warmA) * ALPHA_DECAY;
            stepSimBodies(simBodies, simSprings, warmA, spacingFactor, hubFactor);
          }
          for (var z = 0; z < simBodies.length; z++) {
            simBodies[z].vx = 0;
            simBodies[z].vy = 0;
          }
          simAlpha = 0;
        } else if (sig !== lastSimSig && !simFrozen) {
          simAlpha = Math.max(simAlpha, 0.42);
        }
        lastSimSig = sig;
        syncGraphNodesFromBodies(lastCollapsedCounts);
      }

      function resetSimLayout() {
        for (var i = 0; i < simBodies.length; i++) {
          var b = simBodies[i];
          b.x = WORLD_CX + (b.ax - WORLD_CX) * spacingFactor;
          b.y = WORLD_CY + (b.ay - WORLD_CY) * spacingFactor;
          b.vx = 0;
          b.vy = 0;
          b.fx = null;
          b.fy = null;
        }
        var warmA = 1.0;
        for (var step = 0; step < 220; step++) {
          warmA += (0 - warmA) * ALPHA_DECAY;
          stepSimBodies(simBodies, simSprings, warmA, spacingFactor, hubFactor);
        }
        for (var z = 0; z < simBodies.length; z++) {
          simBodies[z].vx = 0;
          simBodies[z].vy = 0;
        }
        simAlpha = 0;
        simAlphaTarget = 0;
        syncGraphNodesFromBodies(lastCollapsedCounts);
      }

      function computeCapsules() {
        return [];
      }

      function computeEdges(visibleSet, redirectMap) {
        var dedup = {};
        var raw = [];
        for (var en = 0; en < SCHEMA.nodes.length; en++) {
          var ndDef = SCHEMA.nodes[en];
          if (!ndDef.extends) continue;
          if (!visibleSet[ndDef.kind] || !visibleSet[ndDef.extends]) continue;
          var extRk = ndDef.kind + "|EXTENDS|" + ndDef.extends;
          var extEntry = {
            fromKind: ndDef.kind,
            toKind: ndDef.extends,
            rel: "EXTENDS",
            category: "type_dependency",
            isExtends: true,
            priority: 1
          };
          dedup[extRk] = extEntry;
          raw.push(extEntry);
        }
        for (var r = 0; r < SCHEMA.relationships.length; r++) {
          var rd = SCHEMA.relationships[r];
          if (!activeCats[rd.category]) continue;
          for (var p = 0; p < rd.pairs.length; p++) {
            var pair = rd.pairs[p];
            var effFrom = (redirectMap && redirectMap[pair.fromKind]) || pair.fromKind;
            var effTo = (redirectMap && redirectMap[pair.toKind]) || pair.toKind;
            if (!visibleSet[effFrom] || !visibleSet[effTo]) continue;
            if (effFrom === effTo && pair.fromKind !== pair.toKind) continue;
            var prio = 1;
            if (rd.rel === "DECLARED_IN" || rd.rel === "BELONGS_TO") prio = 4;
            else if (SUB_HUBS[effFrom] || SUB_HUBS[effTo]) prio = 3;
            else if (effFrom === "Service" || effTo === "Service" || effFrom === "Workload" || effTo === "Workload" || rd.category === "structural") prio = 2;
            var rk = effFrom + "|" + rd.rel + "|" + effTo;
            if (dedup[rk]) {
              if (prio < dedup[rk].priority) dedup[rk].priority = prio;
            } else {
              var entry = {
                fromKind: effFrom,
                toKind: effTo,
                rel: rd.rel,
                category: rd.category,
                priority: prio
              };
              dedup[rk] = entry;
              raw.push(entry);
            }
          }
        }

        var selfSlots = {};
        var groups = {};
        var edges = [];
        for (var i = 0; i < raw.length; i++) {
          var e = raw[i];
          if (e.fromKind === e.toKind) {
            var sl = selfSlots[e.fromKind] || 0;
            selfSlots[e.fromKind] = sl + 1;
            edges.push({
              fromKind: e.fromKind,
              toKind: e.toKind,
              rel: e.rel,
              category: e.category,
              isSelf: true,
              slot: sl,
              offset: 0,
              priority: e.priority
            });
          } else {
            var minK = e.fromKind < e.toKind ? e.fromKind : e.toKind;
            var maxK = e.fromKind < e.toKind ? e.toKind : e.fromKind;
            var gk = minK + "::" + maxK;
            if (!groups[gk]) groups[gk] = [];
            groups[gk].push(e);
          }
        }

        for (var gkKey in groups) {
          var grp = groups[gkKey];
          for (var g = 0; g < grp.length; g++) {
            var ge = grp[g];
            var off = 22;
            if (grp.length > 1) {
              off = (g - (grp.length - 1) / 2) * 34;
              if (Math.abs(off) < 6) off = 14;
            }
            edges.push({
              fromKind: ge.fromKind,
              toKind: ge.toKind,
              rel: ge.rel,
              category: ge.category,
              isExtends: Boolean(ge.isExtends),
              isSelf: false,
              slot: 0,
              offset: off,
              priority: ge.priority
            });
          }
        }
        edges.sort(function (a, b) { return a.priority - b.priority; });
        return edges;
      }

      var customPos = {};
      var graphNodes = [];
      var graphNodeMap = {};
      var graphCapsules = [];
      var graphEdges = [];
      var viewport = { panX: 0, panY: 0, zoom: 0.72 };
      var hasInitialFit = false;
      var autoFitEnabled = true;
      var lastFrameW = 0;
      var lastFrameH = 0;
      var dragState = null;`;
}

function renderCanvasPart() {
  return `
      function roundRect(ctx, x, y, w, h, r) {
        var rad = Math.min(r, w / 2, h / 2);
        ctx.beginPath();
        ctx.moveTo(x + rad, y);
        ctx.arcTo(x + w, y, x + w, y + h, rad);
        ctx.arcTo(x + w, y + h, x, y + h, rad);
        ctx.arcTo(x, y + h, x, y, rad);
        ctx.arcTo(x, y, x + w, y, rad);
        ctx.closePath();
      }

      function drawGlyph(ctx, glyph, x, y) {
        ctx.save();
        ctx.translate(x, y);
        ctx.strokeStyle = PAL.inkBright;
        ctx.fillStyle = PAL.inkBright;
        ctx.lineWidth = 1.65;
        ctx.lineCap = "round";
        ctx.lineJoin = "round";

        if (glyph === "globe") {
          ctx.beginPath(); ctx.arc(0, 0, 6.8, 0, Math.PI * 2); ctx.stroke();
          ctx.beginPath(); ctx.ellipse(0, 0, 3.1, 6.8, 0, 0, Math.PI * 2); ctx.stroke();
          ctx.beginPath(); ctx.moveTo(-6.6, 0); ctx.lineTo(6.6, 0); ctx.stroke();
        } else if (glyph === "database" || glyph === "procedure") {
          ctx.beginPath();
          ctx.ellipse(0, -5, 6.5, 2.6, 0, 0, Math.PI * 2);
          ctx.stroke();
          ctx.beginPath();
          ctx.moveTo(-6.5, -5);
          ctx.lineTo(-6.5, 5);
          ctx.arc(0, 5, 6.5, Math.PI, 0, true);
          ctx.lineTo(6.5, -5);
          ctx.stroke();
        } else if (glyph === "schema") {
          roundRect(ctx, -6.5, -6, 13, 4.5, 1.5); ctx.stroke();
          roundRect(ctx, -6.5, 1.5, 5.2, 4.5, 1.2); ctx.stroke();
          roundRect(ctx, 1.3, 1.5, 5.2, 4.5, 1.2); ctx.stroke();
          ctx.beginPath();
          ctx.moveTo(0, -1.5); ctx.lineTo(0, 0);
          ctx.moveTo(-3.9, 0); ctx.lineTo(3.9, 0);
          ctx.moveTo(-3.9, 0); ctx.lineTo(-3.9, 1.5);
          ctx.moveTo(3.9, 0); ctx.lineTo(3.9, 1.5);
          ctx.stroke();
        } else if (glyph === "layers") {
          ctx.beginPath();
          ctx.moveTo(0, -6.5); ctx.lineTo(7, -3); ctx.lineTo(0, 0.5); ctx.lineTo(-7, -3);
          ctx.closePath(); ctx.stroke();
          ctx.beginPath();
          ctx.moveTo(-7, 0.8); ctx.lineTo(0, 4.2); ctx.lineTo(7, 0.8);
          ctx.moveTo(-7, 4.2); ctx.lineTo(0, 7.2); ctx.lineTo(7, 4.2);
          ctx.stroke();
        } else if (glyph === "table") {
          roundRect(ctx, -7, -6, 14, 12, 2);
          ctx.stroke();
          ctx.beginPath();
          ctx.moveTo(-7, -1.5); ctx.lineTo(7, -1.5);
          ctx.moveTo(-1.5, -1.5); ctx.lineTo(-1.5, 6);
          ctx.stroke();
        } else if (glyph === "query") {
          ctx.beginPath();
          ctx.arc(-1.5, -1.5, 5, 0, Math.PI * 2);
          ctx.moveTo(2.2, 2.2); ctx.lineTo(6.8, 6.8);
          ctx.stroke();
        } else if (glyph === "endpoint") {
          ctx.beginPath(); ctx.arc(0, 0, 2.2, 0, Math.PI * 2); ctx.fill();
          ctx.beginPath(); ctx.arc(0, 0, 5.5, -Math.PI * 0.7, Math.PI * 0.7); ctx.stroke();
          ctx.beginPath(); ctx.arc(0, 0, 5.5, Math.PI * 0.3, Math.PI * 1.7); ctx.stroke();
        } else if (glyph === "topic") {
          roundRect(ctx, -7, -5, 14, 10, 2); ctx.stroke();
          ctx.beginPath(); ctx.moveTo(-6.5, -4.2); ctx.lineTo(0, 1); ctx.lineTo(6.5, -4.2); ctx.stroke();
        } else if (glyph === "cloud" || glyph === "cloudUp") {
          ctx.beginPath();
          ctx.arc(-2.5, 1, 3.8, Math.PI * 0.5, Math.PI * 1.5);
          ctx.arc(1, -1.5, 4.2, Math.PI, Math.PI * 1.95);
          ctx.arc(4.2, 1.5, 3.2, Math.PI * 1.5, Math.PI * 0.5);
          ctx.closePath(); ctx.stroke();
        } else if (glyph === "function") {
          ctx.beginPath();
          ctx.moveTo(2.5, -6.5); ctx.quadraticCurveTo(-1, -6.5, -1, -2); ctx.lineTo(-1, 6.5);
          ctx.moveTo(-4.5, -1); ctx.lineTo(3, -1);
          ctx.stroke();
        } else if (glyph === "type") {
          ctx.beginPath();
          ctx.moveTo(0, -7); ctx.lineTo(6.5, -3.5); ctx.lineTo(6.5, 3.5);
          ctx.lineTo(0, 7); ctx.lineTo(-6.5, 3.5); ctx.lineTo(-6.5, -3.5);
          ctx.closePath(); ctx.stroke();
        } else if (glyph === "param" || glyph === "arg" || glyph === "literal") {
          ctx.beginPath();
          ctx.moveTo(-4, -5.5); ctx.lineTo(-7, 0); ctx.lineTo(-4, 5.5);
          ctx.moveTo(4, -5.5); ctx.lineTo(7, 0); ctx.lineTo(4, 5.5);
          if (glyph !== "param") { ctx.moveTo(-1.5, 0); ctx.lineTo(1.5, 0); }
          ctx.stroke();
        } else if (glyph === "folder") {
          ctx.beginPath();
          ctx.moveTo(-7, -5); ctx.lineTo(-2, -5); ctx.lineTo(0, -3);
          ctx.lineTo(7, -3); ctx.lineTo(7, 5.5); ctx.lineTo(-7, 5.5);
          ctx.closePath(); ctx.stroke();
        } else if (glyph === "file") {
          ctx.beginPath();
          ctx.moveTo(-5, -6.5); ctx.lineTo(2, -6.5); ctx.lineTo(5.5, -3);
          ctx.lineTo(5.5, 6.5); ctx.lineTo(-5, 6.5);
          ctx.closePath(); ctx.stroke();
        } else if (glyph === "package" || glyph === "project") {
          roundRect(ctx, -6.5, -5.5, 13, 11, 2); ctx.stroke();
          ctx.beginPath(); ctx.moveTo(-6.5, -1.5); ctx.lineTo(6.5, -1.5); ctx.stroke();
        } else if (glyph === "zap") {
          ctx.beginPath();
          ctx.moveTo(1, -7); ctx.lineTo(-5, 0.5); ctx.lineTo(0, 0.5);
          ctx.lineTo(-1, 7); ctx.lineTo(5, -0.5); ctx.lineTo(0, -0.5);
          ctx.closePath(); ctx.stroke();
        } else {
          roundRect(ctx, -6.5, -5.5, 13, 11, 2.5); ctx.stroke();
          ctx.beginPath(); ctx.moveTo(-3.5, 0); ctx.lineTo(3.5, 0); ctx.stroke();
        }
        ctx.restore();
      }

      function drawArrow(ctx, tx, ty, ang, color, sz) {
        ctx.save();
        ctx.translate(tx, ty);
        ctx.rotate(ang);
        ctx.fillStyle = color;
        ctx.beginPath();
        ctx.moveTo(0, 0);
        ctx.lineTo(-sz, -sz * 0.54);
        ctx.lineTo(-sz * 0.78, 0);
        ctx.lineTo(-sz, sz * 0.54);
        ctx.closePath();
        ctx.fill();
        ctx.restore();
      }

      function drawHollowArrow(ctx, tx, ty, ang, color, sz) {
        ctx.save();
        ctx.translate(tx, ty);
        ctx.rotate(ang);
        ctx.fillStyle = PAL.canvasBg;
        ctx.strokeStyle = color;
        ctx.lineWidth = 1.5;
        ctx.lineJoin = "round";
        ctx.beginPath();
        ctx.moveTo(0, 0);
        ctx.lineTo(-sz, -sz * 0.58);
        ctx.lineTo(-sz, sz * 0.58);
        ctx.closePath();
        ctx.fill();
        ctx.stroke();
        ctx.restore();
      }

      function fitGraph() {
        var frame = document.getElementById("ont-canvas-frame");
        if (!frame || graphNodes.length === 0) return;
        var w = frame.clientWidth || 760;
        var h = frame.clientHeight || 640;
        var overlay = document.getElementById("ont-filter-overlay");
        var topInset = 0;
        if (overlay && getComputedStyle(overlay).position === "absolute") {
          topInset = (overlay.offsetHeight || 30) + 14;
        }
        var availH = Math.max(160, h - topInset);
        var minX = 1e9, minY = 1e9, maxX = -1e9, maxY = -1e9;
        for (var i = 0; i < graphNodes.length; i++) {
          var n = graphNodes[i];
          var rx1 = n.x, ry1 = n.y;
          if (simAlpha > 0.05 && typeof n.ax === "number") {
            var tax = WORLD_CX + (n.ax - WORLD_CX) * spacingFactor;
            var tay = WORLD_CY + (n.ay - WORLD_CY) * spacingFactor;
            if (tax - 68 < minX) minX = tax - 68;
            if (tay - 74 < minY) minY = tay - 74;
            if (tax + 68 > maxX) maxX = tax + 68;
            if (tay + 88 > maxY) maxY = tay + 88;
          }
          if (rx1 - 68 < minX) minX = rx1 - 68;
          if (ry1 - 74 < minY) minY = ry1 - 74;
          if (rx1 + 68 > maxX) maxX = rx1 + 68;
          if (ry1 + 108 > maxY) maxY = ry1 + 108;
        }
        var bw = Math.max(200, maxX - minX);
        var bh = Math.max(200, maxY - minY);
        var zm = Math.max(0.22, Math.min(1.85, Math.min((w - 40) / bw, (availH - 36) / bh)));
        viewport.zoom = zm;
        viewport.panX = w / 2 - ((minX + maxX) / 2) * zm;
        viewport.panY = topInset + availH / 2 - ((minY + maxY) / 2) * zm;
      }

      function hitCapsule(ox, oy) {
        var wx = (ox - viewport.panX) / viewport.zoom;
        var wy = (oy - viewport.panY) / viewport.zoom;
        for (var i = graphNodes.length - 1; i >= 0; i--) {
          var n = graphNodes[i];
          var hasTog = Boolean((n.collapsedCount && n.collapsedCount > 0) || n.hasExpandedSubtypes);
          if (!hasTog) continue;
          var px = n.x - n.radius * 0.78 - 14;
          var py = n.y - n.radius * 0.74 - 9;
          if (wx >= px - 4 && wx <= px + 34 && wy >= py - 4 && wy <= py + 22) return n.kind;
        }
        return null;
      }

      function hitNode(ox, oy) {
        var wx = (ox - viewport.panX) / viewport.zoom;
        var wy = (oy - viewport.panY) / viewport.zoom;
        for (var i = graphNodes.length - 1; i >= 0; i--) {
          var n = graphNodes[i];
          var dx = wx - n.x;
          var dy = wy - n.y;
          if (dx * dx + dy * dy <= 32 * 32) return n;
        }
        return null;
      }

      function drawGraph() {
        if (viewMode !== "graph") return;
        var canvas = document.getElementById("ont-graph-canvas");
        var frame = document.getElementById("ont-canvas-frame");
        if (!canvas || !frame) return;

        var w = frame.clientWidth || 760;
        var h = frame.clientHeight || 640;
        if (!hasInitialFit && w > 80 && graphNodes.length > 0) {
          hasInitialFit = true;
          fitGraph();
        }

        var dpr = window.devicePixelRatio || 1;
        canvas.width = Math.max(1, Math.floor(w * dpr));
        canvas.height = Math.max(1, Math.floor(h * dpr));
        var ctx = canvas.getContext("2d");
        if (!ctx) return;

        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
        ctx.fillStyle = PAL.canvasBg;
        ctx.fillRect(0, 0, w, h);

        ctx.fillStyle = PAL.gridDot;
        var step = 32;
        var ox = ((viewport.panX % step) + step) % step;
        var oy = ((viewport.panY % step) + step) % step;
        for (var gx = ox; gx < w; gx += step) {
          for (var gy = oy; gy < h; gy += step) {
            ctx.fillRect(gx, gy, 1.4, 1.4);
          }
        }

        var focus = focusedKind;
        var focusLineage = {};
        if (focus) {
          focusLineage[focus] = true;
          var curF = graphNodeMap[focus];
          var fGuard = {};
          while (curF && curF.extendsKind && !fGuard[curF.extendsKind]) {
            fGuard[curF.extendsKind] = true;
            focusLineage[curF.extendsKind] = true;
            curF = graphNodeMap[curF.extendsKind];
          }
        }
        var peers = {};
        for (var fl in focusLineage) peers[fl] = true;
        if (focus) {
          for (var pe = 0; pe < graphEdges.length; pe++) {
            if (focusLineage[graphEdges[pe].fromKind]) peers[graphEdges[pe].toKind] = true;
            if (focusLineage[graphEdges[pe].toKind]) peers[graphEdges[pe].fromKind] = true;
          }
        }

        ctx.save();
        ctx.translate(viewport.panX, viewport.panY);
        ctx.scale(viewport.zoom, viewport.zoom);

        var pills = [];
        for (var eIdx = 0; eIdx < graphEdges.length; eIdx++) {
          var ed = graphEdges[eIdx];
          var src = graphNodeMap[ed.fromKind];
          var dst = graphNodeMap[ed.toKind];
          if (!src || !dst) continue;

          var inc = Boolean(focus && (focusLineage[ed.fromKind] || focusLineage[ed.toKind]));
          var dim = Boolean(focus && !inc);
          var stroke = dim
            ? PAL.edgeDim
            : ed.isExtends
              ? (inc ? PAL.capHoverBorder : PAL.abstractRing)
              : inc
                ? (ACTIVE_EDGE_COLORS[ed.category] || PAL.edgeControl)
                : PAL.edgeIdle;

          ctx.strokeStyle = stroke;
          ctx.lineWidth = inc ? 1.9 : (ed.isExtends ? 1.5 : 1.25);

          if (ed.isSelf) {
            var baseA = -Math.PI * 0.64 - ed.slot * 0.55;
            var sA = baseA - 0.42;
            var eA = baseA + 0.42;
            var r = src.radius;
            var sx = src.x + Math.cos(sA) * r;
            var sy = src.y + Math.sin(sA) * r;
            var ex = src.x + Math.cos(eA) * (r + 3);
            var ey = src.y + Math.sin(eA) * (r + 3);
            var reach = r + 42 + ed.slot * 16;
            var c1x = src.x + Math.cos(sA - 0.18) * reach;
            var c1y = src.y + Math.sin(sA - 0.18) * reach;
            var c2x = src.x + Math.cos(eA + 0.18) * reach;
            var c2y = src.y + Math.sin(eA + 0.18) * reach;
            ctx.beginPath();
            ctx.moveTo(sx, sy);
            ctx.bezierCurveTo(c1x, c1y, c2x, c2y, ex, ey);
            ctx.stroke();
            if (!dim) {
              drawArrow(ctx, ex, ey, Math.atan2(ey - c2y, ex - c2x), stroke, 7);
              pills.push({
                mx: 0.125 * sx + 0.375 * c1x + 0.375 * c2x + 0.125 * ex,
                my: 0.125 * sy + 0.375 * c1y + 0.375 * c2y + 0.125 * ey,
                text: ed.rel,
                active: inc,
                prio: inc ? 0 : ed.priority
              });
            }
            continue;
          }

          var minF = ed.fromKind < ed.toKind;
          var ax = minF ? src.x : dst.x;
          var ay = minF ? src.y : dst.y;
          var bx = minF ? dst.x : src.x;
          var by = minF ? dst.y : src.y;
          var cdx = bx - ax;
          var cdy = by - ay;
          var cLen = Math.sqrt(cdx * cdx + cdy * cdy) || 1;
          var cpx = (src.x + dst.x) / 2 + (-cdy / cLen) * ed.offset;
          var cpy = (src.y + dst.y) / 2 + (cdx / cLen) * ed.offset;

          var svx = cpx - src.x, svy = cpy - src.y;
          var sL = Math.sqrt(svx * svx + svy * svy) || 1;
          var psx = src.x + (svx / sL) * src.radius;
          var psy = src.y + (svy / sL) * src.radius;

          var evx = dst.x - cpx, evy = dst.y - cpy;
          var eL = Math.sqrt(evx * evx + evy * evy) || 1;
          var ptx = dst.x - (evx / eL) * (dst.radius + (ed.isExtends ? 4 : 3));
          var pty = dst.y - (evy / eL) * (dst.radius + (ed.isExtends ? 4 : 3));

          ctx.save();
          if (ed.isExtends) ctx.setLineDash([5, 4]);
          ctx.beginPath();
          ctx.moveTo(psx, psy);
          ctx.quadraticCurveTo(cpx, cpy, ptx, pty);
          ctx.stroke();
          ctx.restore();

          if (!dim) {
            if (ed.isExtends) {
              drawHollowArrow(ctx, ptx, pty, Math.atan2(evy, evx), stroke, inc ? 9.5 : 8.4);
            } else {
              drawArrow(ctx, ptx, pty, Math.atan2(evy, evx), stroke, inc ? 7.8 : 6.6);
            }
            pills.push({
              mx: 0.25 * psx + 0.5 * cpx + 0.25 * ptx,
              my: 0.25 * psy + 0.5 * cpy + 0.25 * pty,
              text: ed.rel,
              active: inc,
              isExtends: ed.isExtends,
              prio: inc ? 0 : ed.priority
            });
          }
        }

        var boxes = [];
        for (var nIdx = 0; nIdx < graphNodes.length; nIdx++) {
          var nd = graphNodes[nIdx];
          var isSel = nd.kind === focusedKind;
          var isHov = nd.kind === hoveredKind;
          var isDim = Boolean(focus && !peers[nd.kind]);

          var scx = nd.x * viewport.zoom + viewport.panX;
          var scy = nd.y * viewport.zoom + viewport.panY;
          var scr = (nd.radius + 14) * viewport.zoom;
          boxes.push({ minX: scx - scr, minY: scy - scr, maxX: scx + scr, maxY: scy + scr + 18 * viewport.zoom });

          ctx.save();
          if (isDim) ctx.globalAlpha = 0.34;
          if (isSel || isHov) {
            ctx.beginPath();
            ctx.arc(nd.x, nd.y, nd.radius + 6.5, 0, Math.PI * 2);
            ctx.fillStyle = isSel ? PAL.nodeHalo : PAL.nodeHover;
            ctx.fill();
          }

          if (nd.isAbstract) {
            ctx.save();
            ctx.beginPath();
            ctx.arc(nd.x, nd.y, nd.radius + 4, 0, Math.PI * 2);
            ctx.setLineDash([3.5, 3]);
            ctx.lineWidth = 1.3;
            ctx.strokeStyle = PAL.abstractRing;
            ctx.stroke();
            ctx.restore();
          }

          ctx.beginPath();
          ctx.arc(nd.x, nd.y, nd.radius, 0, Math.PI * 2);
          ctx.fillStyle = nd.fill;
          ctx.fill();
          ctx.lineWidth = isSel ? 2.4 : 1.7;
          ctx.strokeStyle = isSel ? PAL.edgeControl : PAL.nodeRing;
          ctx.stroke();

          drawGlyph(ctx, nd.glyph, nd.x, nd.y);

          if ((nd.collapsedCount && nd.collapsedCount > 0) || nd.hasExpandedSubtypes) {
            var cLbl = nd.collapsedCount && nd.collapsedCount > 0 ? "+" + nd.collapsedCount : "\u2212";
            ctx.font = '400 10px "Atkinson Hyperlegible Mono", ui-monospace, monospace';
            var cpw = Math.max(22, ctx.measureText(cLbl).width + 10);
            var cph = 17;
            var cpx2 = nd.x - nd.radius * 0.78 - cpw / 2;
            var cpy2 = nd.y - nd.radius * 0.74 - cph / 2;
            var isExpHov = hoveredCapsule === nd.kind;
            roundRect(ctx, cpx2, cpy2, cpw, cph, 8.5);
            ctx.fillStyle = isExpHov ? PAL.capHeadHoverBg : PAL.capHeadBg;
            ctx.fill();
            ctx.lineWidth = 1.2;
            ctx.strokeStyle = isExpHov ? PAL.capHoverBorder : PAL.capInnerBorder;
            ctx.stroke();
            ctx.fillStyle = PAL.inkBright;
            ctx.textAlign = "center";
            ctx.textBaseline = "middle";
            ctx.fillText(cLbl, cpx2 + cpw / 2, cpy2 + cph / 2 + 0.5);
          }

          ctx.font = '400 12px "Host Grotesk", system-ui, sans-serif';
          ctx.fillStyle = isDim ? PAL.inkDim : PAL.inkBright;
          ctx.textAlign = "center";
          ctx.textBaseline = "top";
          ctx.fillText(nd.kind, nd.x, nd.y + nd.radius + 7);
          ctx.restore();
        }

        ctx.restore();

        if (viewport.zoom >= 0.42) {
          pills.sort(function (a, b) { return a.prio - b.prio; });
          ctx.font = '400 9.5px "Atkinson Hyperlegible Mono", ui-monospace, monospace';
          ctx.textAlign = "center";
          ctx.textBaseline = "middle";
          for (var pIdx = 0; pIdx < pills.length; pIdx++) {
            var pl = pills[pIdx];
            var px = pl.mx * viewport.zoom + viewport.panX;
            var py = pl.my * viewport.zoom + viewport.panY;
            if (px < -40 || px > w + 40 || py < -20 || py > h + 20) continue;
            var pw = ctx.measureText(pl.text).width + 11;
            var ph = 16;
            var box = { minX: px - pw / 2 - 3, minY: py - ph / 2 - 2, maxX: px + pw / 2 + 3, maxY: py + ph / 2 + 2 };
            var hit = false;
            if (!pl.active) {
              for (var bIdx = 0; bIdx < boxes.length; bIdx++) {
                var ob = boxes[bIdx];
                if (!(box.maxX < ob.minX || box.minX > ob.maxX || box.maxY < ob.minY || box.minY > ob.maxY)) {
                  hit = true;
                  break;
                }
              }
            }
            if (hit) continue;
            boxes.push(box);
            roundRect(ctx, px - pw / 2, py - ph / 2, pw, ph, 5);
            ctx.fillStyle = PAL.pillBg;
            ctx.fill();
            ctx.lineWidth = 1;
            ctx.strokeStyle = pl.active ? PAL.pillBorderActive : (pl.isExtends ? PAL.capInnerBorder : PAL.pillBorder);
            ctx.stroke();
            ctx.fillStyle = pl.active ? PAL.inkBright : (pl.isExtends ? PAL.capHoverBorder : PAL.inkSoft);
            ctx.fillText(pl.text, px, py + 0.5);
          }
        }
      }`;
}

function renderUiControllerPart() {
  return `
      function computeBadges(kind) {
        var badges = {};
        var conns = [];
        var seen = {};
        var node = nodeMap[kind];
        if (!node) return { badges: badges, conns: conns };
        var outs = node.inheritedOutEdges ? node.outEdges.concat(node.inheritedOutEdges) : node.outEdges;
        for (var i = 0; i < outs.length; i++) {
          var oe = outs[i];
          if (!activeCats[oe.category]) continue;
          var ok = "out|" + oe.rel + "|" + oe.toKind;
          if (seen[ok]) continue;
          seen[ok] = true;
          if (!badges[oe.toKind]) badges[oe.toKind] = [];
          badges[oe.toKind].push({ dir: "out", rel: oe.rel, cat: oe.category });
          conns.push({ peer: oe.toKind, rel: oe.rel, dir: "out", cat: oe.category });
        }
        var ins = node.inheritedInEdges ? node.inEdges.concat(node.inheritedInEdges) : node.inEdges;
        for (var j = 0; j < ins.length; j++) {
          var ie = ins[j];
          if (!activeCats[ie.category]) continue;
          var ik = "in|" + ie.rel + "|" + ie.fromKind;
          if (seen[ik]) continue;
          seen[ik] = true;
          if (!badges[ie.fromKind]) badges[ie.fromKind] = [];
          badges[ie.fromKind].push({ dir: "in", rel: ie.rel, cat: ie.category });
          conns.push({ peer: ie.fromKind, rel: ie.rel, dir: "in", cat: ie.category });
        }
        return { badges: badges, conns: conns };
      }

      function renderInspector(node, q) {
        if (!node) return;
        var pill = document.getElementById("insp-layer-pill");
        pill.textContent = "Layer " + node.layerId;
        pill.className = "ont-layer-pill ont-layer-pill--l" + node.layerId;

        var absPill = document.getElementById("insp-abstract-pill");
        if (absPill) absPill.hidden = !node.isAbstract;

        var linEl = document.getElementById("insp-lineage");
        if (linEl) {
          var linHtml = "";
          if (node.ancestors && node.ancestors.length > 0) {
            linHtml += '<div class="ont-lineage-row"><span class="ont-layer-sub">Extends</span>';
            for (var ai = 0; ai < node.ancestors.length; ai++) {
              var anc = node.ancestors[ai];
              linHtml += '<button type="button" class="ont-jump-btn" data-select-kind="' + escHtml(anc) + '">:' + escHtml(anc) + '</button>';
            }
            linHtml += '</div>';
          }
          if (node.subtypes && node.subtypes.length > 0) {
            linHtml += '<div class="ont-lineage-row"><span class="ont-layer-sub">Subtypes (' + node.subtypes.length + ')</span>';
            for (var si = 0; si < node.subtypes.length; si++) {
              var sub = node.subtypes[si];
              linHtml += '<button type="button" class="ont-jump-btn" data-select-kind="' + escHtml(sub) + '">:' + escHtml(sub) + '</button>';
            }
            linHtml += '</div>';
          }
          linEl.innerHTML = linHtml;
          linEl.hidden = !linHtml;
        }

        document.getElementById("insp-plural").textContent = node.pluralLabel;
        document.getElementById("insp-title").textContent = ":" + node.kind;
        document.getElementById("insp-purpose").textContent = node.purpose;
        document.getElementById("insp-urn").textContent = node.idScheme || "n/a";
        document.getElementById("insp-props-heading").textContent =
          "Collected properties (" + node.properties.length + " + " + SCHEMA.commonProperties.length + " base)";

        var propsHtml = "";
        for (var i = 0; i < node.properties.length; i++) {
          var p = node.properties[i];
          var isMatch = q && (p.name.toLowerCase().indexOf(q) !== -1 || p.description.toLowerCase().indexOf(q) !== -1);
          var inhProp = p.inheritedFrom ? '<code class="ont-edge-prop">via :' + escHtml(p.inheritedFrom) + '</code>' : "";
          propsHtml +=
            '<div class="ont-prop-item' + (isMatch ? " is-match" : "") + '">' +
              '<div class="ont-prop-top">' +
                '<code class="ont-prop-name">' + escHtml(p.name) + '</code>' +
                '<span class="ont-prop-right">' +
                  inhProp +
                  '<code class="ont-prop-type">' + escHtml(p.type) + '</code>' +
                '</span>' +
              '</div>' +
              '<span class="ont-prop-desc">' + escHtml(p.description) + '</span>' +
            '</div>';
        }
        document.getElementById("insp-props-list").innerHTML = propsHtml;

        var allOut = node.inheritedOutEdges ? node.outEdges.concat(node.inheritedOutEdges) : node.outEdges;
        var allIn = node.inheritedInEdges ? node.inEdges.concat(node.inheritedInEdges) : node.inEdges;
        document.getElementById("insp-rels-heading").textContent =
          "Typed relationships (" + allOut.length + " out, " + allIn.length + " in)";

        var relsHtml = "";
        for (var o = 0; o < allOut.length; o++) {
          var oe = allOut[o];
          var rMeta = relMap[oe.rel];
          var epHtml = "";
          if (rMeta && rMeta.properties && rMeta.properties.length > 1) {
            epHtml = '<div class="ont-edge-props-row">';
            for (var x = 0; x < rMeta.properties.length; x++) {
              var ep = rMeta.properties[x];
              epHtml += '<code class="ont-edge-prop" title="' + escHtml(ep.description) + '">r.' + escHtml(ep.name) + '</code>';
            }
            epHtml += '</div>';
          }
          var inhOut = oe.inheritedFrom ? '<code class="ont-edge-prop">via :' + escHtml(oe.inheritedFrom) + '</code>' : "";
          relsHtml +=
            '<div class="ont-rel-row">' +
              '<div class="ont-rel-line">' +
                '<code class="ont-edge-tag ont-edge-tag--' + escHtml(oe.category) + '">-[:' + escHtml(oe.rel) + ']-&gt;</code>' +
                '<button type="button" class="ont-jump-btn" data-select-kind="' + escHtml(oe.toKind) + '">:' + escHtml(oe.toKind) + '</button>' +
                inhOut +
              '</div>' +
              '<span class="ont-prop-desc">' + escHtml(oe.description) + '</span>' +
              epHtml +
            '</div>';
        }
        for (var m = 0; m < allIn.length; m++) {
          var ie = allIn[m];
          var inhIn = ie.inheritedFrom ? '<code class="ont-edge-prop">via :' + escHtml(ie.inheritedFrom) + '</code>' : "";
          relsHtml +=
            '<div class="ont-rel-row">' +
              '<div class="ont-rel-line">' +
                '<button type="button" class="ont-jump-btn" data-select-kind="' + escHtml(ie.fromKind) + '">:' + escHtml(ie.fromKind) + '</button>' +
                '<code class="ont-edge-tag ont-edge-tag--' + escHtml(ie.category) + '">-[:' + escHtml(ie.rel) + ']-&gt;</code>' +
                inhIn +
              '</div>' +
              '<span class="ont-prop-desc">' + escHtml(ie.description) + '</span>' +
            '</div>';
        }
        document.getElementById("insp-rels-list").innerHTML = relsHtml;
        document.getElementById("insp-cypher").textContent = node.sampleCypher;

        var cPropsLbl = document.getElementById("insp-compact-props-label");
        if (cPropsLbl) {
          cPropsLbl.textContent = "Properties (" + node.properties.length + " + " + SCHEMA.commonProperties.length + " base)";
        }
        var cPropsEl = document.getElementById("insp-compact-props");
        if (cPropsEl) {
          var cpHtml = "";
          var maxCp = Math.min(6, node.properties.length);
          for (var cpi = 0; cpi < maxCp; cpi++) {
            var cp = node.properties[cpi];
            cpHtml += '<code class="ont-edge-prop" title="' + escHtml(cp.description) + '">' + escHtml(cp.name) + ": " + escHtml(cp.type) + "</code>";
          }
          if (node.properties.length > 6) {
            cpHtml += '<code class="ont-edge-prop">+' + (node.properties.length - 6) + " more</code>";
          }
          cPropsEl.innerHTML = cpHtml;
        }

        var cRelsLbl = document.getElementById("insp-compact-rels-label");
        if (cRelsLbl) {
          cRelsLbl.textContent = "Connected entities (" + allOut.length + " out, " + allIn.length + " in)";
        }
        var cRelsEl = document.getElementById("insp-compact-rels");
        if (cRelsEl) {
          var peerMap = {};
          var peerArr = [];
          for (var poi = 0; poi < allOut.length; poi++) {
            var pkOut = allOut[poi].toKind;
            if (!peerMap[pkOut]) {
              peerMap[pkOut] = true;
              peerArr.push(pkOut);
            }
          }
          for (var pii = 0; pii < allIn.length; pii++) {
            var pkIn = allIn[pii].fromKind;
            if (!peerMap[pkIn]) {
              peerMap[pkIn] = true;
              peerArr.push(pkIn);
            }
          }
          var crHtml = "";
          var maxCr = Math.min(6, peerArr.length);
          for (var cri = 0; cri < maxCr; cri++) {
            var pKind = peerArr[cri];
            crHtml += '<button type="button" class="ont-jump-btn" data-select-kind="' + escHtml(pKind) + '">:' + escHtml(pKind) + "</button>";
          }
          if (peerArr.length > 6) {
            crHtml += '<code class="ont-edge-prop">+' + (peerArr.length - 6) + " more</code>";
          }
          cRelsEl.innerHTML = crHtml;
        }

        var inspEl = document.getElementById("entity-inspector");
        var gpEl = document.getElementById("ont-graph-panel");
        var isFsMode = Boolean(gpEl && gpEl.classList.contains("is-fullscreen"));
        if (inspEl) {
          inspEl.hidden = viewMode === "graph" && !focusedKind;
          var showFull = viewMode === "layers" || isFsMode || inspectorExpanded;
          inspEl.classList.toggle("is-compact", !showFull);
          inspEl.classList.toggle("is-expanded-view", showFull);
        }
        var toggleModeBtn = document.getElementById("insp-toggle-mode-btn");
        if (toggleModeBtn) {
          toggleModeBtn.hidden = viewMode === "layers" || isFsMode;
          toggleModeBtn.textContent = inspectorExpanded ? "Compact" : "Full";
        }
        var closeInspBtn = document.getElementById("insp-close-btn");
        if (closeInspBtn) {
          closeInspBtn.hidden = viewMode === "layers";
        }
      }

      function renderFocusStrip(conns) {
        var labelEl = document.getElementById("ont-focus-label");
        var countEl = document.getElementById("ont-focus-count");
        var linksEl = document.getElementById("ont-focus-links");
        if (!labelEl || !countEl || !linksEl) return;
        labelEl.textContent = "Active connections for :" + selectedKind;
        countEl.textContent = conns.length + " active links";
        if (conns.length === 0) {
          linksEl.innerHTML = '<span class="ont-layer-sub">Enable relationship category filters above to display connected entities.</span>';
          return;
        }
        var max = Math.min(12, conns.length);
        var html = "";
        for (var i = 0; i < max; i++) {
          var c = conns[i];
          var txt = c.dir === "out"
            ? "out: " + escHtml(c.rel) + " :" + escHtml(c.peer)
            : "in: :" + escHtml(c.peer) + " " + escHtml(c.rel);
          html += '<button type="button" class="ont-focus-chip ont-focus-chip--' + escHtml(c.cat) + '" data-select-kind="' + escHtml(c.peer) + '"><code>' + txt + '</code></button>';
        }
        linksEl.innerHTML = html;
      }

      function isNodeVisible(nd, q) {
        return Boolean(activeLayers[nd.layerId]) && matchesQuery(nd, q);
      }

      function ensureSimLoop() {
        if (simRafId !== null || simFrozen) return;
        function tickLoop() {
          simRafId = null;
          if (simFrozen || viewMode !== "graph") return;
          simAlpha += (simAlphaTarget - simAlpha) * ALPHA_DECAY;
          if (simAlpha < ALPHA_MIN && simAlphaTarget === 0) {
            simAlpha = 0;
            return;
          }
          stepSimBodies(simBodies, simSprings, simAlpha, spacingFactor, hubFactor);
          syncGraphNodesFromBodies(lastCollapsedCounts);
          drawGraph();
          simRafId = requestAnimationFrame(tickLoop);
        }
        simRafId = requestAnimationFrame(tickLoop);
      }

      function rebuildGraphModel(q) {
        var filteredDtos = [];
        for (var i = 0; i < SCHEMA.nodes.length; i++) {
          var nd = SCHEMA.nodes[i];
          if (isNodeVisible(nd, q)) filteredDtos.push(nd);
        }
        var res = resolveHierarchy(filteredDtos);
        var visDtos = res.visibleDtos;
        var visSet = {};
        var visParts = [];
        for (var v = 0; v < visDtos.length; v++) {
          visSet[visDtos[v].kind] = true;
          visParts.push(visDtos[v].kind);
        }
        var capParts = [];
        for (var hi = 0; hi < hierarchies.length; hi++) {
          var hk = hierarchies[hi].kind;
          capParts.push(hk + ":" + (expandedHierarchies[hk] ? "1" : "0"));
        }
        var visKey = visParts.join(",") + "|" + capParts.join(",");
        var changed = Boolean(lastVisKey && lastVisKey !== visKey);
        lastVisKey = visKey;
        if (focusedKind && !visSet[focusedKind]) {
          focusedKind = res.redirectMap[focusedKind] || null;
        }
        syncSimulation(visDtos, res.redirectMap, res.collapsedCounts);
        graphCapsules = computeCapsules(graphNodes);
        graphEdges = computeEdges(visSet, res.redirectMap);
        if (changed && hasInitialFit && graphNodes.length > 0) {
          autoFitEnabled = true;
          fitGraph();
        }
        if (!simFrozen && (simAlpha >= ALPHA_MIN || simAlphaTarget > 0)) {
          ensureSimLoop();
        }
      }

      function render() {
        var q = searchQuery.trim().toLowerCase();
        var curNode = nodeMap[selectedKind];
        if (!curNode || !activeLayers[curNode.layerId]) {
          for (var s = 0; s < SCHEMA.nodes.length; s++) {
            if (isNodeVisible(SCHEMA.nodes[s], q)) {
              selectedKind = SCHEMA.nodes[s].kind;
              break;
            }
          }
          if (!nodeMap[selectedKind] || !activeLayers[nodeMap[selectedKind].layerId]) {
            for (var s2 = 0; s2 < SCHEMA.nodes.length; s2++) {
              if (activeLayers[SCHEMA.nodes[s2].layerId]) {
                selectedKind = SCHEMA.nodes[s2].kind;
                break;
              }
            }
          }
        }

        var ego = computeBadges(selectedKind);
        var cards = document.querySelectorAll(".ont-node-btn");
        var catList = ["control_flow", "data_flow", "type_dependency", "structural"];

        for (var i = 0; i < cards.length; i++) {
          var btn = cards[i];
          var kind = btn.getAttribute("data-kind");
          var node = nodeMap[kind];
          if (!node) continue;
          var visible = isNodeVisible(node, q);
          btn.hidden = !visible;
          var isSel = kind === selectedKind;
          var bList = ego.badges[kind] || [];
          var isNeighbor = bList.length > 0;
          var primaryCat = isNeighbor ? bList[0].cat : "";
          btn.setAttribute("aria-selected", isSel ? "true" : "false");
          btn.classList.toggle("is-neighbor", !isSel && isNeighbor);
          btn.classList.toggle("is-dimmed", !isSel && !isNeighbor);
          for (var c = 0; c < catList.length; c++) {
            btn.classList.toggle("is-neighbor--" + catList[c], !isSel && isNeighbor && primaryCat === catList[c]);
          }
        }

        var layerBands = document.querySelectorAll(".ont-layer[data-layer-id]");
        for (var lb = 0; lb < layerBands.length; lb++) {
          var lid = Number(layerBands[lb].getAttribute("data-layer-id"));
          layerBands[lb].hidden = !activeLayers[lid];
        }

        var layerBtns = document.querySelectorAll(".ont-layer-btn");
        for (var lbi = 0; lbi < layerBtns.length; lbi++) {
          var blid = Number(layerBtns[lbi].getAttribute("data-layer"));
          layerBtns[lbi].setAttribute("aria-pressed", activeLayers[blid] ? "true" : "false");
        }

        var capGroup = document.getElementById("ont-capsule-group");
        if (capGroup) {
          capGroup.hidden = viewMode !== "graph" || !activeLayers[4];
        }
        var capBtns = document.querySelectorAll(".ont-capsule-btn");
        for (var cbi = 0; cbi < capBtns.length; cbi++) {
          var hAttr = capBtns[cbi].getAttribute("data-hierarchy");
          var isExp = Boolean(expandedHierarchies[hAttr]);
          var hMeta = nodeMap[hAttr];
          if (hMeta && hMeta.ancestors) {
            for (var hai = 0; hai < hMeta.ancestors.length; hai++) {
              if (!expandedHierarchies[hMeta.ancestors[hai]]) isExp = false;
            }
          }
          capBtns[cbi].setAttribute("aria-pressed", isExp ? "true" : "false");
        }

        var graphPanel = document.getElementById("ont-graph-panel");
        var canvasFrame = document.getElementById("ont-canvas-frame");
        var graphFoot = document.getElementById("ont-graph-foot");
        var zoomGroup = document.getElementById("ont-graph-zoom-group");
        var swimlanes = document.getElementById("ont-swimlanes");
        var wasHidden = canvasFrame ? canvasFrame.hidden : false;
        if (graphPanel) {
          graphPanel.classList.toggle("is-layers-mode", viewMode === "layers");
        }
        if (canvasFrame) canvasFrame.hidden = viewMode !== "graph";
        if (graphFoot) graphFoot.hidden = viewMode !== "graph";
        if (zoomGroup) zoomGroup.hidden = viewMode !== "graph";
        if (swimlanes) swimlanes.hidden = viewMode !== "layers";

        var showAllBtn = document.getElementById("graph-show-all");
        if (showAllBtn) {
          showAllBtn.hidden = !focusedKind;
        }

        var modeBtns = document.querySelectorAll(".ont-mode-btn");
        for (var mb = 0; mb < modeBtns.length; mb++) {
          var mAttr = modeBtns[mb].getAttribute("data-mode");
          modeBtns[mb].setAttribute("aria-pressed", mAttr === viewMode ? "true" : "false");
        }

        rebuildGraphModel(q);
        if (wasHidden && viewMode === "graph") {
          autoFitEnabled = true;
          fitGraph();
        }
        drawGraph();
        renderFocusStrip(ego.conns);
        renderInspector(nodeMap[selectedKind], q);
      }`;
}

function renderUiEventsPart() {
  return `
      function toggleHierarchy(hKind) {
        if (!hKind) return;
        var next = !expandedHierarchies[hKind];
        var hm = nodeMap[hKind];
        if (hm && hm.ancestors) {
          for (var a = 0; a < hm.ancestors.length; a++) {
            if (!expandedHierarchies[hm.ancestors[a]]) {
              next = true;
              expandedHierarchies[hm.ancestors[a]] = true;
            }
          }
        }
        expandedHierarchies[hKind] = next;
        autoFitEnabled = true;
        render();
        fitGraph();
        drawGraph();
      }

      document.addEventListener("click", function (ev) {
        var closeInsp = ev.target.closest("#insp-close-btn");
        if (closeInsp) {
          focusedKind = null;
          inspectorExpanded = false;
          render();
          return;
        }

        var toggleInspMode = ev.target.closest("#insp-toggle-mode-btn");
        if (toggleInspMode) {
          inspectorExpanded = !inspectorExpanded;
          render();
          return;
        }

        var modeBtn = ev.target.closest(".ont-mode-btn");
        if (modeBtn) {
          viewMode = modeBtn.getAttribute("data-mode") || "graph";
          render();
          return;
        }

        var capBtn = ev.target.closest(".ont-capsule-btn");
        if (capBtn) {
          toggleHierarchy(capBtn.getAttribute("data-hierarchy"));
          return;
        }

        var layerBtn = ev.target.closest(".ont-layer-btn");
        if (layerBtn) {
          var tlid = Number(layerBtn.getAttribute("data-layer"));
          activeLayers[tlid] = !activeLayers[tlid];
          if (!activeLayers[5] && !activeLayers[4] && !activeLayers[3] && !activeLayers[2] && !activeLayers[1]) {
            activeLayers = { 5: true, 4: true, 3: true, 2: true, 1: true };
          }
          render();
          return;
        }

        var card = ev.target.closest(".ont-node-btn");
        if (card) {
          selectedKind = card.getAttribute("data-kind");
          render();
          return;
        }

        var jump = ev.target.closest("[data-select-kind]");
        if (jump) {
          selectedKind = jump.getAttribute("data-select-kind");
          var jNode = nodeMap[selectedKind];
          if (jNode) {
            activeLayers[jNode.layerId] = true;
            if (jNode.ancestors) {
              for (var ja = 0; ja < jNode.ancestors.length; ja++) {
                expandedHierarchies[jNode.ancestors[ja]] = true;
              }
            }
          }
          if (viewMode === "graph") focusedKind = selectedKind;
          render();
          return;
        }

        var catBtn = ev.target.closest(".ont-cat-btn");
        if (catBtn) {
          var cat = catBtn.getAttribute("data-cat");
          activeCats[cat] = !activeCats[cat];
          catBtn.setAttribute("aria-pressed", activeCats[cat] ? "true" : "false");
          render();
          return;
        }
      });

      var canvasEl = document.getElementById("ont-graph-canvas");
      if (canvasEl) {
        canvasEl.style.cursor = "grab";
        canvasEl.addEventListener("pointerdown", function (e) {
          var rect = canvasEl.getBoundingClientRect();
          var ox = e.clientX - rect.left;
          var oy = e.clientY - rect.top;
          var hitCap = hitCapsule(ox, oy);
          if (hitCap) {
            canvasEl.setPointerCapture(e.pointerId);
            dragState = { mode: "capsule", kind: hitCap, sx: e.clientX, sy: e.clientY, ox: 0, oy: 0, moved: false };
            return;
          }
          var hit = hitNode(ox, oy);
          canvasEl.setPointerCapture(e.pointerId);
          canvasEl.style.cursor = "grabbing";
          if (hit) {
            dragState = { mode: "node", kind: hit.kind, sx: e.clientX, sy: e.clientY, ox: hit.x, oy: hit.y, moved: false };
            var sb = simByKind[hit.kind];
            if (sb) {
              sb.fx = sb.x;
              sb.fy = sb.y;
            }
            if (!simFrozen) {
              simAlphaTarget = 0.3;
              simAlpha = Math.max(simAlpha, 0.3);
              ensureSimLoop();
            }
          } else {
            dragState = { mode: "pan", kind: null, sx: e.clientX, sy: e.clientY, ox: viewport.panX, oy: viewport.panY, moved: false };
          }
        });

        canvasEl.addEventListener("pointermove", function (e) {
          var rect = canvasEl.getBoundingClientRect();
          if (dragState) {
            var dx = e.clientX - dragState.sx;
            var dy = e.clientY - dragState.sy;
            if (Math.abs(dx) > 4 || Math.abs(dy) > 4) dragState.moved = true;
            if (dragState.mode === "pan") {
              if (dragState.moved) autoFitEnabled = false;
              viewport.panX = dragState.ox + dx;
              viewport.panY = dragState.oy + dy;
              drawGraph();
            } else if (dragState.mode === "node" && dragState.kind) {
              var nx = dragState.ox + dx / viewport.zoom;
              var ny = dragState.oy + dy / viewport.zoom;
              var mb = simByKind[dragState.kind];
              if (mb) {
                mb.fx = nx;
                mb.fy = ny;
                mb.x = nx;
                mb.y = ny;
              }
              if (graphNodeMap[dragState.kind]) {
                graphNodeMap[dragState.kind].x = nx;
                graphNodeMap[dragState.kind].y = ny;
              }
              if (!simFrozen) {
                simAlpha = Math.max(simAlpha, 0.3);
                ensureSimLoop();
              }
              drawGraph();
            }
            return;
          }
          var mx = e.clientX - rect.left;
          var my = e.clientY - rect.top;
          var hCap = hitCapsule(mx, my);
          var hNode = hCap ? null : hitNode(mx, my);
          var nextHov = hNode ? hNode.kind : null;
          canvasEl.style.cursor = (hCap || nextHov) ? "pointer" : "grab";
          if (hCap !== hoveredCapsule || nextHov !== hoveredKind) {
            hoveredCapsule = hCap;
            hoveredKind = nextHov;
            drawGraph();
          }
        });

        canvasEl.addEventListener("pointerup", function (e) {
          if (canvasEl.hasPointerCapture(e.pointerId)) canvasEl.releasePointerCapture(e.pointerId);
          var d = dragState;
          dragState = null;
          canvasEl.style.cursor = (hoveredCapsule || hoveredKind) ? "pointer" : "grab";
          if (d && d.mode === "node" && d.kind) {
            simAlphaTarget = 0;
            var rb = simByKind[d.kind];
            if (rb && !simFrozen) {
              rb.fx = null;
              rb.fy = null;
            }
            if (!simFrozen) ensureSimLoop();
          }
          if (!d || d.moved) return;
          if (d.mode === "capsule" && d.kind) {
            hoveredCapsule = null;
            toggleHierarchy(d.kind);
          } else if (d.mode === "node" && d.kind) {
            selectedKind = d.kind;
            focusedKind = focusedKind === d.kind ? null : d.kind;
            render();
          } else if (focusedKind) {
            focusedKind = null;
            render();
          }
        });

        canvasEl.addEventListener("pointerleave", function () {
          if (!dragState && (hoveredKind || hoveredCapsule)) {
            hoveredKind = null;
            hoveredCapsule = null;
            drawGraph();
          }
        });

        canvasEl.addEventListener("wheel", function (e) {
          e.preventDefault();
          autoFitEnabled = false;
          var rect = canvasEl.getBoundingClientRect();
          var mx = e.clientX - rect.left;
          var my = e.clientY - rect.top;
          var factor = e.deltaY < 0 ? 1.12 : 0.89;
          var nextZ = Math.max(0.22, Math.min(2.8, viewport.zoom * factor));
          var sc = nextZ / viewport.zoom;
          viewport.zoom = nextZ;
          viewport.panX = mx - (mx - viewport.panX) * sc;
          viewport.panY = my - (my - viewport.panY) * sc;
          drawGraph();
        }, { passive: false });
      }

      function zoomBy(factor) {
        autoFitEnabled = false;
        var frame = document.getElementById("ont-canvas-frame");
        var cx = (frame ? frame.clientWidth : 760) / 2;
        var cy = (frame ? frame.clientHeight : 640) / 2;
        var nextZ = Math.max(0.22, Math.min(2.8, viewport.zoom * factor));
        var sc = nextZ / viewport.zoom;
        viewport.zoom = nextZ;
        viewport.panX = cx - (cx - viewport.panX) * sc;
        viewport.panY = cy - (cy - viewport.panY) * sc;
        drawGraph();
      }

      var zIn = document.getElementById("graph-zoom-in");
      var zOut = document.getElementById("graph-zoom-out");
      var zFit = document.getElementById("graph-zoom-fit");
      var zAll = document.getElementById("graph-show-all");
      var zFs = document.getElementById("graph-fullscreen");
      var gPanel = document.getElementById("ont-graph-panel");
      var cFrame = document.getElementById("ont-canvas-frame");

      function handleFrameResize() {
        if (!cFrame) return;
        var fw = cFrame.clientWidth || 0;
        var fh = cFrame.clientHeight || 0;
        if (fw <= 0 || fh <= 0) return;
        if (fw === lastFrameW && fh === lastFrameH) return;
        lastFrameW = fw;
        lastFrameH = fh;
        if (autoFitEnabled) {
          fitGraph();
        }
        drawGraph();
      }

      function syncFullscreenState(active) {
        if (!gPanel) return;
        autoFitEnabled = true;
        gPanel.classList.toggle("is-fullscreen", active);
        document.body.classList.toggle("has-fullscreen-graph", active);
        if (zFs) {
          zFs.setAttribute("aria-pressed", active ? "true" : "false");
          zFs.textContent = active ? "Exit" : "Expand";
        }
        render();
        fitGraph();
        drawGraph();
        requestAnimationFrame(function () {
          fitGraph();
          drawGraph();
        });
      }

      if (zFs && gPanel) {
        zFs.addEventListener("click", function () {
          var isFs = gPanel.classList.contains("is-fullscreen");
          syncFullscreenState(!isFs);
        });
      }

      document.addEventListener("keydown", function (e) {
        if (e.key === "Escape" && gPanel && gPanel.classList.contains("is-fullscreen")) {
          syncFullscreenState(false);
        }
      });

      document.addEventListener("mousedown", function (e) {
        if (gPanel && gPanel.classList.contains("is-fullscreen") && !gPanel.contains(e.target)) {
          syncFullscreenState(false);
        }
      });

      var zAir = document.getElementById("graph-air-slider");
      var zAirVal = document.getElementById("graph-air-val");
      var zHub = document.getElementById("graph-hub-slider");
      var zHubVal = document.getElementById("graph-hub-val");
      var zFreeze = document.getElementById("graph-freeze");
      if (zAir) zAir.value = String(spacingFactor);
      if (zAirVal) zAirVal.textContent = spacingFactor.toFixed(1) + "x";
      if (zHub) zHub.value = String(hubFactor);
      if (zHubVal) zHubVal.textContent = hubFactor.toFixed(1) + "x";

      if (zAir) {
        zAir.addEventListener("input", function () {
          var nextSp = parseFloat(zAir.value) || 1.0;
          spacingFactor = Math.max(0.1, Math.min(2.4, nextSp));
          if (zAirVal) zAirVal.textContent = spacingFactor.toFixed(1) + "x";
          try { localStorage.setItem("ce_ontology_air", String(spacingFactor)); } catch (_e) {}
          if (!simFrozen) {
            simAlpha = Math.max(simAlpha, 0.38);
            ensureSimLoop();
          }
        });
      }

      if (zHub) {
        zHub.addEventListener("input", function () {
          var parsedHb = parseFloat(zHub.value);
          var nextHb = isNaN(parsedHb) ? 1.0 : parsedHb;
          hubFactor = Math.max(0.0, Math.min(5.0, nextHb));
          if (zHubVal) zHubVal.textContent = hubFactor.toFixed(1) + "x";
          try { localStorage.setItem("ce_ontology_hub", String(hubFactor)); } catch (_e) {}
          if (!simFrozen) {
            simAlpha = Math.max(simAlpha, 0.42);
            ensureSimLoop();
          }
        });
      }

      if (zFreeze) {
        zFreeze.addEventListener("click", function () {
          simFrozen = !simFrozen;
          zFreeze.setAttribute("aria-pressed", simFrozen ? "true" : "false");
          zFreeze.textContent = simFrozen ? "Frozen" : "Freeze";
          if (simFrozen) {
            simAlpha = 0;
            simAlphaTarget = 0;
            for (var fi = 0; fi < simBodies.length; fi++) {
              simBodies[fi].vx = 0;
              simBodies[fi].vy = 0;
            }
          } else {
            for (var ufi = 0; ufi < simBodies.length; ufi++) {
              simBodies[ufi].fx = null;
              simBodies[ufi].fy = null;
            }
            simAlpha = Math.max(simAlpha, 0.28);
            ensureSimLoop();
          }
        });
      }

      if (zIn) zIn.addEventListener("click", function () { zoomBy(1.18); });
      if (zOut) zOut.addEventListener("click", function () { zoomBy(0.84); });
      if (zFit) zFit.addEventListener("click", function () {
        focusedKind = null;
        autoFitEnabled = true;
        resetSimLayout();
        render();
        fitGraph();
        drawGraph();
      });
      if (zAll) zAll.addEventListener("click", function () {
        focusedKind = null;
        render();
      });

      if (cFrame && typeof ResizeObserver !== "undefined") {
        var frameObserver = new ResizeObserver(function () {
          handleFrameResize();
        });
        frameObserver.observe(cFrame);
      }

      window.addEventListener("resize", function () {
        handleFrameResize();
        drawGraph();
      });

      var searchEl = document.getElementById("ont-search");
      if (searchEl) {
        searchEl.addEventListener("input", function () {
          searchQuery = searchEl.value || "";
          render();
        });
      }

      var copyBtn = document.getElementById("copy-cypher-btn");
      if (copyBtn) {
        copyBtn.addEventListener("click", function () {
          var text = document.getElementById("insp-cypher").textContent;
          if (navigator.clipboard) {
            navigator.clipboard.writeText(text).then(function () {
              var orig = copyBtn.textContent;
              copyBtn.textContent = "Copied";
              setTimeout(function () { copyBtn.textContent = orig; }, 1500);
            });
          }
        });
      }

      render();
    })();
  </script>`;
}

export function renderOntologyClientScript(safeJsonInline) {
  return [
    renderLayoutPart(safeJsonInline),
    renderCanvasPart(),
    renderUiControllerPart(),
    renderUiEventsPart(),
  ].join('\n');
}
