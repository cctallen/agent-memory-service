#!/usr/bin/env node
/**
 * memory-sync-api.mjs — Sync OpenClaw memory to/from the agent-memory-service API
 *
 * Usage:
 *   node memory-sync-api.mjs          # push (default)
 *   node memory-sync-api.mjs push     # push MEMORY.md sections + last 7 days of daily notes
 *   node memory-sync-api.mjs pull     # pull memories from API, reconstruct MEMORY.md
 *   node memory-sync-api.mjs status   # show count + last-updated from API
 *   node memory-sync-api.mjs check    # check for new memories since last check (low overhead)
 *   node memory-sync-api.mjs view     # pretty-print memories (--agent= --type= --source= --compact)
 *
 * Flags:
 *   --dry-run    Log what would happen without writing anything
 *
 * Config (.env):
 *   MEMORY_SERVICE_URL        e.g. https://memory.example.com
 *   MEMORY_SERVICE_API_KEY    API key for X-Api-Key header
 *   MEMORY_NAMESPACE_ID       Namespace UUID
 */

import { readFile, writeFile, readdir, stat } from 'fs/promises';
import { existsSync } from 'fs';
import { resolve, join, dirname } from 'path';
import { fileURLToPath } from 'url';

// ---------------------------------------------------------------------------
// Paths
// ---------------------------------------------------------------------------
const __dirname = dirname(fileURLToPath(import.meta.url));
const ENV_PATH = join(__dirname, '.env');
const WORKSPACE = '/home/ubuntu/.openclaw/workspace';
const MEMORY_FILE = join(WORKSPACE, 'MEMORY.md');
const CHECK_STATE_FILE = join(WORKSPACE, 'state', 'memory-check-state.json');
const DAILY_DIR = join(WORKSPACE, 'memory');

// ---------------------------------------------------------------------------
// .env loader (no external dependencies)
// ---------------------------------------------------------------------------
async function loadEnvAsync(path) {
  if (!existsSync(path)) return {};
  const text = await readFile(path, 'utf8');
  const env = {};
  for (const raw of text.split('\n')) {
    const line = raw.trim();
    if (!line || line.startsWith('#')) continue;
    const eq = line.indexOf('=');
    if (eq === -1) continue;
    const key = line.slice(0, eq).trim();
    let val = line.slice(eq + 1).trim();
    if ((val.startsWith('"') && val.endsWith('"')) ||
        (val.startsWith("'") && val.endsWith("'"))) {
      val = val.slice(1, -1);
    }
    env[key] = val;
  }
  return env;
}

// ---------------------------------------------------------------------------
// API helpers
// ---------------------------------------------------------------------------
async function apiRequest(config, method, path, body) {
  const url = `${config.baseUrl}${path}`;
  const headers = {
    'X-Api-Key': config.apiKey,
    'Content-Type': 'application/json',
    'Accept': 'application/json',
  };

  const opts = { method, headers };
  if (body !== undefined) opts.body = JSON.stringify(body);

  const res = await fetch(url, opts);
  if (!res.ok) {
    const text = await res.text().catch(() => '');
    throw new Error(`API ${method} ${url} → ${res.status} ${res.statusText}: ${text}`);
  }

  // 204 No Content
  if (res.status === 204) return null;
  return res.json();
}

async function getMemories(config, pageSize = 100) {
  return apiRequest(config, 'GET', `/api/namespaces/${config.namespaceId}/memories?pageSize=${pageSize}`);
}

async function getMemoriesSince(config, since) {
  const iso = since instanceof Date ? since.toISOString() : since;
  return apiRequest(config, 'GET', `/api/namespaces/${config.namespaceId}/memories?pageSize=100&since=${encodeURIComponent(iso)}`);
}

async function upsertMemory(config, payload) {
  return apiRequest(config, 'POST', `/api/namespaces/${config.namespaceId}/memories`, payload);
}

// ---------------------------------------------------------------------------
// MEMORY.md section parser
// ---------------------------------------------------------------------------
function splitIntoSections(content) {
  const lines = content.split('\n');
  const sections = [];
  let currentHeading = null;
  let currentLines = [];

  for (const line of lines) {
    if (line.startsWith('## ')) {
      if (currentHeading !== null) {
        sections.push({ heading: currentHeading, content: currentLines.join('\n').trim() });
      }
      currentHeading = line.slice(3).trim();
      currentLines = [line];
    } else {
      if (currentHeading !== null) {
        currentLines.push(line);
      }
      // Lines before first ## heading are ignored (e.g. title line)
    }
  }

  // Last section
  if (currentHeading !== null) {
    sections.push({ heading: currentHeading, content: currentLines.join('\n').trim() });
  }

  return sections;
}

