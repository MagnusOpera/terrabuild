import {themes as prismThemes} from 'prism-react-renderer';
import {existsSync, readFileSync} from 'node:fs';
import path from 'node:path';
import type {Config} from '@docusaurus/types';
import type * as Preset from '@docusaurus/preset-classic';

const currentOnly = process.env.TERRABUILD_WEBSITE_CURRENT_ONLY === 'true';
const versionsPath = path.join(process.cwd(), 'versions.json');
const publishedVersions: string[] = !currentOnly && existsSync(versionsPath)
  ? JSON.parse(readFileSync(versionsPath, 'utf8')).filter((version: string) => version === 'stable')
  : [];
const stableTag: string = JSON.parse(readFileSync(path.join(process.cwd(), 'stable.json'), 'utf8')).tag;
const hasStable = publishedVersions.includes('stable');
const lastVersion = hasStable ? 'stable' : 'current';
const docsDirs = ['site-docs', ...publishedVersions.map((version) => `versioned_docs/version-${version}`)];

const config: Config = {
  title: 'Terrabuild',
  tagline: 'Desired-state configuration for build and deployment.',
  favicon: 'favicon.ico',

  url: 'https://terrabuild.io',
  baseUrl: '/',
  organizationName: 'MagnusOpera',
  projectName: 'Terrabuild',
  deploymentBranch: 'gh-pages',

  onBrokenLinks: 'throw',

  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  markdown: {
    mermaid: true,
  },

  themes: ['@docusaurus/theme-mermaid'],

  presets: [
    [
      'classic',
      {
        docs: {
          path: 'site-docs',
          routeBasePath: 'docs',
          sidebarPath: './sidebars.ts',
          lastVersion,
          onlyIncludeVersions: hasStable ? ['current', 'stable'] : ['current'],
          versions: {
            current: {
              label: 'Next',
              banner: 'unreleased',
              path: 'next',
            },
            ...(hasStable ? {stable: {label: `Stable (${stableTag.replace(/^v/, '')})`, banner: 'none' as const, path: ''}} : {}),
          },
          editUrl: ({version, docPath}) => {
            const ref = version === 'stable' ? stableTag : 'main';
            const file = docPath === 'whats-new.md' ? 'CHANGELOG.md' : `website/site-docs/${docPath}`;
            return `https://github.com/MagnusOpera/Terrabuild/blob/${ref}/${file}`;
          },
        },
        blog: false,
        pages: false,
        theme: {
          customCss: './theme/custom.css',
        },
      } satisfies Preset.Options,
    ],
  ],

  plugins: [
    [
      path.resolve(process.cwd(), 'plugins/google-analytics/index.cjs'),
      {
        trackingID: 'G-40K057W4NR',
        allowedHosts: ['terrabuild.io', 'www.terrabuild.io'],
      },
    ],
    [
      '@docusaurus/plugin-content-pages',
      {
        path: 'pages',
        routeBasePath: '/',
        editUrl: 'https://github.com/MagnusOpera/Terrabuild/tree/main/',
      },
    ],
    [
      require.resolve('@easyops-cn/docusaurus-search-local'),
      {
        docsDir: docsDirs,
        indexDocs: true,
        indexBlog: false,
        indexPages: true,
        docsRouteBasePath: '/docs',
        hashed: true,
        explicitSearchResultPath: true,
      },
    ],
  ],

  themeConfig: {
    image: 'images/terrabuild-social.png',
    mermaid: {
      theme: {
        light: 'base',
        dark: 'dark',
      },
      options: {
        fontFamily: '"SFMono-Regular", "Consolas", monospace',
        themeVariables: {
          fontSize: '14px',
        },
        flowchart: {
          curve: 'basis',
          htmlLabels: true,
          nodeSpacing: 42,
          rankSpacing: 54,
          padding: 18,
          useMaxWidth: true,
        },
      },
    },
    colorMode: {
      defaultMode: 'light',
      disableSwitch: false,
      respectPrefersColorScheme: true,
    },
    navbar: {
      title: 'Terrabuild',
      logo: {
        alt: 'Terrabuild Logo',
        src: 'images/logo.svg',
      },
      items: [
        {
          type: 'docSidebar',
          sidebarId: 'docs',
          label: 'Documentation',
          position: 'left',
        },
        {type: 'search', position: 'right'},
        ...(hasStable
          ? [
              {
                type: 'docsVersionDropdown' as const,
                position: 'right' as const,
                dropdownActiveClassDisabled: true,
                versions: ['current', 'stable'],
              },
            ]
          : []),
        {
          href: '/docs/next/insights',
          label: 'Insights',
          position: 'left',
        },
        {
          href: 'https://github.com/MagnusOpera/Terrabuild',
          label: 'GitHub',
          position: 'right',
        },
      ],
    },
    footer: {
      style: 'light',
      links: [],
      copyright:
        '©️2023-present <img src="/images/logo.svg" alt="Magnus Opera" class="tb-footer-mark" /> <a href="https://magnusopera.io">Magnus Opera SAS</a>',
    },
    prism: {
      theme: prismThemes.github,
      darkTheme: prismThemes.dracula,
      additionalLanguages: ['bash', 'fsharp', 'json', 'hcl'],
    },
  } satisfies Preset.ThemeConfig,
};

export default config;
