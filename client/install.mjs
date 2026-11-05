#!/usr/bin/env node
/**
 * install.mjs — OpenClaw Agent Memory Service client installer
 *
 * Downloads memory-sync-api.mjs into the OpenClaw workspace scripts folder,
 * writes config to scripts/.env, and optionally sets up a cron job.
 *
 * Usage:
 *   node install.mjs \
 *     --url=http://your-server:5228 \
 *     --api-key=ams_xxxx \
 *     --namespace-id=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
 *
 * Optional flags:
 *   --workspace=/path/to/workspace   (default: ~/workspace or ~/.openclaw/workspace)
 *   --no-cron                        Skip cron job setup
 *   --dry-run                        Print what would happen without writing
 */

import { readFile, writeFile, mkdir, access } from 'fs/promises';
import { existsSync } from 'fs';
import { resolve, join, dirname } from 'path';
import { homedir } from 'os';
import { execSync } from 'child_process';

// ---------------------------------------------------------------------------
// Arg parsing
// ---------------------------------------------------------------------------
const args = {};
for (const arg of process.argv.slice(2)) {
  if (arg.startsWith('--')) {
    const eq = arg.indexOf('=');
    if (eq === -1) {
      args[arg.slice(2)] = true;
    } else {
      args[arg.slice(2, eq)] = arg.slice(eq + 1);
    }
  }
}

const DRY_RUN = args['dry-run'] === true || args['dry-run'] === 'true';
const SKIP_CRON = args['no-cron'] === true || args['no-cron'] === 'true';

// ---------------------------------------------------------------------------
// Validate required args
// ---------------------------------------------------------------------------
const required = ['url', 'api-key', 'namespace-id'];
const missing = required.filter(k => !args[k]);
if (missing.length > 0) {
  console.error(`❌  Missing required arguments: ${missing.map(k => `--${k}`).join(', ')}`);
  console.error('');
  console.error('Usage:');
  console.error('  node install.mjs \\');
  console.error('    --url=http://your-server:5228 \\');
  console.error('    --api-key=ams_xxxx \\');
  console.error('    --namespace-id=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx');
  process.exit(1);
}

const SERVER_URL = args['url'].replace(/\/$/, '');
const API_KEY = args['api-key'];
const NAMESPACE_ID = args['namespace-id'];

// ---------------------------------------------------------------------------
// Resolve workspace path
// ---------------------------------------------------------------------------
async function findWorkspace() {
  if (args['workspace']) return resolve(args['workspace']);

  const candidates = [
    join(homedir(), 'workspace'),
    join(homedir(), '.openclaw', 'workspace'),
  ];

  for (const p of candidates) {
    if (existsSync(p)) return p;
  }

  // Default to ~/.openclaw/workspace (will be created)
  return join(homedir(), '.openclaw', 'workspace');
}

// ---------------------------------------------------------------------------
// Download helper (uses built-in fetch, node 18+)
// ---------------------------------------------------------------------------
async function fetchText(url) {
  const res = await fetch(url);
  if (!res.ok) throw new Error(`HTTP ${res.status} fetching ${url}`);
  return res.text();
}

// ---------------------------------------------------------------------------
// Write helper (respects dry-run)
// ---------------------------------------------------------------------------
async function writeFileSafe(path, content, description) {
  if (DRY_RUN) {
    console.log(`[dry-run] Would write ${description} → ${path}`);
    return;
  }
  await mkdir(dirname(path), { recursive: true });
  await writeFile(path, content, 'utf8');
  console.log(`✅  Wrote ${description} → ${path}`);
}