// ---------------------------------------------------------------------------
// Daily notes helpers
// ---------------------------------------------------------------------------
function daysAgo(n) {
  const d = new Date();
  d.setUTCDate(d.getUTCDate() - n);
  return d.toISOString().slice(0, 10); // YYYY-MM-DD
}

async function getRecentDailyFiles() {
  const dates = [];
  for (let i = 0; i < 7; i++) {
    dates.push(daysAgo(i));
  }

  const files = [];
  for (const date of dates) {
    const filePath = join(DAILY_DIR, `${date}.md`);
    if (existsSync(filePath)) {
      files.push({ date, filePath, source: `memory/${date}.md` });
    }
  }
  return files;
}

// ---------------------------------------------------------------------------
// PUSH
// ---------------------------------------------------------------------------
async function push(config, dryRun) {
  console.log('📤 PUSH mode');
  let pushed = 0;
  let skipped = 0;

  // --- MEMORY.md sections ---
  if (!existsSync(MEMORY_FILE)) {
    console.warn(`⚠️  MEMORY.md not found at ${MEMORY_FILE}`);
  } else {
    const content = await readFile(MEMORY_FILE, 'utf8');
    const sections = splitIntoSections(content);
    console.log(`📄 MEMORY.md: found ${sections.length} section(s)`);

    for (const { heading, content: sectionContent } of sections) {
      const source = `MEMORY.md#${heading}`;
      const payload = {
        content: sectionContent,
        type: 5,               // MemoryType.Summary = 5
        source,
        visibility: 0,         // MemoryVisibility.Public = 0
        metadata: {
          agent: 'todd-jr',
          section: heading,
          file: 'MEMORY.md',
        },
      };

      if (dryRun) {
        console.log(`  [dry-run] Would upsert: ${source} (${sectionContent.length} chars)`);
        pushed++;
      } else {
        try {
          await upsertMemory(config, payload);
          console.log(`  ✅ Upserted: ${source}`);
          pushed++;
        } catch (err) {
          console.error(`  ❌ Failed: ${source} — ${err.message}`);
          skipped++;
        }
      }
    }
  }

  // --- Daily notes (last 7 days) ---
  const dailyFiles = await getRecentDailyFiles();
  console.log(`📅 Daily notes: found ${dailyFiles.length} file(s) in last 7 days`);

  for (const { filePath, source, date } of dailyFiles) {
    const content = await readFile(filePath, 'utf8');
    const payload = {
      content,
      type: 3,               // MemoryType.Context = 3
      source,
      visibility: 1,         // MemoryVisibility.Private = 1
      metadata: {
        agent: 'todd-jr',
        date,
        file: source,
      },
    };

    if (dryRun) {
      console.log(`  [dry-run] Would upsert: ${source} (${content.length} chars)`);
      pushed++;
    } else {
      try {
        await upsertMemory(config, payload);
        console.log(`  ✅ Upserted: ${source}`);
        pushed++;
      } catch (err) {
        console.error(`  ❌ Failed: ${source} — ${err.message}`);
        skipped++;
      }
    }
  }

  console.log(`\n✨ Push complete. Upserted: ${pushed}, Failed: ${skipped}`);
}

