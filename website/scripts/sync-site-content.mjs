import {cpSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, statSync, writeFileSync} from 'node:fs';
import path from 'node:path';

const siteRoot = process.cwd();
const repoRoot = path.resolve(siteRoot, '..');
const sourceRoot = path.resolve(repoRoot, '../terrabuild.io');

if (!existsSync(sourceRoot)) {
  console.error(`Missing terrabuild.io source directory: ${sourceRoot}`);
  process.exit(1);
}

const docsSource = path.join(sourceRoot, 'content', 'docs');
const blogSource = path.join(sourceRoot, 'content', 'blog');
const staticSource = path.join(sourceRoot, 'static');

const docsTarget = path.join(siteRoot, 'site-docs');
const blogTarget = path.join(siteRoot, 'blog');
const staticTarget = path.join(siteRoot, 'static');

const replaceAll = (input, replacements) =>
  replacements.reduce((value, [pattern, replacement]) => value.replace(pattern, replacement), input);

function transformCardsBlock(content) {
  return content.replace(/\{\{< cards >\}\}([\s\S]*?)\{\{< \/cards >\}\}/g, (_, body) => {
    const items = [...body.matchAll(/\{\{< card ([^>]+) >\}\}/g)].map((match) => {
      const attributes = match[1];
      const link = attributes.match(/link="([^"]+)"/)?.[1] ?? '#';
      const title = attributes.match(/title="([^"]+)"/)?.[1] ?? link;
      const subtitle = attributes.match(/subtitle="([^"]+)"/)?.[1];
      return `- [${title}](${link})${subtitle ? `: ${subtitle}` : ''}`;
    });

    return items.join('\n');
  });
}

function transformMarkdown(content) {
  return replaceAll(
    transformCardsBlock(content),
    [
      [/\{\{< callout type="info" >\}\}/g, ':::info'],
      [/\{\{< callout type="warning" >\}\}/g, ':::warning'],
      [/\{\{< \/callout >\}\}/g, ':::'],
      [/\{\{< icon [^>]+ >\}\}/g, ''],
      [/\{\{< icon "[^"]+" >\}\}/g, ''],
      [/<br>/g, '<br />'],
      [/\/docs\/quick-start/g, '/docs/getting-started/quick-start'],
      [/\/docs\/scaffolding/g, '/docs/getting-started/scaffolding'],
      [/\/command\)/g, '/dispatch)'],
      [/^\s*weight:\s+\d+\s*$/gm, ''],
      [/^\s*next:\s+.+$/gm, ''],
      [/\n{3,}/g, '\n\n'],
    ],
  );
}

function addSlugFrontMatter(content, slug) {
  if (!content.startsWith('---\n')) {
    return `---\nslug: ${slug}\n---\n\n${content}`;
  }

  const end = content.indexOf('\n---', 4);
  if (end === -1) {
    return content;
  }

  const frontMatter = content.slice(0, end + 4);
  const body = content.slice(end + 4);
  if (/^slug:\s+/m.test(frontMatter)) {
    return content;
  }

  return `${frontMatter.replace(/\n---$/, `\nslug: ${slug}\n---`)}${body}`;
}

function copyStatic() {
  rmSync(staticTarget, {recursive: true, force: true});
  cpSync(staticSource, staticTarget, {recursive: true});
  writeFileSync(path.join(staticTarget, 'CNAME'), 'terrabuild.io\n');
}

function ensureDir(dir) {
  mkdirSync(dir, {recursive: true});
}

function syncDocsDir(sourceDir, targetDir) {
  ensureDir(targetDir);

  for (const entry of readdirSync(sourceDir)) {
    const sourcePath = path.join(sourceDir, entry);
    const stats = statSync(sourcePath);

    if (stats.isDirectory()) {
      syncDocsDir(sourcePath, path.join(targetDir, entry));
      continue;
    }

    if (!entry.endsWith('.md')) {
      continue;
    }

    const targetName = entry === '_index.md' ? 'index.md' : entry;
    const targetPath = path.join(targetDir, targetName);
    const content = readFileSync(sourcePath, 'utf8');
    let transformed = transformMarkdown(content);
    const parentName = path.basename(targetDir);
    const fileStem = path.basename(targetName, '.md');

    if (fileStem !== 'index' && fileStem === parentName) {
      transformed = addSlugFrontMatter(transformed, `/${parentName}/${fileStem}`);
    }

    writeFileSync(targetPath, transformed);
  }
}

function syncBlog() {
  rmSync(blogTarget, {recursive: true, force: true});
  ensureDir(blogTarget);

  for (const entry of readdirSync(blogSource)) {
    if (entry === '_index.md' || !entry.endsWith('.md')) {
      continue;
    }

    const sourcePath = path.join(blogSource, entry);
    const targetPath = path.join(blogTarget, entry);
    writeFileSync(targetPath, transformMarkdown(readFileSync(sourcePath, 'utf8')));
  }
}

rmSync(docsTarget, {recursive: true, force: true});
syncDocsDir(docsSource, docsTarget);
syncBlog();
copyStatic();

console.log('Synchronized Terrabuild site content into Docusaurus directories.');
