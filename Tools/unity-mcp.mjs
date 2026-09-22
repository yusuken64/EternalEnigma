// Project-local MCP client, also usable when a host still has another project's connection cached.
import { readdirSync, existsSync, readFileSync } from 'node:fs';
import { resolve, join } from 'node:path';
import { pathToFileURL } from 'node:url';

const root = resolve(import.meta.dirname, '..');
const cache = join(root, 'Library/PackageCache');
const pkg = readdirSync(cache).find(n => n.startsWith('com.gamelovers.mcp-unity@'));
if (!pkg) throw Error('Open this project in Unity first to install its MCP package.');
const server = join(cache, pkg, 'Server~');
const sdk = join(server, 'node_modules/@modelcontextprotocol/sdk/dist/esm/client');
if (!existsSync(sdk)) throw Error('The Unity MCP server dependencies are not installed yet.');
const { Client } = await import(pathToFileURL(join(sdk, 'index.js')));
const { StdioClientTransport } = await import(pathToFileURL(join(sdk, 'stdio.js')));
const client = new Client({ name: 'eternal-enigma-harness', version: '1.0.0' });
const transport = new StdioClientTransport({
    command: process.execPath,
    args: [join(server, 'build/index.js')],
    env: { ...process.env,
        MCP_UNITY_SETTINGS_PATH: join(root, 'ProjectSettings/McpUnitySettings.json'),
        MCP_UNITY_AUTH_TOKEN_PATH: join(root, 'Library/McpUnity/bridge-token') },
    stderr: 'inherit'
});
try {
    await client.connect(transport);
    const name = process.argv[2] ?? 'get_scene_info';
    if (name === 'harness') {
        const mode = process.argv[3] ?? 'EditMode';
        if (!['EditMode', 'PlayMode', 'Skills', 'Demo', 'Town', 'Overworld', 'Campaign', 'Autoplay', 'Enemies'].includes(mode)) throw Error('Expected EditMode, PlayMode, Skills, Demo, Town, Overworld, Campaign, Autoplay or Enemies.');
        const resultPath = join(root, `Temp/HarnessResults/${['Skills', 'Demo', 'Town', 'Overworld', 'Campaign', 'Autoplay', 'Enemies'].includes(mode) ? 'PlayMode' : mode}.json`);
        const readSummary = () => {
            try { return JSON.parse(readFileSync(resultPath, 'utf8')); } catch { return null; }
        };
        const previousRun = readSummary()?.runId;
        const started = await client.callTool({ name: 'execute_menu_item',
            arguments: { menuPath: `Tools/Eternal Enigma/Tests/Run ${mode}` } });
        if (started.isError) throw Error(JSON.stringify(started));
        const deadline = Date.now() + 600000;
        let summary;
        while (Date.now() < deadline) {
            summary = readSummary();
            if (summary?.runId !== previousRun && summary?.state === 'Completed') break;
            await new Promise(resolve => setTimeout(resolve, 500));
        }
        if (!summary || summary.runId === previousRun || summary.state !== 'Completed')
            throw Error('Harness timed out. Check Unity Console; no successful result is assumed.');
        console.log(JSON.stringify(summary, null, 2));
        if (summary.failed > 0 || summary.passed === 0) process.exitCode = 1;
    } else {
    const argument = process.argv[3] ?? '{}';
    const json = argument.startsWith('@') ? readFileSync(resolve(argument.slice(1)), 'utf8') : argument;
    const result = name === 'list' ? await client.listTools() :
        await client.callTool({ name, arguments: JSON.parse(json) }, undefined, { timeout: 600000 });
    console.log(JSON.stringify(result, null, 2));
    if (result.isError) process.exitCode = 1;
    if (name === 'run_tests') {
        const summary = result.content?.filter(c => c.type === 'text').map(c => {
            try { return JSON.parse(c.text); } catch { return null; }
        }).find(c => c && typeof c.testCount === 'number');
        if (!summary || summary.testCount === 0 || summary.failCount > 0) process.exitCode = 1;
    }
    }
} finally {
    await client.close();
}