// ---------------------------------------------------------------------------
// PULL
// ---------------------------------------------------------------------------
async function pull(config, dryRun) {
  console.log('📥 PULL mode');

  const result = await getMemories(config);
  const memories = result?.items ?? result ?? [];
  console.log(`📦 Retrieved ${memories.length} memories from API`);

  // Filter: agent = todd-jr OR visibility is Public (0)
  const relevant = memories.filter(m => {
    const isOurs = m.metadata?.agent === 'todd-jr';
    const isPublic = m.visibility === 0 || m.visibility === 'Public';
    return isOurs || isPublic;
  });
  console.log(`🔍 Relevant memories (ours or public): ${relevant.length}`);

  // Separate MEMORY.md sections from daily notes
  const memorySections = relevant
    .filter(m => m.source?.startsWith('MEMORY.md#'))
    .sort((a, b) => a.source.localeCompare(b.source));

  const dailyNotes = relevant
    .filter(m => m.source?.startsWith('memory/') && m.source.endsWith('.md'));

  // Reconstruct MEMORY.md from sections
  if (memorySections.length > 0) {
    const reconstructed = '# MEMORY.md\n\n' + memorySections.map(m => m.content).join('\n\n') + '\n';

    if (dryRun) {
      console.log(`[dry-run] Would write MEMORY.md (${reconstructed.length} chars, ${memorySections.length} sections):`);
      console.log('---');
      console.log(reconstructed.slice(0, 500) + (reconstructed.length > 500 ? '...' : ''));
      console.log('---');
    } else {
      await writeFile(MEMORY_FILE, reconstructed, 'utf8');
      console.log(`✅ MEMORY.md written (${memorySections.length} sections, ${reconstructed.length} chars)`);
    }
  } else {
    console.log('⚠️  No MEMORY.md sections found in API — skipping MEMORY.md reconstruction');
  }

  // Reconstruct daily notes
  for (const note of dailyNotes) {
    const filePath = join(WORKSPACE, note.source);
    if (dryRun) {
      console.log(`[dry-run] Would write ${note.source} (${note.content?.length ?? 0} chars)`);
    } else {
      try {
        await writeFile(filePath, note.content ?? '', 'utf8');
        console.log(`✅ Written: ${note.source}`);
      } catch (err) {
        console.error(`❌ Failed to write ${note.source}: ${err.message}`);
      }
    }
  }

  console.log(`\n✨ Pull complete.`);
}

// ---------------------------------------------------------------------------
// STATUS
// ---------------------------------------------------------------------------
async function status(config) {
  console.log('📊 STATUS mode');

  const result = await getMemories(config, 200);
  const memories = result?.items ?? result ?? [];

  const ours = memories.filter(m => m.metadata?.agent === 'todd-jr');
  const publicOther = memories.filter(m =>
    (m.visibility === 0 || m.visibility === 'Public') && m.metadata?.agent !== 'todd-jr'
  );

  console.log(`\nNamespace: ${config.namespaceId}`);
  console.log(`Total memories in namespace: ${memories.length}`);
  console.log(`  → From todd-jr: ${ours.length}`);
  console.log(`  → Public (other agents): ${publicOther.length}`);

  if (ours.length > 0) {
    // Find last updated
    const sorted = [...ours].sort((a, b) => {
      const ta = new Date(a.updatedAt ?? a.createdAt ?? 0).getTime();
      const tb = new Date(b.updatedAt ?? b.createdAt ?? 0).getTime();
      return tb - ta;
    });
    const latest = sorted[0];
    const latestTime = latest.updatedAt ?? latest.createdAt ?? 'unknown';
    console.log(`  → Last updated: ${latestTime}`);
    console.log(`  → Most recent: ${latest.source}`);

    console.log('\nSources:');
    const bySource = {};
    for (const m of ours) {
      const key = m.source?.includes('#') ? m.source.split('#')[0] : (m.source ?? 'unknown');
      bySource[key] = (bySource[key] ?? 0) + 1;
    }
    for (const [src, count] of Object.entries(bySource).sort()) {
      console.log(`  ${src}: ${count} record(s)`);
    }
  }
}

// ---------------------------------------------------------------------------
// CHECK (lightweight — only fetches memories since last check)
// ---------------------------------------------------------------------------
async function check(config) {
  // Load last-checked timestamp
  let lastChecked = null;
  try {
    const raw = await readFile(CHECK_STATE_FILE, 'utf8');
    lastChecked = JSON.parse(raw).lastChecked ?? null;
  } catch { /* first run */ }

  const since = lastChecked ?? new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString();
  const now = new Date().toISOString();

  const result = await getMemoriesSince(config, since);
  const memories = result?.items ?? result ?? [];

  // Filter out our own pushed memories (source starts with MEMORY.md)
  const external = memories.filter(m => !m.source?.startsWith('MEMORY.md'));

  // Save updated timestamp
  await writeFile(CHECK_STATE_FILE, JSON.stringify({ lastChecked: now, lastCount: external.length }, null, 2), 'utf8');

  if (external.length === 0) {
    process.stdout.write('0'); // machine-readable: nothing new
    return;
  }

  // New memories found — print summary for the agent to act on
  console.log(`NEW_MEMORIES:${external.length}`);
  for (const m of external) {
    const preview = (m.content ?? '').slice(0, 100).replace(/\n/g, ' ');
    console.log(`  [${m.id}] ${preview}${m.content?.length > 100 ? '\u2026' : ''}`);
  }
}