// ---------------------------------------------------------------------------
// Merge .env (add/update keys without clobbering existing entries)
// ---------------------------------------------------------------------------
async function mergeEnv(envPath, newVars) {
  let existing = '';
  if (existsSync(envPath)) {
    existing = await readFile(envPath, 'utf8');
  }

  const lines = existing.split('\n');
  const updated = { ...newVars };

  // Update existing keys in-place
  const patched = lines.map(line => {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith('#')) return line;
    const eq = trimmed.indexOf('=');
    if (eq === -1) return line;
    const key = trimmed.slice(0, eq).trim();
    if (key in updated) {
      const val = updated[key];
      delete updated[key];
      return `${key}=${val}`;
    }
    return line;
  });

  // Append any new keys that weren't already present
  const newLines = Object.entries(updated).map(([k, v]) => `${k}=${v}`);
  if (newLines.length > 0) {
    if (patched[patched.length - 1] !== '') patched.push('');
    patched.push(...newLines);
  }

  return patched.join('\n');
}

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------
async function main() {
  console.log('🚀  OpenClaw Agent Memory Service installer');
  console.log(`    Server: ${SERVER_URL}`);
  console.log(`    Namespace: ${NAMESPACE_ID}`);
  if (DRY_RUN) console.log('    [DRY RUN — no files will be written]');
  console.log('');

  // 1. Find workspace
  const workspace = await findWorkspace();
  console.log(`📁  Workspace: ${workspace}`);

  const scriptsDir = join(workspace, 'scripts');
  const stateDir = join(workspace, 'state');
  const syncScript = join(scriptsDir, 'memory-sync-api.mjs');
  const envFile = join(scriptsDir, '.env');

  // 2. Download memory-sync-api.mjs from the same repo
  const SYNC_SCRIPT_URL = 'https://raw.githubusercontent.com/cctallen/agent-memory-service/master/client/memory-sync-api.mjs';
  console.log(`⬇️   Downloading memory-sync-api.mjs...`);
  let syncScriptContent;
  try {
    syncScriptContent = await fetchText(SYNC_SCRIPT_URL);
    console.log(`    Downloaded ${syncScriptContent.length} bytes`);
  } catch (err) {
    console.error(`❌  Failed to download sync script: ${err.message}`);
    process.exit(1);
  }

  await writeFileSafe(syncScript, syncScriptContent, 'memory-sync-api.mjs');

  // Make it executable
  if (!DRY_RUN) {
    try { execSync(`chmod +x ${syncScript}`); } catch {}
  }

  // 3. Ensure state dir exists
  if (!DRY_RUN) {
    await mkdir(stateDir, { recursive: true });
  }

  // 4. Write .env config
  console.log('⚙️   Updating .env config...');
  const envContent = await mergeEnv(envFile, {
    MEMORY_SERVICE_URL: SERVER_URL,
    MEMORY_SERVICE_API_KEY: API_KEY,
    MEMORY_NAMESPACE_ID: NAMESPACE_ID,
  });
  await writeFileSafe(envFile, envContent, '.env');

  // 5. Verify server reachability
  console.log('🔍  Testing server connectivity...');
  try {
    const res = await fetch(`${SERVER_URL}/health`);
    if (res.ok || res.status === 404) {
      // 404 is fine — means server is up but /health not defined
      console.log(`✅  Server reachable (HTTP ${res.status})`);
    } else {
      console.warn(`⚠️   Server returned HTTP ${res.status} — check URL and firewall`);
    }
  } catch (err) {
    console.warn(`⚠️   Could not reach server: ${err.message}`);
    console.warn('    Continuing anyway — check your --url and network settings');
  }

  // 6. Run initial push (unless dry-run)
  if (!DRY_RUN) {
    const memoryFile = join(workspace, 'MEMORY.md');
    if (existsSync(memoryFile)) {
      console.log('📤  Running initial memory push...');
      try {
        execSync(`node ${syncScript} push`, {
          stdio: 'inherit',
          env: { ...process.env, HOME: homedir() },
        });
      } catch (err) {
        console.warn(`⚠️   Initial push failed: ${err.message}`);
        console.warn('    You can run it manually: node scripts/memory-sync-api.mjs push');
      }
    } else {
      console.log('ℹ️   No MEMORY.md found — skipping initial push');
      console.log(`    Create ${memoryFile} and run: node ${syncScript} push`);
    }
  }

  // 7. Set up cron job (if not skipped)
  if (!SKIP_CRON && !DRY_RUN) {
    console.log('⏰  Setting up 6-hour sync cron job...');
    try {
      const cronLine = `0 */6 * * * node ${syncScript} push >> /tmp/memory-sync.log 2>&1`;
      const existingCron = (() => {
        try { return execSync('crontab -l 2>/dev/null').toString(); } catch { return ''; }
      })();

      if (existingCron.includes('memory-sync-api.mjs')) {
        console.log('    Cron job already exists — skipping');
      } else {
        const newCron = existingCron.trimEnd() + '\n' + cronLine + '\n';
        execSync(`echo ${JSON.stringify(newCron)} | crontab -`);
        console.log(`✅  Cron job added: ${cronLine}`);
      }
    } catch (err) {
      console.warn(`⚠️   Could not set up cron: ${err.message}`);
      console.warn('    Add manually: 0 */6 * * * node ' + syncScript + ' push >> /tmp/memory-sync.log 2>&1');
    }
  } else if (SKIP_CRON) {
    console.log('ℹ️   Skipping cron setup (--no-cron)');
  }

  // 8. Done
  console.log('');
  console.log('🎉  Installation complete!');
  console.log('');
  console.log('Next steps:');
  console.log(`  • Push memory:  node ${syncScript} push`);
  console.log(`  • Pull memory:  node ${syncScript} pull`);
  console.log(`  • Check status: node ${syncScript} status`);
}

main().catch(err => {
  console.error('❌  Fatal error:', err.message);
  process.exit(1);
});
