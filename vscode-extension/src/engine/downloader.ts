import * as fs from 'fs';
import * as https from 'https';
import * as http from 'http';
import { URL } from 'url';
import { DownloadProgressCallback, LogOutput } from './types';
import { compareSemver, getEngineConfigFromExtensionVersion, matchesEnginePattern } from './semver';
import { resolveTargetAsset } from './platform';

export const GITHUB_REPO = 'vmikhailov/code-explorer';

/**
 * Fetches releases list from GitHub API.
 */
export function fetchGitHubReleases(repo = GITHUB_REPO): Promise<any[]> {
  return new Promise((resolve, reject) => {
    const options: https.RequestOptions = {
      hostname: 'api.github.com',
      path: `/repos/${repo}/releases?per_page=30`,
      method: 'GET',
      headers: {
        'User-Agent': 'CodeExplorer-VSCode-Extension',
        'Accept': 'application/vnd.github.v3+json',
      },
    };

    const req = https.request(options, (res) => {
      if (res.statusCode && (res.statusCode < 200 || res.statusCode >= 300)) {
        return reject(new Error(`GitHub API returned status ${res.statusCode}`));
      }

      let data = '';
      res.on('data', (chunk) => {
        data += chunk;
      });
      res.on('end', () => {
        try {
          const parsed = JSON.parse(data);
          if (Array.isArray(parsed)) {
            resolve(parsed);
          } else {
            reject(new Error('Unexpected GitHub API response structure'));
          }
        } catch (e) {
          reject(e);
        }
      });
    });

    req.on('error', reject);
    req.setTimeout(6000, () => {
      req.destroy();
      reject(new Error('GitHub API request timed out'));
    });
    req.end();
  });
}

/**
 * Downloads a file from a URL, following 301/302 redirects.
 */
export function downloadFileWithRedirects(
  downloadUrl: string,
  destPath: string,
  onProgress?: DownloadProgressCallback
): Promise<void> {
  return new Promise((resolve, reject) => {
    const parsed = new URL(downloadUrl);
    const client = parsed.protocol === 'https:' ? https : http;

    const request = client.get(
      downloadUrl,
      {
        headers: {
          'User-Agent': 'CodeExplorer-VSCode-Extension',
          'Accept': 'application/octet-stream',
        },
      },
      (response) => {
        // Follow redirects
        if (
          response.statusCode &&
          [301, 302, 307, 308].includes(response.statusCode) &&
          response.headers.location
        ) {
          return downloadFileWithRedirects(response.headers.location, destPath, onProgress)
            .then(resolve)
            .catch(reject);
        }

        if (response.statusCode && (response.statusCode < 200 || response.statusCode >= 300)) {
          return reject(new Error(`Download failed with status: ${response.statusCode}`));
        }

        const totalBytes = parseInt(response.headers['content-length'] || '0', 10);
        let receivedBytes = 0;

        const fileStream = fs.createWriteStream(destPath);
        response.on('data', (chunk) => {
          receivedBytes += chunk.length;
          onProgress?.(receivedBytes, totalBytes);
        });

        response.pipe(fileStream);

        fileStream.on('finish', () => {
          fileStream.close();
          resolve();
        });

        fileStream.on('error', (err) => {
          fs.unlink(destPath, () => {});
          reject(err);
        });
      }
    );

    request.on('error', (err) => {
      fs.unlink(destPath, () => {});
      reject(err);
    });

    request.setTimeout(60000, () => {
      request.destroy();
      fs.unlink(destPath, () => {});
      reject(new Error('Download timed out after 60s'));
    });
  });
}

/**
 * Probes the GitHub releases API to find the best engine version for the given host platform.
 * Verifies that candidate releases actually contain the required binary asset.
 */
export async function probeEngineVersion(
  extensionVersion: string,
  userSetting?: string,
  outputChannel?: LogOutput,
  targetArchiveName?: string
): Promise<{ version: string; assetUrl?: string }> {
  const log = (msg: string) => outputChannel?.appendLine(`[BinaryManager] ${msg}`);
  const { pattern, fallback } = getEngineConfigFromExtensionVersion(extensionVersion, userSetting);
  const cleanExt = extensionVersion.replace(/^v/, '').trim();
  const extMajor = cleanExt.split('.')[0];
  const requiredAsset = targetArchiveName || resolveTargetAsset().archiveName;

  log(`Probing releases for engine: extension v${cleanExt}, pattern '${pattern}', target asset '${requiredAsset}'`);

  try {
    const releases = await fetchGitHubReleases();

    interface ReleaseMatch {
      version: string;
      assetUrl: string;
    }
    const availableWithAsset: ReleaseMatch[] = [];

    for (const rel of releases) {
      if (rel.draft || rel.prerelease) continue;
      const tag = rel.tag_name || '';
      const ver = tag.replace(/^v/, '').trim();
      if (!ver || !/^\d+\.\d+/.test(ver)) continue;

      const matchingAsset = (rel.assets || []).find((a: any) => a.name === requiredAsset);
      if (matchingAsset && matchingAsset.browser_download_url) {
        availableWithAsset.push({
          version: ver,
          assetUrl: matchingAsset.browser_download_url,
        });
      }
    }

    if (availableWithAsset.length > 0) {
      availableWithAsset.sort((a, b) => compareSemver(a.version, b.version));

      // Tier 1: Exact pattern matches (e.g. major.minor.*)
      const patternMatches = availableWithAsset.filter((r) => matchesEnginePattern(r.version, pattern));
      if (patternMatches.length > 0) {
        const best = patternMatches[patternMatches.length - 1];
        log(`Smart probe tier 1: found release v${best.version} matching '${pattern}'`);
        return best;
      }

      // Tier 2: Same major version (e.g. 1.x)
      if (extMajor) {
        const sameMajor = availableWithAsset.filter((r) => r.version.startsWith(`${extMajor}.`));
        if (sameMajor.length > 0) {
          const best = sameMajor[sameMajor.length - 1];
          log(`Smart probe tier 2: found release v${best.version} in major series ${extMajor}.x`);
          return best;
        }
      }

      // Tier 3: Highest available release containing asset
      const latest = availableWithAsset[availableWithAsset.length - 1];
      log(`Smart probe tier 3: using latest available release v${latest.version}`);
      return latest;
    }

    log(`No release found containing asset '${requiredAsset}'. Using fallback: v${fallback}`);
  } catch (err: any) {
    log(`Smart probe failed (${err?.message || err}). Falling back to extension version v${fallback}`);
  }

  const fallbackUrl = `https://github.com/${GITHUB_REPO}/releases/download/v${fallback}/${requiredAsset}`;
  return { version: fallback, assetUrl: fallbackUrl };
}