// ---------------------------------------------------------------------------
// VIEW
// ---------------------------------------------------------------------------
async function view(config, args) {
  const filterAgent = args.find(a => a.startsWith('--agent='))?.split('=')[1];
  const filterType  = args.find(a => a.startsWith('--type='))?.split('=')[1];
  const filterSource = args.find(a => a.startsWith('--source='))?.split('=')[1];
  const compact = args.includes('--compact');

  console.log('🔍 VIEW mode');
  if (filterAgent)  console.log(`   Filter agent:  ${filterAgent}`);
  if (filterType)   console.log(`   Filter type:   ${filterType}`);
  if (filterSource) console.log(`   Filter source: ${filterSource}`);

  const result = await getMemories(config, 200);
  let memories = result?.items ?? result ?? [];

  // Apply filters
  if (filterAgent)  memories = memories.filter(m => m.metadata?.agent === filterAgent || m.agentId === filterAgent);
  if (filterType)   memories = memories.filter(m => String(m.type).toLowerCase() === filterType.toLowerCase());
  if (filterSource) memories = memories.filter(m => m.source?.toLowerCase().includes(filterSource.toLowerCase()));

  if (memories.length === 0) {
    console.log('\n⚠️  No memories found matching filters.');
    return;
  }

  const typeNames = ['Fact', 'Preference', 'Instruction', 'Context', 'Observation', 'Summary'];
  const visNames  = ['Public', 'Private'];

  console.log(`\n📦 ${memories.length} memory(s) found:\n`);

  for (const m of memories) {
    const typeName = typeof m.type === 'number' ? (typeNames[m.type] ?? m.type) : m.type;
    const visName  = typeof m.visibility === 'number' ? (visNames[m.visibility] ?? m.visibility) : m.visibility;
    const agent    = m.metadata?.agent ?? m.agentId ?? 'unknown';
    const date     = m.createdAt ? new Date(m.createdAt).toISOString().slice(0, 10) : '?';

    console.log(`─── ${m.source ?? '(no source)'}`);
    console.log(`    id:         ${m.id}`);
    console.log(`    agent:      ${agent}`);
    console.log(`    type:       ${typeName}  |  visibility: ${visName}  |  created: ${date}`);
    if (!compact) {
      console.log(`    content:`);
      const lines = (m.content ?? '').split('\n');
      for (const line of lines.slice(0, 20)) {
        console.log(`      ${line}`);
      }
      if (lines.length > 20) console.log(`      ... (${lines.length - 20} more lines)`);
    } else {
      const preview = (m.content ?? '').slice(0, 120).replace(/\n/g, ' ');
      console.log(`    preview:    ${preview}${m.content?.length > 120 ? '…' : ''}`);
    }
    console.log('');
  }
}

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------
async function main() {
  const args = process.argv.slice(2);
  const dryRun = args.includes('--dry-run');
  const cmd = args.find(a => !a.startsWith('--')) ?? 'push';

  if (!['push', 'pull', 'status', 'view', 'check'].includes(cmd)) {
    console.error(`Unknown command: ${cmd}`);
    console.error('Usage: node memory-sync-api.mjs [push|pull|status|view|check] [--dry-run]');
    console.error('       view options: --agent=<name> --type=<Summary|Context|Fact|...> --source=<string> --compact');
    process.exit(1);
  }

  if (dryRun) console.log('🔍 DRY RUN mode — no writes will occur\n');

  // Load config
  const env = await loadEnvAsync(ENV_PATH);
  const merged = { ...process.env, ...env };

  const baseUrl = merged.MEMORY_SERVICE_URL?.replace(/\/$/, '');
  const apiKey = merged.MEMORY_SERVICE_API_KEY;
  const namespaceId = merged.MEMORY_NAMESPACE_ID;

  if (!baseUrl || !apiKey || !namespaceId) {
    console.error('❌ Missing required config. Set in scripts/.env:');
    console.error('   MEMORY_SERVICE_URL');
    console.error('   MEMORY_SERVICE_API_KEY');
    console.error('   MEMORY_NAMESPACE_ID');
    process.exit(1);
  }

  const config = { baseUrl, apiKey, namespaceId };

  switch (cmd) {
    case 'push':   await push(config, dryRun); break;
    case 'pull':   await pull(config, dryRun); break;
    case 'status': await status(config); break;
    case 'check':  await check(config); break;
    case 'view':   await view(config, args); break;
  }
}

main().catch(err => {
  console.error('💥 Fatal error:', err.message);
  process.exit(1);
});
