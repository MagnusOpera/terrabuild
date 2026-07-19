import {existsSync, readdirSync} from 'node:fs';
import path from 'node:path';
import type {SidebarsConfig} from '@docusaurus/plugin-content-docs';

const extensionLabels: Record<string, string> = {
  fscript: 'FScript',
  openapi: 'OpenAPI',
};

const extensionLabel = (name: string) =>
  extensionLabels[name] ??
  name
    .split(/[-_]/)
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(' ');

const extensionDocsDirectory = path.join(
  process.cwd(),
  'site-docs',
  'extensions',
);

const extensionSidebarItems = existsSync(extensionDocsDirectory)
  ? readdirSync(extensionDocsDirectory, {withFileTypes: true})
      .filter(
        (entry) =>
          entry.isDirectory() &&
          existsSync(path.join(extensionDocsDirectory, entry.name, 'index.md')),
      )
      .sort((left, right) => left.name.localeCompare(right.name))
      .map((entry) => ({
        type: 'category' as const,
        label: extensionLabel(entry.name),
        link: {
          type: 'doc' as const,
          id: `extensions/${entry.name}/index`,
        },
        items: readdirSync(path.join(extensionDocsDirectory, entry.name), {
          withFileTypes: true,
        })
          .filter(
            (file) =>
              file.isFile() &&
              file.name.endsWith('.md') &&
              file.name !== 'index.md',
          )
          .map((file) => file.name.slice(0, -'.md'.length))
          .sort((left, right) => left.localeCompare(right))
          .map((operation) => `extensions/${entry.name}/${operation}`),
      }))
  : [];

const sidebars: SidebarsConfig = {
  docs: [
    {
      type: 'doc',
      id: 'index',
      className: 'tb-sidebar-hidden-item',
    },
    {
      type: 'category',
      label: 'Getting Started',
      link: {type: 'doc', id: 'getting-started/index'},
      items: [
        'getting-started/install',
        'getting-started/quick-start',
        'getting-started/key-concepts',
        'getting-started/scaffolding',
        'getting-started/graph',
        'getting-started/tasks',
        'getting-started/caching',
        'getting-started/batch',
        'getting-started/glossary',
      ],
    },
    {
      type: 'category',
      label: 'Syntax',
      link: {type: 'doc', id: 'syntax/index'},
      items: [
        'syntax/vscode',
        'syntax/comments',
        'syntax/block',
        'syntax/attribute',
        'syntax/identifier',
      ],
    },
    {
      type: 'category',
      label: 'Expression',
      link: {type: 'doc', id: 'expression/index'},
      items: [
        'expression/types',
        'expression/variables',
        'expression/functions',
        'expression/predefined-variables',
      ],
    },
    {
      type: 'category',
      label: 'Workspace',
      link: {type: 'doc', id: 'workspace/index'},
      items: [
        'workspace/workspace',
        'workspace/locals',
        'workspace/variable',
        'workspace/extension',
        'workspace/phase',
        'workspace/target',
      ],
    },
    {
      type: 'category',
      label: 'Project',
      link: {type: 'doc', id: 'project/index'},
      items: [
        'project/project',
        'project/locals',
        'project/extension',
        'project/target',
      ],
    },
    {
      type: 'category',
      label: 'Console',
      link: {type: 'doc', id: 'console/index'},
      items: [
        'console/graph',
      ],
    },
    {
      type: 'category',
      label: 'Extensions',
      link: {type: 'doc', id: 'extensions/index'},
      items: extensionSidebarItems,
    },
    {
      type: 'category',
      label: 'Extensibility',
      link: {type: 'doc', id: 'extensibility/index'},
      items: [
        {
          type: 'link',
          label: 'FScript documentation ↗',
          href: 'https://magnusopera.github.io/FScript/',
        },
        'extensibility/script',
        'extensibility/types',
        'extensibility/functions',
        'extensibility/container',
      ],
    },
    {
      type: 'category',
      label: 'Usage',
      link: {type: 'doc', id: 'usage/index'},
      items: [
        'usage/run',
        'usage/impact',
        'usage/logs',
        'usage/serve',
        'usage/scaffold',
        'usage/console',
        'usage/clear',
        'usage/prune',
        'usage/login',
        'usage/logout',
      ],
    },
    'troubleshooting',
    {
      type: 'html',
      value: '<div style="margin-top:1.75rem;font-weight:700;color:var(--ifm-heading-color);">More</div>',
      defaultStyle: true,
    },
    {
      type: 'link',
      label: 'Insights',
      href: 'https://insights.magnusopera.io',
    },
    {
      type: 'link',
      label: 'Magnus Opera',
      href: 'https://magnusopera.io',
    },
  ],
};

export default sidebars;
