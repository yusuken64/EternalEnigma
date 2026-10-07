// Run the local fit build in an isolated Chromium profile and retain raw evidence.
// Uses an installed Playwright core discovered through its standard browser-cache links.
import { createServer } from 'node:http';
import { readFileSync, writeFileSync, readdirSync, existsSync, statSync } from 'node:fs';
import { resolve, join, extname, sep } from 'node:path';
import { pathToFileURL } from 'node:url';

const project = resolve(import.meta.dirname, '..');
const integrated = process.argv.includes('--integrated');
const build = join(project, integrated ? 'Builds/UnifiedPresentationWebGL' : 'Builds/DioramaFitWebGL');
const evidence = join(project, 'Docs/Art/Previews/Diorama/' + (integrated ? 'Verification' : 'Fit'));
const cache = join(process.env.LOCALAPPDATA, 'ms-playwright');
const links = readdirSync(join(cache, '.links')).map(n => readFileSync(join(cache, '.links', n), 'utf8').trim());
const core = links.find(p => existsSync(join(p, 'index.mjs')));
if (!core) throw new Error('Install Playwright Chromium before running the WebGL benchmark.');
const { chromium } = await import(pathToFileURL(join(core, 'index.mjs')));
const server = createServer((request, response) => {
    const requested = decodeURIComponent(new URL(request.url, 'http://localhost').pathname);
    const path = resolve(build, '.' + (requested === '/' ? '/index.html' : requested));
    if (!path.startsWith(build + sep) || !existsSync(path) || !statSync(path).isFile()) {
        response.writeHead(404); response.end(); return;
    }
    const compression = path.endsWith('.gz') ? 'gzip' : path.endsWith('.br') ? 'br' : null;
    const suffix = extname(compression ? path.slice(0, compression === 'gzip' ? -3 : -3) : path);
    const mime = { '.html':'text/html', '.js':'application/javascript', '.wasm':'application/wasm', '.css':'text/css', '.png':'image/png' }[suffix] ?? 'application/octet-stream';
    response.setHeader('Content-Type', mime);
    response.setHeader('Cross-Origin-Opener-Policy','same-origin');
    response.setHeader('Cross-Origin-Embedder-Policy','require-corp');
    if (compression) response.setHeader('Content-Encoding', compression);
    response.end(readFileSync(path));
});
await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
let browser;
const logs = [];
const captures = [];
try {
    browser = await chromium.launch({ headless: true, args: ['--use-angle=d3d11','--enable-webgl','--disable-background-timer-throttling','--disable-renderer-backgrounding'] });
    const page = await browser.newPage({ viewport:{width:1280,height:800} });
    let finish;
    const completed = new Promise(resolve => { finish = resolve; });
    page.on('console', event => {
        const line = event.text(); logs.push(event.type() + ': ' + line);
        const stage = line.match(/DIORAMA_PLAYER_STAGE (Town|Dungeon|Overworld)/)?.[1];
        if (integrated && stage) captures.push(page.screenshot({path:join(evidence,'WebGL_'+stage+'.png')}));
        const marker = integrated ? 'DIORAMA_PLAYER_JSON ' : 'DIORAMA_BENCHMARK_JSON ';
        const offset = line.indexOf(marker);
        if (offset >= 0) {
            try { finish(JSON.parse(line.slice(offset + marker.length))); } catch {}
        }
    });
    page.on('pageerror', error => logs.push('PAGE ERROR: '+error));
    await page.goto(`http://127.0.0.1:${server.address().port}` + (integrated ? '/?dioramaValidation=1' : '/'), {waitUntil:'domcontentloaded'});
    const interval = setInterval(() => console.log('WebGL benchmark running; console lines: '+logs.length), 30000);
    let timer;
    const report = await Promise.race([completed, new Promise((_,reject) => { timer=setTimeout(()=>reject(new Error('WebGL benchmark timed out')),600000); })]).finally(()=>{clearInterval(interval);clearTimeout(timer);});
    writeFileSync(join(evidence,integrated?'WebGLPlayerValidation.json':'WebGLBenchmark.json'),JSON.stringify(report,null,2));
    await Promise.all(captures);
    await page.screenshot({path:join(evidence,integrated?'WebGLPlayerValidation.png':'WebGLBenchmark.png')});
    console.log(JSON.stringify(report,null,2));
    if (integrated && (report.errors.length || report.samples.length !== 3 || report.samples.some(s => s.errorMaterials)))
        throw new Error('Integrated player validation failed; see the saved report.');
} finally {
    writeFileSync(join(evidence,'WebGLConsole.txt'),logs.join('\n'));
    if(browser)await browser.close();
    await new Promise(resolve=>server.close(resolve));
}
